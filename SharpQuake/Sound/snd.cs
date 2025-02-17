/// <copyright>
///
/// SharpQuakeEvolved changes by optimus-code, 2019
/// 
/// Based on SharpQuake (Quake Rewritten in C# by Yury Kiselev, 2010.)
///
/// Copyright (C) 1996-1997 Id Software, Inc.
///
/// This program is free software; you can redistribute it and/or
/// modify it under the terms of the GNU General Public License
/// as published by the Free Software Foundation; either version 2
/// of the License, or (at your option) any later version.
///
/// This program is distributed in the hope that it will be useful,
/// but WITHOUT ANY WARRANTY; without even the implied warranty of
/// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
///
/// See the GNU General Public License for more details.
///
/// You should have received a copy of the GNU General Public License
/// along with this program; if not, write to the Free Software
/// Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
/// </copyright>

// sound.h -- client sound i/o functions

namespace SharpQuake
{
    using SharpQuake.Framework;
    using SharpQuake.Framework.IO;
    using SharpQuake.Framework.IO.Sound;
    using System.Numerics;

    // snd_started == Sound._Controller.IsInitialized
    // snd_initialized == Sound._IsInitialized

    /// <summary>
    /// S_functions
    /// </summary>
    public partial class Sound
    {
        public bool IsInitialised => _Controller.IsInitialised;

        public DMA Shm { get; private set; } = new DMA();

        public float BgmVolume => Host.Cvars.BgmVolume.Get<float>();

        public float Volume => Host.Cvars.Volume.Get<float>();

        public const int DEFAULT_SOUND_PACKET_VOLUME = 255;
        public const float DEFAULT_SOUND_PACKET_ATTENUATION = 1.0f;
        public const int MAX_CHANNELS = 128;
        public const int MAX_DYNAMIC_CHANNELS = 8;

        private const int MAX_SFX = 512;



        private readonly ISoundController _Controller = new OpenALController();// NullSoundController();
        private bool _IsInitialized; // snd_initialized

        private readonly SoundEffect_t[] _KnownSfx = new SoundEffect_t[MAX_SFX]; // hunk allocated [MAX_SFX]
        private int _NumSfx; // num_sfx
        private readonly SoundEffect_t[] _AmbientSfx = new SoundEffect_t[AmbientDef.NUM_AMBIENTS]; // *ambient_sfx[NUM_AMBIENTS]
        private bool _Ambient = true; // snd_ambient

        // 0 to MAX_DYNAMIC_CHANNELS-1	= normal entity sounds
        // MAX_DYNAMIC_CHANNELS to MAX_DYNAMIC_CHANNELS + NUM_AMBIENTS -1 = water, etc
        // MAX_DYNAMIC_CHANNELS + NUM_AMBIENTS to total_channels = static sounds
        private readonly Channel_t[] _Channels = new Channel_t[MAX_CHANNELS]; // channels[MAX_CHANNELS]

        private int _TotalChannels; // total_channels

        private readonly float _SoundNominalClipDist = 1000.0f; // sound_nominal_clip_dist
        private Vector3 _ListenerOrigin; // listener_origin
        private Vector3 _ListenerRight; // listener_right

        private int _SoundTime; // soundtime		// sample PAIRS
        private int _PaintedTime; // paintedtime 	// sample PAIRS
        private bool _SoundStarted; // sound_started
        private int _SoundBlocked = 0; // snd_blocked
        private int _OldSamplePos; // oldsamplepos from GetSoundTime()
        private int _Buffers; // buffers from GetSoundTime()
        private int _PlayHash = 345; // hash from S_Play()
        private int _PlayVolHash = 543; // hash S_PlayVol

        // CHANGE
        private Host Host
        {
            get;
            set;
        }

        // S_Init (void)
        public void Initialise()
        {
            Host.Cvars.BgmVolume = Host.CVars.Add("bgmvolume", 1f, ClientVariableFlags.Archive);// = { "bgmvolume", "1", true };
            Host.Cvars.Volume = Host.CVars.Add("volume", 0.7f, ClientVariableFlags.Archive);// = { "volume", "0.7", true };
            Host.Cvars.NoSound = Host.CVars.Add("nosound", false);// = { "nosound", "0" };
            Host.Cvars.Precache = Host.CVars.Add("precache", true);// = { "precache", "1" };
            Host.Cvars.LoadAs8bit = Host.CVars.Add("loadas8bit", false);// = { "loadas8bit", "0" };
            Host.Cvars.BgmBuffer = Host.CVars.Add("bgmbuffer", 4096f);// = { "bgmbuffer", "4096" };
            Host.Cvars.AmbientLevel = Host.CVars.Add("ambient_level", 0.3f);// = { "ambient_level", "0.3" };
            Host.Cvars.AmbientFade = Host.CVars.Add("ambient_fade", 100f);// = { "ambient_fade", "100" };
            Host.Cvars.NoExtraUpdate = Host.CVars.Add("snd_noextraupdate", false);// = { "snd_noextraupdate", "0" };
            Host.Cvars.Show = Host.CVars.Add("snd_show", false);// = { "snd_show", "0" };
            Host.Cvars.MixAhead = Host.CVars.Add("_snd_mixahead", 0.1f, ClientVariableFlags.Archive);// = { "_snd_mixahead", "0.1", true };


            Host.Console.Print("\nSound Initialization\n");

            if (CommandLine.HasParam("-nosound"))
            {
                return;
            }

            for (var i = 0; i < _Channels.Length; i++)
            {
                _Channels[i] = new Channel_t();
            }

            Host.Commands.Add("play", Play);
            Host.Commands.Add("playvol", PlayVol);
            Host.Commands.Add("stopsound", StopAllSoundsCmd);
            Host.Commands.Add("soundlist", SoundList);
            Host.Commands.Add("soundinfo", SoundInfo_f);

            _IsInitialized = true;

            Startup();

            InitScaletable();

            _NumSfx = 0;

            Host.Console.Print("Sound sampling rate: {0}\n", Shm.speed);

            // provides a tick sound until washed clean
            _AmbientSfx[AmbientDef.AMBIENT_WATER] = PrecacheSound("ambience/water1.wav");
            _AmbientSfx[AmbientDef.AMBIENT_SKY] = PrecacheSound("ambience/wind2.wav");

            StopAllSounds(true);
        }

        // S_AmbientOff (void)
        public void AmbientOff()
        {
            _Ambient = false;
        }

        // S_AmbientOn (void)
        public void AmbientOn()
        {
            _Ambient = true;
        }

        // S_Shutdown (void)
        public void Shutdown()
        {
            if (!_Controller.IsInitialised)
            {
                return;
            }

            if (Shm != null)
            {
                Shm.gamealive = false;
            }

            _Controller.Shutdown();
            Shm = null;
        }

        // S_TouchSound (char *sample)
        public void TouchSound(string sample)
        {
            if (!_Controller.IsInitialised)
            {
                return;
            }

            var sfx = FindName(sample);
            Host.Cache.Check(sfx.cache);
        }

        // S_ClearBuffer (void)
        public void ClearBuffer()
        {
            if (!_Controller.IsInitialised || Shm == null || Shm.buffer == null)
            {
                return;
            }

            _Controller.ClearBuffer();
        }

        // S_StaticSound (sfx_t *sfx, vec3_t origin, float vol, float attenuation)
        public void StaticSound(SoundEffect_t sfx, ref Vector3 origin, float vol, float attenuation)
        {
            if (sfx == null)
            {
                return;
            }

            if (_TotalChannels == MAX_CHANNELS)
            {
                Host.Console.Print("total_channels == MAX_CHANNELS\n");
                return;
            }

            var ss = _Channels[_TotalChannels];
            _TotalChannels++;

            var sc = LoadSound(sfx);
            if (sc == null)
            {
                return;
            }

            if (sc.loopstart == -1)
            {
                Host.Console.Print("Sound {0} not looped\n", sfx.name);
                return;
            }

            ss.sfx = sfx;
            ss.origin = origin;
            ss.master_vol = (int)vol;
            ss.dist_mult = attenuation / 64 / _SoundNominalClipDist;
            ss.end = _PaintedTime + sc.length;

            Spatialize(ss);
        }

        // S_StartSound (int entnum, int entchannel, sfx_t *sfx, vec3_t origin, float fvol,  float attenuation)
        public void StartSound(int entnum, int entchannel, SoundEffect_t sfx, ref Vector3 origin, float fvol, float attenuation)
        {
            if (!_SoundStarted || sfx == null)
            {
                return;
            }

            if (Host.Cvars.NoSound.Get<bool>())
            {
                return;
            }

            var vol = (int)(fvol * 255);

            // pick a channel to play on
            var target_chan = PickChannel(entnum, entchannel);
            if (target_chan == null)
            {
                return;
            }

            // spatialize
            //memset (target_chan, 0, sizeof(*target_chan));
            target_chan.origin = origin;
            target_chan.dist_mult = attenuation / _SoundNominalClipDist;
            target_chan.master_vol = vol;
            target_chan.entnum = entnum;
            target_chan.entchannel = entchannel;
            Spatialize(target_chan);

            if (target_chan.leftvol == 0 && target_chan.rightvol == 0)
            {
                return;     // not audible at all
            }

            // new channel
            var sc = LoadSound(sfx);
            if (sc == null)
            {
                target_chan.sfx = null;
                return;		// couldn't load the sound's data
            }

            target_chan.sfx = sfx;
            target_chan.pos = 0;
            target_chan.end = _PaintedTime + sc.length;

            // if an identical sound has also been started this frame, offset the pos
            // a bit to keep it from just making the first one louder
            for (var i = AmbientDef.NUM_AMBIENTS; i < AmbientDef.NUM_AMBIENTS + MAX_DYNAMIC_CHANNELS; i++)
            {
                var check = _Channels[i];
                if (check == target_chan)
                {
                    continue;
                }

                if (check.sfx == sfx && check.pos == 0)
                {
                    var skip = MathLib.Random((int)(0.1 * Shm.speed));// rand() % (int)(0.1 * shm->speed);
                    if (skip >= target_chan.end)
                    {
                        skip = target_chan.end - 1;
                    }

                    target_chan.pos += skip;
                    target_chan.end -= skip;
                    break;
                }
            }
        }

        // S_StopSound (int entnum, int entchannel)
        public void StopSound(int entnum, int entchannel)
        {
            for (var i = 0; i < MAX_DYNAMIC_CHANNELS; i++)
            {
                if (_Channels[i].entnum == entnum &&
                    _Channels[i].entchannel == entchannel)
                {
                    _Channels[i].end = 0;
                    _Channels[i].sfx = null;
                    return;
                }
            }
        }

        // sfx_t *S_PrecacheSound (char *sample)
        public SoundEffect_t PrecacheSound(string sample)
        {
            if (!_IsInitialized || Host.Cvars.NoSound.Get<bool>())
            {
                return null;
            }

            var sfx = FindName(sample);

            // cache it in
            if (Host.Cvars.Precache.Get<bool>())
            {
                LoadSound(sfx);
            }

            return sfx;
        }

        // void S_ClearPrecache (void)
        public static void ClearPrecache()
        {
            // nothing to do
        }

        // void S_Update (vec3_t origin, vec3_t v_forward, vec3_t v_right, vec3_t v_up)
        //
        // Called once each time through the main loop
        public void Update(ref Vector3 origin, ref Vector3 right)
        {
            if (!_IsInitialized || (_SoundBlocked > 0))
            {
                return;
            }

            _ListenerOrigin = origin;
            _ListenerRight = right;

            // update general area ambient sound sources
            UpdateAmbientSounds();

            Channel_t combine = null;

            // update spatialization for and dynamic sounds
            //channel_t ch = channels + NUM_AMBIENTS;
            for (var i = AmbientDef.NUM_AMBIENTS; i < _TotalChannels; i++)
            {
                var ch = _Channels[i];// channels + NUM_AMBIENTS;
                if (ch.sfx == null)
                {
                    continue;
                }

                Spatialize(ch);  // respatialize channel
                if (ch.leftvol == 0 && ch.rightvol == 0)
                {
                    continue;
                }

                // try to combine static sounds with a previous channel of the same
                // sound effect so we don't mix five torches every frame
                if (i >= MAX_DYNAMIC_CHANNELS + AmbientDef.NUM_AMBIENTS)
                {
                    // see if it can just use the last one
                    if (combine != null && combine.sfx == ch.sfx)
                    {
                        combine.leftvol += ch.leftvol;
                        combine.rightvol += ch.rightvol;
                        ch.leftvol = ch.rightvol = 0;
                        continue;
                    }
                    // search for one
                    combine = _Channels[MAX_DYNAMIC_CHANNELS + AmbientDef.NUM_AMBIENTS];// channels + MAX_DYNAMIC_CHANNELS + NUM_AMBIENTS;
                    int j;
                    for (j = MAX_DYNAMIC_CHANNELS + AmbientDef.NUM_AMBIENTS; j < i; j++)
                    {
                        combine = _Channels[j];
                        if (combine.sfx == ch.sfx)
                        {
                            break;
                        }
                    }

                    if (j == _TotalChannels)
                    {
                        combine = null;
                    }
                    else
                    {
                        if (combine != ch)
                        {
                            combine.leftvol += ch.leftvol;
                            combine.rightvol += ch.rightvol;
                            ch.leftvol = ch.rightvol = 0;
                        }
                        continue;
                    }
                }
            }

            //
            // debugging output
            //
            if (Host.Cvars.Show.Get<bool>())
            {
                var total = 0;
                for (var i = 0; i < _TotalChannels; i++)
                {
                    var ch = _Channels[i];
                    if (ch.sfx != null && (ch.leftvol > 0 || ch.rightvol > 0))
                    {
                        total++;
                    }
                }
                Host.Console.Print("----({0})----\n", total);
            }

            // mix some sound
            Update();
        }

        // S_StopAllSounds (qboolean clear)
        public void StopAllSounds(bool clear)
        {
            if (!_Controller.IsInitialised)
            {
                return;
            }

            _TotalChannels = MAX_DYNAMIC_CHANNELS + AmbientDef.NUM_AMBIENTS;	// no statics

            for (var i = 0; i < MAX_CHANNELS; i++)
            {
                if (_Channels[i].sfx != null)
                {
                    _Channels[i].Clear();
                }
            }

            if (clear)
            {
                ClearBuffer();
            }
        }

        // void S_BeginPrecaching (void)
        public static void BeginPrecaching()
        {
            // nothing to do
        }

        // void S_EndPrecaching (void)
        public static void EndPrecaching()
        {
            // nothing to do
        }

        // void S_ExtraUpdate (void)
        public void ExtraUpdate()
        {
            if (!_IsInitialized)
            {
                return;
            }
#if _WIN32
	        IN_Accumulate ();
#endif

            if (Host.Cvars.NoExtraUpdate.Get<bool>())
            {
                return;     // don't pollute timings
            }

            Update();
        }

        // void S_LocalSound (char *s)
        public void LocalSound(string sound)
        {
            if (Host.Cvars.NoSound == null || Host.Cvars.NoSound.Get<bool>())
            {
                return;
            }

            if (!_Controller.IsInitialised)
            {
                return;
            }

            var sfx = PrecacheSound(sound);
            if (sfx == null)
            {
                Host.Console.Print("S_LocalSound: can't cache {0}\n", sound);
                return;
            }
            StartSound(Host.Client.Cl.viewentity, -1, sfx, ref Utilities.ZeroVector, 1, 1);
        }

        // S_Startup
        public void Startup()
        {
            if (_IsInitialized && !_Controller.IsInitialised)
            {
                _Controller.Initialise(Host);
                _SoundStarted = _Controller.IsInitialised;
            }
        }

        /// <summary>
        /// S_BlockSound
        /// </summary>
        public void BlockSound()
        {
            _SoundBlocked++;

            if (_SoundBlocked == 1)
            {
                _Controller.ClearBuffer();  //waveOutReset (hWaveOut);
            }
        }

        /// <summary>
        /// S_UnblockSound
        /// </summary>
        public void UnblockSound()
        {
            _SoundBlocked--;
        }

        // S_Play
        private void Play(CommandMessage msg)
        {
            foreach (var parameter in msg.Parameters)
            {
                var name = parameter;

                if (name.Contains('.'))
                {
                    name += ".wav";
                }

                var sfx = PrecacheSound(name);
                StartSound(_PlayHash++, 0, sfx, ref _ListenerOrigin, 1.0f, 1.0f);
            }
        }

        // S_PlayVol
        private void PlayVol(CommandMessage msg)
        {
            for (var i = 0; i < msg.Parameters.Length; i += 2)
            {
                var name = msg.Parameters[i];
                if (name.Contains('.'))
                {
                    name += ".wav";
                }

                var sfx = PrecacheSound(name);
                var vol = float.Parse(msg.Parameters[i + 1]);
                StartSound(_PlayVolHash++, 0, sfx, ref _ListenerOrigin, vol, 1.0f);
            }
        }

        // S_SoundList
        private void SoundList(CommandMessage msg)
        {
            var total = 0;
            for (var i = 0; i < _NumSfx; i++)
            {
                var sfx = _KnownSfx[i];
                var sc = (SoundEffectCache_t)Host.Cache.Check(sfx.cache);
                if (sc == null)
                {
                    continue;
                }

                var size = sc.length * sc.width * (sc.stereo + 1);
                total += size;
                if (sc.loopstart >= 0)
                {
                    Host.Console.Print("L");
                }
                else
                {
                    Host.Console.Print(" ");
                }

                Host.Console.Print("({0:d2}b) {1:g6} : {2}\n", sc.width * 8, size, sfx.name);
            }
            Host.Console.Print("Total resident: {0}\n", total);
        }

        // S_SoundInfo_f
        private void SoundInfo_f(CommandMessage msg)
        {
            if (!_Controller.IsInitialised || Shm == null)
            {
                Host.Console.Print("sound system not started\n");
                return;
            }

            Host.Console.Print("{0:d5} stereo\n", Shm.channels - 1);
            Host.Console.Print("{0:d5} samples\n", Shm.samples);
            Host.Console.Print("{0:d5} samplepos\n", Shm.samplepos);
            Host.Console.Print("{0:d5} samplebits\n", Shm.samplebits);
            Host.Console.Print("{0:d5} submission_chunk\n", Shm.submission_chunk);
            Host.Console.Print("{0:d5} speed\n", Shm.speed);
            //Host.Console.Print("0x%x dma buffer\n", _shm.buffer);
            Host.Console.Print("{0:d5} total_channels\n", _TotalChannels);
        }

        // S_StopAllSoundsC
        private void StopAllSoundsCmd(CommandMessage msg)
        {
            StopAllSounds(true);
        }

        // S_FindName
        private SoundEffect_t FindName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                Utilities.Error("S_FindName: NULL or empty\n");
            }

            if (name.Length >= QDef.MAX_QPATH)
            {
                Utilities.Error("Sound name too long: {0}", name);
            }

            // see if already loaded
            for (var i = 0; i < _NumSfx; i++)
            {
                if (_KnownSfx[i].name == name)// !Q_strcmp(known_sfx[i].name, name))
                {
                    return _KnownSfx[i];
                }
            }

            if (_NumSfx == MAX_SFX)
            {
                Utilities.Error("S_FindName: out of sfx_t");
            }

            var sfx = _KnownSfx[_NumSfx];
            sfx.name = name;

            _NumSfx++;
            return sfx;
        }

        // SND_Spatialize
        private void Spatialize(Channel_t ch)
        {
            // anything coming from the view entity will allways be full volume
            if (ch.entnum == Host.Client.Cl.viewentity)
            {
                ch.leftvol = ch.master_vol;
                ch.rightvol = ch.master_vol;
                return;
            }

            // calculate stereo seperation and distance attenuation
            var source_vec = ch.origin - _ListenerOrigin;

            var dist = MathLib.Normalize(ref source_vec) * ch.dist_mult;
            var dot = Vector3.Dot(_ListenerRight, source_vec);

            float rscale, lscale;
            if (Shm.channels == 1)
            {
                rscale = 1.0f;
                lscale = 1.0f;
            }
            else
            {
                rscale = 1.0f + dot;
                lscale = 1.0f - dot;
            }

            // add in distance effect
            var scale = (1.0f - dist) * rscale;
            ch.rightvol = (int)(ch.master_vol * scale);
            if (ch.rightvol < 0)
            {
                ch.rightvol = 0;
            }

            scale = (1.0f - dist) * lscale;
            ch.leftvol = (int)(ch.master_vol * scale);
            if (ch.leftvol < 0)
            {
                ch.leftvol = 0;
            }
        }

        // S_LoadSound
        private SoundEffectCache_t LoadSound(SoundEffect_t s)
        {
            // see if still in memory
            var sc = (SoundEffectCache_t)Host.Cache.Check(s.cache);
            if (sc != null)
            {
                return sc;
            }

            // load it in
            var namebuffer = "sound/" + s.name;

            var data = FileSystem.LoadFile(namebuffer);
            if (data == null)
            {
                Host.Console.Print("Couldn't load {0}\n", namebuffer);
                return null;
            }

            var info = GetWavInfo(s.name, data);
            if (info.channels != 1)
            {
                Host.Console.Print("{0} is a stereo sample\n", s.name);
                return null;
            }

            var stepscale = info.rate / (float)Shm.speed;
            var len = (int)(info.samples / stepscale);

            len *= info.width * info.channels;

            s.cache = Host.Cache.Alloc(len);
            if (s.cache == null)
            {
                return null;
            }

            sc = new SoundEffectCache_t
            {
                length = info.samples,
                loopstart = info.loopstart,
                speed = info.rate,
                width = info.width,
                stereo = info.channels
            };
            s.cache.data = sc;

            ResampleSfx(s, sc.speed, sc.width, new ByteArraySegment(data, info.dataofs));

            return sc;
        }

        // SND_PickChannel
        private Channel_t PickChannel(int entnum, int entchannel)
        {
            // Check for replacement sound, or find the best one to replace
            var first_to_die = -1;
            var life_left = 0x7fffffff;
            for (var ch_idx = AmbientDef.NUM_AMBIENTS; ch_idx < AmbientDef.NUM_AMBIENTS + MAX_DYNAMIC_CHANNELS; ch_idx++)
            {
                if (entchannel != 0		// channel 0 never overrides
                    && _Channels[ch_idx].entnum == entnum
                    && (_Channels[ch_idx].entchannel == entchannel || entchannel == -1))
                {
                    // allways override sound from same entity
                    first_to_die = ch_idx;
                    break;
                }

                // don't let monster sounds override player sounds
                if (_Channels[ch_idx].entnum == Host.Client.Cl.viewentity && entnum != Host.Client.Cl.viewentity && _Channels[ch_idx].sfx != null)
                {
                    continue;
                }

                if (_Channels[ch_idx].end - _PaintedTime < life_left)
                {
                    life_left = _Channels[ch_idx].end - _PaintedTime;
                    first_to_die = ch_idx;
                }
            }

            if (first_to_die == -1)
            {
                return null;
            }

            if (_Channels[first_to_die].sfx != null)
            {
                _Channels[first_to_die].sfx = null;
            }

            return _Channels[first_to_die];
        }

        // S_UpdateAmbientSounds
        private void UpdateAmbientSounds()
        {
            if (!_Ambient)
            {
                return;
            }

            // calc ambient sound levels
            if (Host.Client.Cl.worldmodel == null)
            {
                return;
            }

            var l = Host.Client.Cl.worldmodel.PointInLeaf(ref _ListenerOrigin);
            if (l == null || Host.Cvars.AmbientLevel.Get<float>() == 0)
            {
                for (var i = 0; i < AmbientDef.NUM_AMBIENTS; i++)
                {
                    _Channels[i].sfx = null;
                }

                return;
            }

            for (var i = 0; i < AmbientDef.NUM_AMBIENTS; i++)
            {
                var chan = _Channels[i];
                chan.sfx = _AmbientSfx[i];

                var vol = Host.Cvars.AmbientLevel.Get<float>() * l.ambient_sound_level[i];
                if (vol < 8)
                {
                    vol = 0;
                }

                // don't adjust volume too fast
                if (chan.master_vol < vol)
                {
                    chan.master_vol += (int)(Host.FrameTime * Host.Cvars.AmbientFade.Get<float>());
                    if (chan.master_vol > vol)
                    {
                        chan.master_vol = (int)vol;
                    }
                }
                else if (chan.master_vol > vol)
                {
                    chan.master_vol -= (int)(Host.FrameTime * Host.Cvars.AmbientFade.Get<float>());
                    if (chan.master_vol < vol)
                    {
                        chan.master_vol = (int)vol;
                    }
                }

                chan.leftvol = chan.rightvol = chan.master_vol;
            }
        }

        // S_Update_
        private void Update()
        {
            if (!_SoundStarted || (_SoundBlocked > 0) || Shm == null)
            {
                return;
            }

            // Updates DMA time
            GetSoundTime();

            // check to make sure that we haven't overshot
            if (_PaintedTime < _SoundTime)
            {
                _PaintedTime = _SoundTime;
            }

            // mix ahead of current position
            var endtime = (int)(_SoundTime + (Host.Cvars.MixAhead.Get<float>() * Shm.speed));
            var samps = Shm.samples >> (Shm.channels - 1);
            if (endtime - _SoundTime > samps)
            {
                endtime = _SoundTime + samps;
            }

            PaintChannels(endtime);
        }

        // GetSoundtime
        private void GetSoundTime()
        {
            var fullsamples = Shm.samples / Shm.channels;
            var samplepos = _Controller.GetPosition();
            if (samplepos < _OldSamplePos)
            {
                _Buffers++; // buffer wrapped

                if (_PaintedTime > 0x40000000)
                {
                    // time to chop things off to avoid 32 bit limits
                    _Buffers = 0;
                    _PaintedTime = fullsamples;
                    StopAllSounds(true);
                }
            }
            _OldSamplePos = samplepos;
            _SoundTime = (_Buffers * fullsamples) + (samplepos / Shm.channels);
        }

        public Sound(Host host)
        {
            Host = host;

            for (var i = 0; i < _KnownSfx.Length; i++)
            {
                _KnownSfx[i] = new SoundEffect_t();
            }
        }
    }
}
