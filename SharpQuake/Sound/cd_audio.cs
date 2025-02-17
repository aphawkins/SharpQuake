/// <copyright>
///
/// Rewritten in C# by Yury Kiselev, 2010.
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

// cdaudio.h

namespace SharpQuake
{
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

    using System;
    using System.IO;
    using NVorbis.OpenTKSupport;
    using SharpQuake.Framework;
    using SharpQuake.Framework.IO;

    /// <summary>
    /// CDAudio_functions
    /// </summary>

    public class CdAudio
    {
#if _WINDOWS
        private ICDAudioController _Controller;
#else
        private readonly NullCDAudioController _Controller;
#endif

        // CHANGE
        private Host Host
        {
            get;
            set;
        }

        public CdAudio(Host host)
        {
            Host = host;
            _Controller = new NullCDAudioController(Host);
        }
        /// <summary>
        /// CDAudio_Init
        /// </summary>
        public bool Initialise()
        {
            if (Host.Client.Cls.state == ClientActive.ca_dedicated)
            {
                return false;
            }

            if (CommandLine.HasParam("-nocdaudio"))
            {
                return false;
            }

            _Controller.Initialise();

            if (NullCDAudioController.IsInitialised)
            {
                Host.Commands.Add("cd", CD_f);
                Host.Console.Print("CD Audio (Fallback) Initialized\n");
            }

            return NullCDAudioController.IsInitialised;
        }

        // CDAudio_Play(byte track, qboolean looping)
        public void Play(byte track, bool looping)
        {
            _Controller.Play(track, looping);
#if DEBUG
            Console.WriteLine("DEBUG: track byte:{0} - loop byte: {1}", track, looping);
#endif
        }

        // CDAudio_Stop
        public void Stop()
        {
            _Controller.Stop();
        }

        // CDAudio_Pause
        public void Pause()
        {
            _Controller.Pause();
        }

        // CDAudio_Resume
        public void Resume()
        {
            _Controller.Resume();
        }

        // CDAudio_Shutdown
        public void Shutdown()
        {
            _Controller.Shutdown();
        }

        // CDAudio_Update
        public void Update()
        {
            _Controller.Update();
        }

        private void CD_f(CommandMessage msg)
        {
            if (msg.Parameters == null || msg.Parameters.Length < 1)
            {
                return;
            }

            var command = msg.Parameters[0];

            if (Utilities.SameText(command, "on"))
            {
                NullCDAudioController.IsEnabled = true;
                return;
            }

            if (Utilities.SameText(command, "off"))
            {
                if (_Controller.IsPlaying)
                {
                    _Controller.Stop();
                }

                NullCDAudioController.IsEnabled = false;
                return;
            }

            if (Utilities.SameText(command, "reset"))
            {
                NullCDAudioController.IsEnabled = true;
                if (_Controller.IsPlaying)
                {
                    _Controller.Stop();
                }

                NullCDAudioController.ReloadDiskInfo();
                return;
            }

            if (Utilities.SameText(command, "remap"))
            {
                var ret = msg.Parameters.Length - 1;
                var remap = _Controller.Remap;
                if (ret <= 0)
                {
                    for (var n = 1; n < 100; n++)
                    {
                        if (remap[n] != n)
                        {
                            Host.Console.Print("  {0} -> {1}\n", n, remap[n]);
                        }
                    }

                    return;
                }
                for (var n = 1; n <= ret; n++)
                {
                    remap[n] = (byte)MathLib.AToI(msg.Parameters[n]);
                }

                return;
            }

            if (Utilities.SameText(command, "close"))
            {
                NullCDAudioController.CloseDoor();
                return;
            }

            if (!NullCDAudioController.IsValidCD)
            {
                NullCDAudioController.ReloadDiskInfo();
                if (!NullCDAudioController.IsValidCD)
                {
                    Host.Console.Print("No CD in player.\n");
                    return;
                }
            }

            if (Utilities.SameText(command, "play"))
            {
                _Controller.Play((byte)MathLib.AToI(msg.Parameters[1]), false);
                return;
            }

            if (Utilities.SameText(command, "loop"))
            {
                _Controller.Play((byte)MathLib.AToI(msg.Parameters[1]), true);
                return;
            }

            if (Utilities.SameText(command, "stop"))
            {
                _Controller.Stop();
                return;
            }

            if (Utilities.SameText(command, "pause"))
            {
                _Controller.Pause();
                return;
            }

            if (Utilities.SameText(command, "resume"))
            {
                _Controller.Resume();
                return;
            }

            if (Utilities.SameText(command, "eject"))
            {
                if (_Controller.IsPlaying)
                {
                    _Controller.Stop();
                }

                NullCDAudioController.Eject();
                return;
            }

            if (Utilities.SameText(command, "info"))
            {
                Host.Console.Print("%u tracks\n", NullCDAudioController.MaxTrack);
                if (_Controller.IsPlaying)
                {
                    Host.Console.Print("Currently {0} track {1}\n", _Controller.IsLooping ? "looping" : "playing", NullCDAudioController.CurrentTrack);
                }
                else if (_Controller.IsPaused)
                {
                    Host.Console.Print("Paused {0} track {1}\n", _Controller.IsLooping ? "looping" : "playing", NullCDAudioController.CurrentTrack);
                }

                Host.Console.Print("Volume is {0}\n", _Controller.Volume);
                return;
            }
        }
    }

    internal class NullCDAudioController
    {
        private OggStream oggStream;
        private OggStreamer streamer;
        private string trackid;
        private string trackpath;
        private bool _noAudio = false;
        private bool _noPlayback = false;

        private Host Host
        {
            get;
            set;
        }

        public NullCDAudioController(Host host)
        {
            Host = host;
            Remap = new byte[100];
        }

        #region ICDAudioController Members

        public static bool IsInitialised => true;

        public static bool IsEnabled
        {
            get => true;
            set
            {

            }
        }

        public bool IsPlaying { get; }

        public bool IsPaused { get; }

        public static bool IsValidCD => false;

        public bool IsLooping { get; private set; }

        public byte[] Remap { get; }

        public static byte MaxTrack => 0;

        public static byte CurrentTrack => 0;

        public float Volume { get; set; }

        public void Initialise()
        {
            streamer = new OggStreamer(441000);
            Volume = Host.Sound.BgmVolume;

            if (Directory.Exists(string.Format("{0}/{1}/music/", QuakeParameter.globalbasedir, QuakeParameter.globalgameid)) == false)
            {
                _noAudio = true;
            }
        }

        public void Play(byte track, bool looping)
        {
            if (_noAudio == false)
            {
                trackid = track.ToString("00");
                trackpath = string.Format("{0}/{1}/music/track{2}.ogg", QuakeParameter.globalbasedir, QuakeParameter.globalgameid, trackid);
#if DEBUG
                Console.WriteLine("DEBUG: track path:{0} ", trackpath);
#endif
                try
                {
                    IsLooping = looping;
                    if (oggStream != null)
                    {
                        oggStream.Stop();
                    }

                    oggStream = new OggStream(trackpath, 3)
                    {
                        IsLooped = looping
                    };
                    oggStream.Play();
                    oggStream.Volume = Volume;
                    _noPlayback = false;
                }
                catch
                {
                    Console.WriteLine("Could not find or play {0}", trackpath);
                    _noPlayback = true;
                    //throw;
                }
            }
        }

        public void Stop()
        {
            if (streamer == null)
            {
                return;
            }

            if (_noAudio == true)
            {
                return;
            }

            oggStream.Stop();
        }

        public void Pause()
        {
            if (streamer == null)
            {
                return;
            }

            if (_noAudio == true)
            {
                return;
            }

            oggStream.Pause();
        }

        public void Resume()
        {
            if (streamer == null)
            {
                return;
            }

            oggStream.Resume();
        }

        public void Shutdown()
        {
            if (streamer == null)
            {
                return;
            }

            if (_noAudio == true)
            {
                return;
            }

            //oggStream.Dispose();
            streamer.Dispose();
        }

        public void Update()
        {
            if (streamer == null)
            {
                return;
            }

            if (_noAudio == true)
            {
                return;
            }

            if (_noPlayback == true)
            {
                return;
            }

            /*if (waveOut.PlaybackState == PlaybackState.Paused)
            {
                _isPaused = true;
            }
            else if (waveOut.PlaybackState == PlaybackState.Playing)
            {
                _isPaused = false;
            }

            if (waveOut.PlaybackState == PlaybackState.Paused || waveOut.PlaybackState == PlaybackState.Stopped)
            {
                _isPlaying = false;
            }
            else if (waveOut.PlaybackState == PlaybackState.Playing)
            {
                _isPlaying = true;
            }*/

            Volume = Host.Sound.BgmVolume;
            oggStream.Volume = Volume;
        }

        public static void ReloadDiskInfo()
        {
        }

        public static void CloseDoor()
        {
        }

        public static void Eject()
        {
        }

        #endregion ICDAudioController Members
    }
}
