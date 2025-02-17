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

// cl_parse.c

namespace SharpQuake
{
    using System;
    using SharpQuake.Framework;
    using SharpQuake.Framework.IO;
    using SharpQuake.Game.Data.Models;
    using SharpQuake.Game.World;

    public partial class Client
    {
        private const string ConsoleBar = "\n\n\u001D\u001E\u001E\u001E\u001E\u001E\u001E\u001E\u001E\u001E\u001E\u001E\u001E\u001E\u001E\u001E\u001E\u001E\u001E\u001E\u001E\u001E\u001E\u001E\u001E\u001E\u001E\u001E\u001E\u001E\u001E\u001E\u001E\u001E\u001E\u001E\u001E\u001E\u001F\n\n";

        private static readonly string[] _SvcStrings = new string[]
        {
            "svc_bad",
            "svc_nop",
            "svc_disconnect",
            "svc_updatestat",
            "svc_version",		// [long] server version
	        "svc_setview",		// [short] entity number
	        "svc_sound",			// <see code>
	        "svc_time",			// [float] server time
	        "svc_print",			// [string] null terminated string
	        "svc_stufftext",		// [string] stuffed into client's console buffer
						        // the string should be \n terminated
	        "svc_setangle",		// [vec3] set the view angle to this absolute value

	        "svc_serverinfo",		// [long] version
						        // [string] signon string
						        // [string]..[0]model cache [string]...[0]sounds cache
						        // [string]..[0]item cache
	        "svc_lightstyle",		// [byte] [string]
	        "svc_updatename",		// [byte] [string]
	        "svc_updatefrags",	// [byte] [short]
	        "svc_clientdata",		// <shortbits + data>
	        "svc_stopsound",		// <see code>
	        "svc_updatecolors",	// [byte] [byte]
	        "svc_particle",		// [vec3] <variable>
	        "svc_damage",			// [byte] impact [byte] blood [vec3] from

	        "svc_spawnstatic",
            "OBSOLETE svc_spawnbinary",
            "svc_spawnbaseline",

            "svc_temp_entity",		// <variable>
	        "svc_setpause",
            "svc_signonnum",
            "svc_centerprint",
            "svc_killedmonster",
            "svc_foundsecret",
            "svc_spawnstaticsound",
            "svc_intermission",
            "svc_finale",			// [string] music [string] text
	        "svc_cdtrack",			// [byte] track [byte] looptrack
	        "svc_sellscreen",
            "svc_cutscene"
        };

        private readonly int[] _BitCounts = new int[16]; // bitcounts
        private object _MsgState; // used by KeepaliveMessage function
        private float _LastMsg; // static float lastmsg from CL_KeepaliveMessage

        /// <summary>
        /// CL_ParseServerMessage
        /// </summary>
        private void ParseServerMessage()
        {
            //
            // if recording demos, copy the message out
            //
            if (Host.Cvars.ShowNet.Get<int>() == 1)
            {
                Host.Console.Print("{0} ", Host.Network.Message.Length);
            }
            else if (Host.Cvars.ShowNet.Get<int>() == 2)
            {
                Host.Console.Print("------------------\n");
            }

            Cl.onground = false;	// unless the server says otherwise

            //
            // parse the message
            //
            Host.Network.Reader.Reset();
            int i;
            while (true)
            {
                if (Host.Network.Reader.IsBadRead)
                {
                    Host.Error("CL_ParseServerMessage: Bad server message");
                }

                var cmd = Host.Network.Reader.ReadByte();
                if (cmd == -1)
                {
                    ShowNet("END OF MESSAGE");
                    return;	// end of message
                }

                // if the high bit of the command byte is set, it is a fast update
                if ((cmd & 128) != 0)
                {
                    ShowNet("fast update");
                    ParseUpdate(cmd & 127);
                    continue;
                }

                ShowNet(_SvcStrings[cmd]);

                // other commands
                switch (cmd)
                {
                    default:
                        Host.Error("CL_ParseServerMessage: Illegible server message\n");
                        break;

                    case ProtocolDef.svc_nop:
                        break;

                    case ProtocolDef.svc_time:
                        Cl.mtime[1] = Cl.mtime[0];
                        Cl.mtime[0] = Host.Network.Reader.ReadFloat();
                        break;

                    case ProtocolDef.svc_clientdata:
                        i = Host.Network.Reader.ReadShort();
                        ParseClientData(i);
                        break;

                    case ProtocolDef.svc_version:
                        i = Host.Network.Reader.ReadLong();
                        if (i != ProtocolDef.PROTOCOL_VERSION)
                        {
                            Host.Error("CL_ParseServerMessage: Server is protocol {0} instead of {1}\n", i, ProtocolDef.PROTOCOL_VERSION);
                        }

                        break;

                    case ProtocolDef.svc_disconnect:
                        Host.EndGame("Server disconnected\n");
                        break;

                    case ProtocolDef.svc_print:
                        Host.Console.Print(Host.Network.Reader.ReadString());
                        break;

                    case ProtocolDef.svc_centerprint:
                        Host.Screen.CenterPrint(Host.Network.Reader.ReadString());
                        break;

                    case ProtocolDef.svc_stufftext:
                        Host.Commands.Buffer.Append(Host.Network.Reader.ReadString());
                        break;

                    case ProtocolDef.svc_damage:
                        Host.View.ParseDamage();
                        break;

                    case ProtocolDef.svc_serverinfo:
                        ParseServerInfo();
                        Host.Screen.VidDef.recalc_refdef = true;	// leave intermission full screen
                        break;

                    case ProtocolDef.svc_setangle:
                        Cl.viewangles.X = Host.Network.Reader.ReadAngle();
                        Cl.viewangles.Y = Host.Network.Reader.ReadAngle();
                        Cl.viewangles.Z = Host.Network.Reader.ReadAngle();
                        break;

                    case ProtocolDef.svc_setview:
                        Cl.viewentity = Host.Network.Reader.ReadShort();
                        break;

                    case ProtocolDef.svc_lightstyle:
                        i = Host.Network.Reader.ReadByte();
                        if (i >= QDef.MAX_LIGHTSTYLES)
                        {
                            Utilities.Error("svc_lightstyle > MAX_LIGHTSTYLES");
                        }

                        LightStyle[i].map = Host.Network.Reader.ReadString();
                        break;

                    case ProtocolDef.svc_sound:
                        ParseStartSoundPacket();
                        break;

                    case ProtocolDef.svc_stopsound:
                        i = Host.Network.Reader.ReadShort();
                        Host.Sound.StopSound(i >> 3, i & 7);
                        break;

                    case ProtocolDef.svc_updatename:
                        Host.Hud.Changed();
                        i = Host.Network.Reader.ReadByte();
                        if (i >= Cl.maxclients)
                        {
                            Host.Error("CL_ParseServerMessage: svc_updatename > MAX_SCOREBOARD");
                        }

                        Cl.scores[i].name = Host.Network.Reader.ReadString();
                        break;

                    case ProtocolDef.svc_updatefrags:
                        Host.Hud.Changed();
                        i = Host.Network.Reader.ReadByte();
                        if (i >= Cl.maxclients)
                        {
                            Host.Error("CL_ParseServerMessage: svc_updatefrags > MAX_SCOREBOARD");
                        }

                        Cl.scores[i].frags = Host.Network.Reader.ReadShort();
                        break;

                    case ProtocolDef.svc_updatecolors:
                        Host.Hud.Changed();
                        i = Host.Network.Reader.ReadByte();
                        if (i >= Cl.maxclients)
                        {
                            Host.Error("CL_ParseServerMessage: svc_updatecolors > MAX_SCOREBOARD");
                        }

                        Cl.scores[i].colors = Host.Network.Reader.ReadByte();
                        NewTranslation(i);
                        break;

                    case ProtocolDef.svc_particle:
                        Host.RenderContext.Particles.ParseParticleEffect(Host.Client.Cl.time, Host.Network.Reader);
                        break;

                    case ProtocolDef.svc_spawnbaseline:
                        i = Host.Network.Reader.ReadShort();
                        // must use CL_EntityNum() to force cl.num_entities up
                        ParseBaseline(EntityNum(i));
                        break;

                    case ProtocolDef.svc_spawnstatic:
                        ParseStatic();
                        break;

                    case ProtocolDef.svc_temp_entity:
                        ParseTempEntity();
                        break;

                    case ProtocolDef.svc_setpause:
                        {
                            Cl.paused = Host.Network.Reader.ReadByte() != 0;

                            if (Cl.paused)
                            {
                                Host.CDAudio.Pause();
                            }
                            else
                            {
                                Host.CDAudio.Resume();
                            }
                        }
                        break;

                    case ProtocolDef.svc_signonnum:
                        i = Host.Network.Reader.ReadByte();
                        if (i <= Cls.signon)
                        {
                            Host.Error("Received signon {0} when at {1}", i, Cls.signon);
                        }

                        Cls.signon = i;
                        SignonReply();
                        break;

                    case ProtocolDef.svc_killedmonster:
                        Cl.stats[QStatsDef.STAT_MONSTERS]++;
                        break;

                    case ProtocolDef.svc_foundsecret:
                        Cl.stats[QStatsDef.STAT_SECRETS]++;
                        break;

                    case ProtocolDef.svc_updatestat:
                        i = Host.Network.Reader.ReadByte();
                        if (i < 0 || i >= QStatsDef.MAX_CL_STATS)
                        {
                            Utilities.Error("svc_updatestat: {0} is invalid", i);
                        }

                        Cl.stats[i] = Host.Network.Reader.ReadLong();
                        break;

                    case ProtocolDef.svc_spawnstaticsound:
                        ParseStaticSound();
                        break;

                    case ProtocolDef.svc_cdtrack:
                        Cl.cdtrack = Host.Network.Reader.ReadByte();
                        Cl.looptrack = Host.Network.Reader.ReadByte();
                        if ((Cls.demoplayback || Cls.demorecording) && (Cls.forcetrack != -1))
                        {
                            Host.CDAudio.Play((byte)Cls.forcetrack, true);
                        }
                        else
                        {
                            Host.CDAudio.Play((byte)Cl.cdtrack, true);
                        }

                        break;

                    case ProtocolDef.svc_intermission:
                        Cl.intermission = 1;
                        Cl.completed_time = (int)Cl.time;
                        Host.Screen.VidDef.recalc_refdef = true;	// go to full screen
                        break;

                    case ProtocolDef.svc_finale:
                        Cl.intermission = 2;
                        Cl.completed_time = (int)Cl.time;
                        Host.Screen.VidDef.recalc_refdef = true;	// go to full screen
                        Host.Screen.CenterPrint(Host.Network.Reader.ReadString());
                        break;

                    case ProtocolDef.svc_cutscene:
                        Cl.intermission = 3;
                        Cl.completed_time = (int)Cl.time;
                        Host.Screen.VidDef.recalc_refdef = true;	// go to full screen
                        Host.Screen.CenterPrint(Host.Network.Reader.ReadString());
                        break;

                    case ProtocolDef.svc_sellscreen:
                        Host.Commands.ExecuteString("help", CommandSource.Command);
                        break;
                }
            }
        }

        private void ShowNet(string s)
        {
            if (Host.Cvars.ShowNet.Get<int>() == 2)
            {
                Host.Console.Print("{0,3}:{1}\n", Host.Network.Reader.Position - 1, s);
            }
        }

        /// <summary>
        /// CL_ParseUpdate
        ///
        /// Parse an entity update message from the server
        /// If an entities model or origin changes from frame to frame, it must be
        /// relinked.  Other attributes can change without relinking.
        /// </summary>
        private void ParseUpdate(int bits)
        {
            int i;

            if (Cls.signon == ClientDef.SIGNONS - 1)
            {
                // first update is the final signon stage
                Cls.signon = ClientDef.SIGNONS;
                SignonReply();
            }

            if ((bits & ProtocolDef.U_MOREBITS) != 0)
            {
                i = Host.Network.Reader.ReadByte();
                bits |= i << 8;
            }

            int num = (bits & ProtocolDef.U_LONGENTITY) != 0 ? Host.Network.Reader.ReadShort() : Host.Network.Reader.ReadByte();
            var ent = EntityNum(num);
            for (i = 0; i < 16; i++)
            {
                if ((bits & (1 << i)) != 0)
                {
                    _BitCounts[i]++;
                }
            }

            var forcelink = false;
            if (ent.msgtime != Cl.mtime[1])
            {
                forcelink = true;  // no previous frame to lerp from
            }

            ent.msgtime = Cl.mtime[0];
            int modnum;
            if ((bits & ProtocolDef.U_MODEL) != 0)
            {
                modnum = Host.Network.Reader.ReadByte();
                if (modnum >= QDef.MAX_MODELS)
                {
                    Host.Error("CL_ParseModel: bad modnum");
                }
            }
            else
            {
                modnum = ent.baseline.modelindex;
            }

            var model = Cl.model_precache[modnum];
            if (model != ent.model)
            {
                ent.model = model;
                // automatic animation (torches, etc) can be either all together
                // or randomized
                if (model != null)
                {
                    ent.syncbase = model.SyncType == SyncType.ST_RAND ? (float)(MathLib.Random() & 0x7fff) / 0x7fff : 0;
                }
                else
                {
                    forcelink = true;  // hack to make null model players work
                }

                if (num > 0 && num <= Cl.maxclients)
                {
                    Host.RenderContext.TranslatePlayerSkin(num - 1);
                }
            }

            ent.frame = (bits & ProtocolDef.U_FRAME) != 0 ? Host.Network.Reader.ReadByte() : ent.baseline.frame;

            i = (bits & ProtocolDef.U_COLORMAP) != 0 ? Host.Network.Reader.ReadByte() : ent.baseline.colormap;
            if (i == 0)
            {
                ent.colormap = Host.Screen.VidDef.colormap;
            }
            else
            {
                if (i > Cl.maxclients)
                {
                    Utilities.Error("i >= cl.maxclients");
                }

                ent.colormap = Cl.scores[i - 1].translations;
            }

            int skin = (bits & ProtocolDef.U_SKIN) != 0 ? Host.Network.Reader.ReadByte() : ent.baseline.skin;
            if (skin != ent.skinnum)
            {
                ent.skinnum = skin;
                if (num > 0 && num <= Cl.maxclients)
                {
                    Host.RenderContext.TranslatePlayerSkin(num - 1);
                }
            }

            ent.effects = (bits & ProtocolDef.U_EFFECTS) != 0 ? Host.Network.Reader.ReadByte() : ent.baseline.effects;

            // shift the known values for interpolation
            ent.msg_origins[1] = ent.msg_origins[0];
            ent.msg_angles[1] = ent.msg_angles[0];

            ent.msg_origins[0].X = (bits & ProtocolDef.U_ORIGIN1) != 0 ? Host.Network.Reader.ReadCoord() : ent.baseline.origin.x;
            ent.msg_angles[0].X = (bits & ProtocolDef.U_ANGLE1) != 0 ? Host.Network.Reader.ReadAngle() : ent.baseline.angles.x;

            ent.msg_origins[0].Y = (bits & ProtocolDef.U_ORIGIN2) != 0 ? Host.Network.Reader.ReadCoord() : ent.baseline.origin.y;
            ent.msg_angles[0].Y = (bits & ProtocolDef.U_ANGLE2) != 0 ? Host.Network.Reader.ReadAngle() : ent.baseline.angles.y;

            ent.msg_origins[0].Z = (bits & ProtocolDef.U_ORIGIN3) != 0 ? Host.Network.Reader.ReadCoord() : ent.baseline.origin.z;
            ent.msg_angles[0].Z = (bits & ProtocolDef.U_ANGLE3) != 0 ? Host.Network.Reader.ReadAngle() : ent.baseline.angles.z;

            if ((bits & ProtocolDef.U_NOLERP) != 0)
            {
                ent.forcelink = true;
            }

            if (forcelink)
            {	// didn't have an update last message
                ent.msg_origins[1] = ent.msg_origins[0];
                ent.origin = ent.msg_origins[0];
                ent.msg_angles[1] = ent.msg_angles[0];
                ent.angles = ent.msg_angles[0];
                ent.forcelink = true;
            }
        }

        /// <summary>
        /// CL_ParseClientdata
        /// Server information pertaining to this client only
        /// </summary>
        private void ParseClientData(int bits)
        {
            Cl.viewheight = (bits & ProtocolDef.SU_VIEWHEIGHT) != 0 ? Host.Network.Reader.ReadChar() : ProtocolDef.DEFAULT_VIEWHEIGHT;

            Cl.idealpitch = (bits & ProtocolDef.SU_IDEALPITCH) != 0 ? Host.Network.Reader.ReadChar() : 0;

            Cl.mvelocity[1] = Cl.mvelocity[0];
            for (var i = 0; i < 3; i++)
            {
                if ((bits & (ProtocolDef.SU_PUNCH1 << i)) != 0)
                {
                    MathLib.SetComp(ref Cl.punchangle, i, Host.Network.Reader.ReadChar());
                }
                else
                {
                    MathLib.SetComp(ref Cl.punchangle, i, 0);
                }

                if ((bits & (ProtocolDef.SU_VELOCITY1 << i)) != 0)
                {
                    MathLib.SetComp(ref Cl.mvelocity[0], i, Host.Network.Reader.ReadChar() * 16);
                }
                else
                {
                    MathLib.SetComp(ref Cl.mvelocity[0], i, 0);
                }
            }

            // [always sent]	if (bits & SU_ITEMS)
            var i2 = Host.Network.Reader.ReadLong();

            if (Cl.items != i2)
            {	// set flash times
                Host.Hud.Changed();
                for (var j = 0; j < 32; j++)
                {
                    if ((i2 & (1 << j)) != 0 && (Cl.items & (1 << j)) == 0)
                    {
                        Cl.item_gettime[j] = (float)Cl.time;
                    }
                }

                Cl.items = i2;
            }

            Cl.onground = (bits & ProtocolDef.SU_ONGROUND) != 0;
            Cl.inwater = (bits & ProtocolDef.SU_INWATER) != 0;

            Cl.stats[QStatsDef.STAT_WEAPONFRAME] = (bits & ProtocolDef.SU_WEAPONFRAME) != 0 ? Host.Network.Reader.ReadByte() : 0;

            i2 = (bits & ProtocolDef.SU_ARMOR) != 0 ? Host.Network.Reader.ReadByte() : 0;
            if (Cl.stats[QStatsDef.STAT_ARMOR] != i2)
            {
                Cl.stats[QStatsDef.STAT_ARMOR] = i2;
                Host.Hud.Changed();
            }

            i2 = (bits & ProtocolDef.SU_WEAPON) != 0 ? Host.Network.Reader.ReadByte() : 0;
            if (Cl.stats[QStatsDef.STAT_WEAPON] != i2)
            {
                Cl.stats[QStatsDef.STAT_WEAPON] = i2;
                Host.Hud.Changed();
            }

            i2 = Host.Network.Reader.ReadShort();
            if (Cl.stats[QStatsDef.STAT_HEALTH] != i2)
            {
                Cl.stats[QStatsDef.STAT_HEALTH] = i2;
                Host.Hud.Changed();
            }

            i2 = Host.Network.Reader.ReadByte();
            if (Cl.stats[QStatsDef.STAT_AMMO] != i2)
            {
                Cl.stats[QStatsDef.STAT_AMMO] = i2;
                Host.Hud.Changed();
            }

            for (i2 = 0; i2 < 4; i2++)
            {
                var j = Host.Network.Reader.ReadByte();
                if (Cl.stats[QStatsDef.STAT_SHELLS + i2] != j)
                {
                    Cl.stats[QStatsDef.STAT_SHELLS + i2] = j;
                    Host.Hud.Changed();
                }
            }

            i2 = Host.Network.Reader.ReadByte();

            // Change
            if (MainWindow.Common.GameKind == GameKind.StandardQuake)
            {
                if (Cl.stats[QStatsDef.STAT_ACTIVEWEAPON] != i2)
                {
                    Cl.stats[QStatsDef.STAT_ACTIVEWEAPON] = i2;
                    Host.Hud.Changed();
                }
            }
            else
            {
                if (Cl.stats[QStatsDef.STAT_ACTIVEWEAPON] != (1 << i2))
                {
                    Cl.stats[QStatsDef.STAT_ACTIVEWEAPON] = 1 << i2;
                    Host.Hud.Changed();
                }
            }
        }

        /// <summary>
        /// CL_ParseServerInfo
        /// </summary>
        private void ParseServerInfo()
        {
            Host.Console.DPrint("Serverinfo packet received.\n");

            //
            // wipe the client_state_t struct
            //
            ClearState();

            // parse protocol version number
            var i = Host.Network.Reader.ReadLong();
            if (i != ProtocolDef.PROTOCOL_VERSION)
            {
                Host.Console.Print("Server returned version {0}, not {1}", i, ProtocolDef.PROTOCOL_VERSION);
                return;
            }

            // parse maxclients
            Cl.maxclients = Host.Network.Reader.ReadByte();
            if (Cl.maxclients is < 1 or > QDef.MAX_SCOREBOARD)
            {
                Host.Console.Print("Bad maxclients ({0}) from server\n", Cl.maxclients);
                return;
            }
            Cl.scores = new Scoreboard[Cl.maxclients];// Hunk_AllocName (cl.maxclients*sizeof(*cl.scores), "scores");
            for (i = 0; i < Cl.scores.Length; i++)
            {
                Cl.scores[i] = new Scoreboard();
            }

            // parse gametype
            Cl.gametype = Host.Network.Reader.ReadByte();

            // parse signon message
            var str = Host.Network.Reader.ReadString();
            Cl.levelname = Utilities.Copy(str, 40);

            // seperate the printfs so the server message can have a color
            Host.Console.Print(ConsoleBar);
            Host.Console.Print("{0}{1}\n", (char)2, str);

            //
            // first we go through and touch all of the precache data that still
            // happens to be in the cache, so precaching something else doesn't
            // needlessly purge it
            //

            // precache models
            Array.Clear(Cl.model_precache, 0, Cl.model_precache.Length);
            int nummodels;
            var model_precache = new string[QDef.MAX_MODELS];
            for (nummodels = 1; ; nummodels++)
            {
                str = Host.Network.Reader.ReadString();
                if (string.IsNullOrEmpty(str))
                {
                    break;
                }

                if (nummodels == QDef.MAX_MODELS)
                {
                    Host.Console.Print("Server sent too many model precaches\n");
                    return;
                }
                model_precache[nummodels] = str;
                Host.Model.TouchModel(str);
            }

            // precache sounds
            Array.Clear(Cl.sound_precache, 0, Cl.sound_precache.Length);
            int numsounds;
            var sound_precache = new string[QDef.MAX_SOUNDS];
            for (numsounds = 1; ; numsounds++)
            {
                str = Host.Network.Reader.ReadString();
                if (string.IsNullOrEmpty(str))
                {
                    break;
                }

                if (numsounds == QDef.MAX_SOUNDS)
                {
                    Host.Console.Print("Server sent too many sound precaches\n");
                    return;
                }
                sound_precache[numsounds] = str;
                Host.Sound.TouchSound(str);
            }

            //
            // now we try to load everything else until a cache allocation fails
            //
            for (i = 1; i < nummodels; i++)
            {
                var name = model_precache[i];
                var n = name.ToLower();
                ModelType type = (n.StartsWith("*") && !n.Contains(".mdl")) || n.Contains(".bsp")
                    ? ModelType.mod_brush
                    : n.Contains(".mdl") ? ModelType.mod_alias : ModelType.mod_sprite;

                if (name == "progs/player.mdl")
                {

                }
                Cl.model_precache[i] = Host.Model.ForName(name, false, type);
                if (Cl.model_precache[i] == null)
                {
                    Host.Console.Print("Model {0} not found\n", name);
                    return;
                }
                KeepaliveMessage();
            }

            Sound.BeginPrecaching();
            for (i = 1; i < numsounds; i++)
            {
                Cl.sound_precache[i] = Host.Sound.PrecacheSound(sound_precache[i]);
                KeepaliveMessage();
            }
            Sound.EndPrecaching();

            // local state
            Cl.worldmodel = (BrushModelData)Cl.model_precache[1];
            Entities[0].model = Cl.model_precache[1];

            Host.RenderContext.NewMap();

            Host.NoClipAngleHack = false; // noclip is turned off at start

            GC.Collect();
        }

        // CL_ParseStartSoundPacket
        private void ParseStartSoundPacket()
        {
            var field_mask = Host.Network.Reader.ReadByte();
            int volume;
            float attenuation;

            volume = (field_mask & ProtocolDef.SND_VOLUME) != 0 ? Host.Network.Reader.ReadByte() : Sound.DEFAULT_SOUND_PACKET_VOLUME;

            attenuation = (field_mask & ProtocolDef.SND_ATTENUATION) != 0
                ? Host.Network.Reader.ReadByte() / 64.0f
                : Sound.DEFAULT_SOUND_PACKET_ATTENUATION;

            var channel = Host.Network.Reader.ReadShort();
            var sound_num = Host.Network.Reader.ReadByte();

            var ent = channel >> 3;
            channel &= 7;

            if (ent > QDef.MAX_EDICTS)
            {
                Host.Error("CL_ParseStartSoundPacket: ent = {0}", ent);
            }

            var pos = Host.Network.Reader.ReadCoords();
            Host.Sound.StartSound(ent, channel, Cl.sound_precache[sound_num], ref pos, volume / 255.0f, attenuation);
        }

        // CL_NewTranslation
        private void NewTranslation(int slot)
        {
            if (slot > Cl.maxclients)
            {
                Utilities.Error("CL_NewTranslation: slot > cl.maxclients");
            }

            var dest = Cl.scores[slot].translations;
            var source = Host.Screen.VidDef.colormap;
            Array.Copy(source, dest, dest.Length);

            var top = Cl.scores[slot].colors & 0xf0;
            var bottom = (Cl.scores[slot].colors & 15) << 4;

            Host.RenderContext.TranslatePlayerSkin(slot);

            for (int i = 0, offset = 0; i < Vid.VID_GRADES; i++)//, dest += 256, source+=256)
            {
                if (top < 128)  // the artists made some backwards ranges.  sigh.
                {
                    Buffer.BlockCopy(source, offset + top, dest, offset + Render.TOP_RANGE, 16);  //memcpy (dest + Render.TOP_RANGE, source + top, 16);
                }
                else
                {
                    for (var j = 0; j < 16; j++)
                    {
                        dest[offset + Render.TOP_RANGE + j] = source[offset + top + 15 - j];
                    }
                }

                if (bottom < 128)
                {
                    Buffer.BlockCopy(source, offset + bottom, dest, offset + Render.BOTTOM_RANGE, 16); // memcpy(dest + Render.BOTTOM_RANGE, source + bottom, 16);
                }
                else
                {
                    for (var j = 0; j < 16; j++)
                    {
                        dest[offset + Render.BOTTOM_RANGE + j] = source[offset + bottom + 15 - j];
                    }
                }

                offset += 256;
            }
        }

        /// <summary>
        /// CL_EntityNum
        ///
        /// This error checks and tracks the total number of entities
        /// </summary>
        /// <param name="num"></param>
        /// <returns></returns>
        private Entity EntityNum(int num)
        {
            if (num >= Cl.num_entities)
            {
                if (num >= QDef.MAX_EDICTS)
                {
                    Host.Error("CL_EntityNum: %i is an invalid number", num);
                }

                while (Cl.num_entities <= num)
                {
                    Entities[Cl.num_entities].colormap = Host.Screen.VidDef.colormap;
                    Cl.num_entities++;
                }
            }

            return Entities[num];
        }

        /// <summary>
        /// CL_ParseBaseline
        /// </summary>
        /// <param name="ent"></param>
        private void ParseBaseline(Entity ent)
        {
            ent.baseline.modelindex = Host.Network.Reader.ReadByte();
            ent.baseline.frame = Host.Network.Reader.ReadByte();
            ent.baseline.colormap = Host.Network.Reader.ReadByte();
            ent.baseline.skin = Host.Network.Reader.ReadByte();
            ent.baseline.origin.x = Host.Network.Reader.ReadCoord();
            ent.baseline.angles.x = Host.Network.Reader.ReadAngle();
            ent.baseline.origin.y = Host.Network.Reader.ReadCoord();
            ent.baseline.angles.y = Host.Network.Reader.ReadAngle();
            ent.baseline.origin.z = Host.Network.Reader.ReadCoord();
            ent.baseline.angles.z = Host.Network.Reader.ReadAngle();
        }

        /// <summary>
        /// CL_ParseStatic
        /// </summary>
        private void ParseStatic()
        {
            var i = Cl.num_statics;
            if (i >= ClientDef.MAX_STATIC_ENTITIES)
            {
                Host.Error("Too many static entities");
            }

            var ent = _StaticEntities[i];
            Cl.num_statics++;
            ParseBaseline(ent);

            // copy it to the current state
            ent.model = Cl.model_precache[ent.baseline.modelindex];
            ent.frame = ent.baseline.frame;
            ent.colormap = Host.Screen.VidDef.colormap;
            ent.skinnum = ent.baseline.skin;
            ent.effects = ent.baseline.effects;
            ent.origin = Utilities.ToVector(ref ent.baseline.origin);
            ent.angles = Utilities.ToVector(ref ent.baseline.angles);
            Host.RenderContext.AddEfrags(ent);
        }

        /// <summary>
        /// CL_ParseStaticSound
        /// </summary>
        private void ParseStaticSound()
        {
            var org = Host.Network.Reader.ReadCoords();
            var sound_num = Host.Network.Reader.ReadByte();
            var vol = Host.Network.Reader.ReadByte();
            var atten = Host.Network.Reader.ReadByte();

            Host.Sound.StaticSound(Cl.sound_precache[sound_num], ref org, vol, atten);
        }

        /// <summary>
        /// CL_KeepaliveMessage
        /// When the client is taking a long time to load stuff, send keepalive messages
        /// so the server doesn't disconnect.
        /// </summary>
        private void KeepaliveMessage()
        {
            if (Host.Server.IsActive)
            {
                return; // no need if server is local
            }

            if (Cls.demoplayback)
            {
                return;
            }

            // read messages from server, should just be nops
            Host.Network.Message.SaveState(ref _MsgState);

            int ret;
            do
            {
                ret = GetMessage();
                switch (ret)
                {
                    default:
                        Host.Error("CL_KeepaliveMessage: CL_GetMessage failed");
                        break;

                    case 0:
                        break;  // nothing waiting

                    case 1:
                        Host.Error("CL_KeepaliveMessage: received a message");
                        break;

                    case 2:
                        if (Host.Network.Reader.ReadByte() != ProtocolDef.svc_nop)
                        {
                            Host.Error("CL_KeepaliveMessage: datagram wasn't a nop");
                        }

                        break;
                }
            } while (ret != 0);

            Host.Network.Message.RestoreState(_MsgState);

            // check time
            var time = (float)Timer.GetFloatTime();
            if (time - _LastMsg < 5)
            {
                return;
            }

            _LastMsg = time;

            // write out a nop
            Host.Console.Print("--> client to server keepalive\n");

            Cls.message.WriteByte(ProtocolDef.clc_nop);
            Host.Network.SendMessage(Cls.netcon, Cls.message);
            Cls.message.Clear();
        }
    }
}
