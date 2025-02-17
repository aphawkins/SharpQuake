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

namespace SharpQuake
{
    using System;
    using SharpQuake.Framework.World;
    using SharpQuake.Framework;
    using SharpQuake.Game.Rendering;
    using SharpQuake.Game.World;
    using SharpQuake.Framework.IO;
    using System.Numerics;

    public partial class Client
    {
        // Instance
        public Host Host
        {
            get;
            private set;
        }

        // CL_Init
        public void Initialise()
        {
            InitInput(Host);
            InitTempEntities();

            if (Host.Cvars.Name == null)
            {
                Host.Cvars.Name = Host.CVars.Add("_cl_name", "player", ClientVariableFlags.Archive);
                Host.Cvars.Color = Host.CVars.Add("_cl_color", 0f, ClientVariableFlags.Archive);
                Host.Cvars.ShowNet = Host.CVars.Add("cl_shownet", 0);	// can be 0, 1, or 2
                Host.Cvars.NoLerp = Host.CVars.Add("cl_nolerp", false);
                Host.Cvars.LookSpring = Host.CVars.Add("lookspring", false, ClientVariableFlags.Archive);
                Host.Cvars.LookStrafe = Host.CVars.Add("lookstrafe", false, ClientVariableFlags.Archive);
                Host.Cvars.Sensitivity = Host.CVars.Add("sensitivity", 3f, ClientVariableFlags.Archive);
                Host.Cvars.MPitch = Host.CVars.Add("m_pitch", 0.022f, ClientVariableFlags.Archive);
                Host.Cvars.MYaw = Host.CVars.Add("m_yaw", 0.022f, ClientVariableFlags.Archive);
                Host.Cvars.MForward = Host.CVars.Add("m_forward", 1f, ClientVariableFlags.Archive);
                Host.Cvars.MSide = Host.CVars.Add("m_side", 0.8f, ClientVariableFlags.Archive);
                Host.Cvars.UpSpeed = Host.CVars.Add("cl_upspeed", 200f);
                Host.Cvars.ForwardSpeed = Host.CVars.Add("cl_forwardspeed", 200f, ClientVariableFlags.Archive);
                Host.Cvars.BackSpeed = Host.CVars.Add("cl_backspeed", 200f, ClientVariableFlags.Archive);
                Host.Cvars.SideSpeed = Host.CVars.Add("cl_sidespeed", 350f);
                Host.Cvars.MoveSpeedKey = Host.CVars.Add("cl_movespeedkey", 2.0f);
                Host.Cvars.YawSpeed = Host.CVars.Add("cl_yawspeed", 140f);
                Host.Cvars.PitchSpeed = Host.CVars.Add("cl_pitchspeed", 150f);
                Host.Cvars.AngleSpeedKey = Host.CVars.Add("cl_anglespeedkey", 1.5f);
                Host.Cvars.AnimationBlend = Host.CVars.Add("cl_animationblend", false);
            }

            for (var i = 0; i < _EFrags.Length; i++)
            {
                _EFrags[i] = new EFrag();
            }

            for (var i = 0; i < Entities.Length; i++)
            {
                Entities[i] = new Entity();
            }

            for (var i = 0; i < _StaticEntities.Length; i++)
            {
                _StaticEntities[i] = new Entity();
            }

            for (var i = 0; i < DLights.Length; i++)
            {
                DLights[i] = new DLight();
            }

            //
            // register our commands
            //
            Host.Commands.Add("cmd", ForwardToServer_f);
            Host.Commands.Add("entities", PrintEntities_f);
            Host.Commands.Add("disconnect", Disconnect_f);
            Host.Commands.Add("record", Record_f);
            Host.Commands.Add("stop", Stop_f);
            Host.Commands.Add("playdemo", PlayDemo_f);
            Host.Commands.Add("timedemo", TimeDemo_f);
        }

        // void	Cmd_ForwardToServer (void);
        // adds the current command line as a clc_stringcmd to the client message.
        // things like godmode, noclip, etc, are commands directed to the server,
        // so when they are typed in at the console, they will need to be forwarded.
        //
        // Sends the entire command line over to the server
        public void ForwardToServer_f(CommandMessage msg)
        {
            if (Host.Client.Cls.state != ClientActive.ca_connected)
            {
                Host.Console.Print($"Can't \"{msg.Name}\", not connected\n");
                return;
            }

            if (Host.Client.Cls.demoplayback)
            {
                return;     // not really connected
            }

            var writer = Host.Client.Cls.message;
            writer.WriteByte(ProtocolDef.clc_stringcmd);
            if (!msg.Name.Equals("cmd"))
            {
                writer.Print(msg.Name + " ");
            }
            if (msg.HasParameters)
            {
                writer.Print(msg.StringParameters);
            }
            else
            {
                writer.Print("\n");
            }
        }

        /// <summary>
        /// CL_EstablishConnection
        /// </summary>
        public void EstablishConnection(string host)
        {
            if (Cls.state == ClientActive.ca_dedicated)
            {
                return;
            }

            if (Cls.demoplayback)
            {
                return;
            }

            Disconnect();

            Cls.netcon = Host.Network.Connect(host);
            if (Cls.netcon == null)
            {
                Host.Error("CL_Connect: connect failed\n");
            }

            Host.Console.DPrint("CL_EstablishConnection: connected to {0}\n", host);

            Cls.demonum = -1;			// not in the demo loop now
            Cls.state = ClientActive.ca_connected;
            Cls.signon = 0;				// need all the signon messages before playing
        }

        /// <summary>
        /// CL_NextDemo
        ///
        /// Called to play the next demo in the demo loop
        /// </summary>
        public void NextDemo()
        {
            if (Cls.demonum == -1)
            {
                return;     // don't play demos
            }

            Host.Screen.BeginLoadingPlaque();

            if (string.IsNullOrEmpty(Cls.demos[Cls.demonum]) || Cls.demonum == ClientDef.MAX_DEMOS)
            {
                Cls.demonum = 0;
                if (string.IsNullOrEmpty(Cls.demos[Cls.demonum]))
                {
                    Host.Console.Print("No demos listed with startdemos\n");
                    Cls.demonum = -1;
                    return;
                }
            }

            Host.Commands.Buffer.Insert(string.Format("playdemo {0}\n", Cls.demos[Cls.demonum]));
            Cls.demonum++;
        }

        /// <summary>
        /// CL_AllocDlight
        /// </summary>
        public DLight AllocDlight(int key)
        {
            DLight dl;

            // first look for an exact key match
            if (key != 0)
            {
                for (var i = 0; i < ClientDef.MAX_DLIGHTS; i++)
                {
                    dl = DLights[i];
                    if (dl.key == key)
                    {
                        dl.Clear();
                        dl.key = key;
                        return dl;
                    }
                }
            }

            // then look for anything else
            //dl = cl_dlights;
            for (var i = 0; i < ClientDef.MAX_DLIGHTS; i++)
            {
                dl = DLights[i];
                if (dl.die < Cl.time)
                {
                    dl.Clear();
                    dl.key = key;
                    return dl;
                }
            }

            dl = DLights[0];
            dl.Clear();
            dl.key = key;
            return dl;
        }

        /// <summary>
        /// CL_DecayLights
        /// </summary>
        public void DecayLights()
        {
            var time = (float)(Cl.time - Cl.oldtime);

            for (var i = 0; i < ClientDef.MAX_DLIGHTS; i++)
            {
                var dl = DLights[i];
                if (dl.die < Cl.time || dl.radius == 0)
                {
                    continue;
                }

                dl.radius -= time * dl.decay;
                if (dl.radius < 0)
                {
                    dl.radius = 0;
                }
            }
        }

        // CL_Disconnect_f
        public void Disconnect_f(CommandMessage msg)
        {
            Disconnect();
            if (Host.Server.IsActive)
            {
                Host.ShutdownServer(false);
            }
        }

        // CL_SendCmd
        public void SendCmd()
        {
            if (Cls.state != ClientActive.ca_connected)
            {
                return;
            }

            if (Cls.signon == ClientDef.SIGNONS)
            {
                var cmd = new UserCommand();

                // get basic movement from keyboard
                BaseMove(ref cmd);

                // allow mice or other external controllers to add to the move
                MainWindow.Input.Move(cmd);

                // send the unreliable message
                Host.Client.SendMove(ref cmd);
            }

            if (Cls.demoplayback)
            {
                Cls.message.Clear();//    SZ_Clear (cls.message);
                return;
            }

            // send the reliable message
            if (Cls.message.IsEmpty)
            {
                return;     // no message at all
            }

            if (!Host.Network.CanSendMessage(Cls.netcon))
            {
                Host.Console.DPrint("CL_WriteToServer: can't send\n");
                return;
            }

            if (Host.Network.SendMessage(Cls.netcon, Cls.message) == -1)
            {
                Host.Error("CL_WriteToServer: lost server connection");
            }

            Cls.message.Clear();
        }

        // CL_ReadFromServer
        //
        // Read all incoming data from the server
        public int ReadFromServer()
        {
            Cl.oldtime = Cl.time;
            Cl.time += Host.FrameTime;

            int ret;
            do
            {
                ret = GetMessage();
                if (ret == -1)
                {
                    Host.Error("CL_ReadFromServer: lost server connection");
                }

                if (ret == 0)
                {
                    break;
                }

                Cl.last_received_message = (float)Host.RealTime;
                ParseServerMessage();
            } while (ret != 0 && Cls.state == ClientActive.ca_connected);

            if (Host.Cvars.ShowNet.Get<int>() != 0)
            {
                Host.Console.Print("\n");
            }

            //
            // bring the links up to date
            //
            RelinkEntities();
            UpdateTempEntities();

            return 0;
        }

        /// <summary>
        /// CL_Disconnect
        ///
        /// Sends a disconnect message to the server
        /// This is also called on Host_Error, so it shouldn't cause any errors
        /// </summary>
        public void Disconnect()
        {
            // stop sounds (especially looping!)
            Host.Sound.StopAllSounds(true);

            // bring the console down and fade the colors back to normal
            //	SCR_BringDownConsole ();

            // if running a local server, shut it down
            if (Cls.demoplayback)
            {
                StopPlayback();
            }
            else if (Cls.state == ClientActive.ca_connected)
            {
                if (Cls.demorecording)
                {
                    Stop_f(null);
                }

                Host.Console.DPrint("Sending clc_disconnect\n");
                Cls.message.Clear();
                Cls.message.WriteByte(ProtocolDef.clc_disconnect);
                Host.Network.SendUnreliableMessage(Cls.netcon, Cls.message);
                Cls.message.Clear();
                Host.Network.Close(Cls.netcon);

                Cls.state = ClientActive.ca_disconnected;
                if (Host.Server.NetServer.active)
                {
                    Host.ShutdownServer(false);
                }
            }

            Cls.demoplayback = Cls.timedemo = false;
            Cls.signon = 0;
        }

        // CL_PrintEntities_f
        private void PrintEntities_f(CommandMessage msg)
        {
            for (var i = 0; i < Cl.num_entities; i++)
            {
                var ent = Entities[i];
                Host.Console.Print("{0:d3}:", i);
                if (ent.model == null)
                {
                    Host.Console.Print("EMPTY\n");
                    continue;
                }
                Host.Console.Print("{0}:{1:d2}  ({2}) [{3}]\n", ent.model.Name, ent.frame, ent.origin, ent.angles);
            }
        }

        /// <summary>
        /// CL_RelinkEntities
        /// </summary>
        private void RelinkEntities()
        {
            // determine partial update time
            var frac = LerpPoint();

            NumVisEdicts = 0;

            //
            // interpolate player info
            //
            Cl.velocity = Cl.mvelocity[1] + (frac * (Cl.mvelocity[0] - Cl.mvelocity[1]));

            if (Cls.demoplayback)
            {
                // interpolate the angles
                var angleDelta = Cl.mviewangles[0] - Cl.mviewangles[1];
                MathLib.CorrectAngles180(ref angleDelta);
                Cl.viewangles = Cl.mviewangles[1] + (frac * angleDelta);
            }

            var bobjrotate = MathLib.AngleMod(100 * Cl.time);

            // start on the entity after the world
            for (var i = 1; i < Cl.num_entities; i++)
            {
                var ent = Entities[i];
                if (ent.model == null)
                {
                    // empty slot
                    if (ent.forcelink)
                    {
                        Host.RenderContext.RemoveEfrags(ent);  // just became empty
                    }

                    continue;
                }

                // if the object wasn't included in the last packet, remove it
                if (ent.msgtime != Cl.mtime[0])
                {
                    ent.model = null;
                    continue;
                }

                var oldorg = ent.origin;

                if (ent.forcelink)
                {
                    // the entity was not updated in the last message
                    // so move to the final spot
                    ent.origin = ent.msg_origins[0];
                    ent.angles = ent.msg_angles[0];
                }
                else
                {
                    // if the delta is large, assume a teleport and don't lerp
                    var f = frac;
                    var delta = ent.msg_origins[0] - ent.msg_origins[1];
                    if (Math.Abs(delta.X) > 100 || Math.Abs(delta.Y) > 100 || Math.Abs(delta.Z) > 100)
                    {
                        f = 1; // assume a teleportation, not a motion
                    }

                    // interpolate the origin and angles
                    ent.origin = ent.msg_origins[1] + (f * delta);
                    var angleDelta = ent.msg_angles[0] - ent.msg_angles[1];
                    MathLib.CorrectAngles180(ref angleDelta);
                    ent.angles = ent.msg_angles[1] + (f * angleDelta);
                }

                // rotate binary objects locally
                if (ent.model.Flags.HasFlag(EntityFlags.Rotate))
                {
                    ent.angles.Y = bobjrotate;
                }

                if ((ent.effects & EntityEffects.EF_BRIGHTFIELD) != 0)
                {
                    Host.RenderContext.Particles.EntityParticles(Host.Client.Cl.time, ent.origin);
                }

                if ((ent.effects & EntityEffects.EF_MUZZLEFLASH) != 0)
                {
                    var dl = AllocDlight(i);
                    dl.origin = ent.origin;
                    dl.origin.Z += 16;
                    MathLib.AngleVectors(ref ent.angles, out Vector3 forward, out _, out _);
                    dl.origin += forward * 18;
                    dl.radius = 200 + (MathLib.Random() & 31);
                    dl.minlight = 32;
                    dl.die = (float)Cl.time + 0.1f;
                }
                if ((ent.effects & EntityEffects.EF_BRIGHTLIGHT) != 0)
                {
                    var dl = AllocDlight(i);
                    dl.origin = ent.origin;
                    dl.origin.Z += 16;
                    dl.radius = 400 + (MathLib.Random() & 31);
                    dl.die = (float)Cl.time + 0.001f;
                }
                if ((ent.effects & EntityEffects.EF_DIMLIGHT) != 0)
                {
                    var dl = AllocDlight(i);
                    dl.origin = ent.origin;
                    dl.radius = 200 + (MathLib.Random() & 31);
                    dl.die = (float)Cl.time + 0.001f;
                }

                if (ent.model.Flags.HasFlag(EntityFlags.Gib))
                {
                    Host.RenderContext.Particles.RocketTrail(Host.Client.Cl.time, ref oldorg, ref ent.origin, 2);
                }
                else if (ent.model.Flags.HasFlag(EntityFlags.ZomGib))
                {
                    Host.RenderContext.Particles.RocketTrail(Host.Client.Cl.time, ref oldorg, ref ent.origin, 4);
                }
                else if (ent.model.Flags.HasFlag(EntityFlags.Tracer))
                {
                    Host.RenderContext.Particles.RocketTrail(Host.Client.Cl.time, ref oldorg, ref ent.origin, 3);
                }
                else if (ent.model.Flags.HasFlag(EntityFlags.Tracer2))
                {
                    Host.RenderContext.Particles.RocketTrail(Host.Client.Cl.time, ref oldorg, ref ent.origin, 5);
                }
                else if (ent.model.Flags.HasFlag(EntityFlags.Rocket))
                {
                    Host.RenderContext.Particles.RocketTrail(Host.Client.Cl.time, ref oldorg, ref ent.origin, 0);
                    var dl = AllocDlight(i);
                    dl.origin = ent.origin;
                    dl.radius = 200;
                    dl.die = (float)Cl.time + 0.01f;
                }
                else if (ent.model.Flags.HasFlag(EntityFlags.Grenade))
                {
                    Host.RenderContext.Particles.RocketTrail(Host.Client.Cl.time, ref oldorg, ref ent.origin, 1);
                }
                else if (ent.model.Flags.HasFlag(EntityFlags.Tracer3))
                {
                    Host.RenderContext.Particles.RocketTrail(Host.Client.Cl.time, ref oldorg, ref ent.origin, 6);
                }

                ent.forcelink = false;

                if (i == Cl.viewentity && !Host.ChaseView.IsActive)
                {
                    continue;
                }

                if (NumVisEdicts < ClientDef.MAX_VISEDICTS)
                {
                    VisEdicts[NumVisEdicts] = ent;
                    NumVisEdicts++;
                }
            }
        }

        /// <summary>
        /// CL_SignonReply
        ///
        /// An svc_signonnum has been received, perform a client side setup
        /// </summary>
        private void SignonReply()
        {
            Host.Console.DPrint("CL_SignonReply: {0}\n", Cls.signon);

            switch (Cls.signon)
            {
                case 1:
                    Cls.message.WriteByte(ProtocolDef.clc_stringcmd);
                    Cls.message.WriteString("prespawn");
                    break;

                case 2:
                    Cls.message.WriteByte(ProtocolDef.clc_stringcmd);
                    Cls.message.WriteString(string.Format("name \"{0}\"\n", Host.Cvars.Name.Get<string>()));

                    Cls.message.WriteByte(ProtocolDef.clc_stringcmd);
                    Cls.message.WriteString(string.Format("color {0} {1}\n", ((int)Host.Cvars.Color.Get<float>()) >> 4, ((int)Host.Cvars.Color.Get<float>()) & 15));

                    Cls.message.WriteByte(ProtocolDef.clc_stringcmd);
                    Cls.message.WriteString("spawn " + Cls.spawnparms);
                    break;

                case 3:
                    Cls.message.WriteByte(ProtocolDef.clc_stringcmd);
                    Cls.message.WriteString("begin");
                    Host.Cache.Report();	// print remaining memory
                    break;

                case 4:
                    Host.Screen.EndLoadingPlaque();		// allow normal screen updates
                    break;
            }
        }

        /// <summary>  
        /// CL_ClearState
        /// </summary>
        private void ClearState()
        {
            if (!Host.Server.NetServer.active)
            {
                Host.ClearMemory();
            }

            // wipe the entire cl structure
            Cl.Clear();

            Cls.message.Clear();

            // clear other arrays
            foreach (var ef in _EFrags)
            {
                ef.Clear();
            }

            foreach (var et in Entities)
            {
                et.Clear();
            }

            foreach (var dl in DLights)
            {
                dl.Clear();
            }

            Array.Clear(LightStyle, 0, LightStyle.Length);

            foreach (var et in _TempEntities)
            {
                et.Clear();
            }

            foreach (var b in _Beams)
            {
                b.Clear();
            }

            //
            // allocate the efrags and chain together into a free list
            //
            Cl.free_efrags = _EFrags[0];// cl_efrags;
            for (var i = 0; i < ClientDef.MAX_EFRAGS - 1; i++)
            {
                _EFrags[i].entnext = _EFrags[i + 1];
            }

            _EFrags[ClientDef.MAX_EFRAGS - 1].entnext = null;
        }

        /// <summary>
        /// CL_LerpPoint
        /// Determines the fraction between the last two messages that the objects
        /// should be put at.
        /// </summary>
        private float LerpPoint()
        {
            var f = Cl.mtime[0] - Cl.mtime[1];
            if (f == 0 || Host.Cvars.NoLerp.Get<bool>() || Cls.timedemo || Host.Server.IsActive)
            {
                Cl.time = Cl.mtime[0];
                return 1;
            }

            if (f > 0.1)
            {	// dropped packet, or start of demo
                Cl.mtime[1] = Cl.mtime[0] - 0.1;
                f = 0.1;
            }
            var frac = (Cl.time - Cl.mtime[1]) / f;
            if (frac < 0)
            {
                if (frac < -0.01)
                {
                    Cl.time = Cl.mtime[1];
                }
                frac = 0;
            }
            else if (frac > 1)
            {
                if (frac > 1.01)
                {
                    Cl.time = Cl.mtime[0];
                }
                frac = 1;
            }
            return (float)frac;
        }
    }
}
