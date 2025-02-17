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
    using System.IO;
    using System.Text;
    using SharpQuake.Framework;
    using SharpQuake.Framework.IO;

    public partial class Client
    {
        /// <summary>
        /// CL_StopPlayback
        ///
        /// Called when a demo file runs out, or the user starts a game
        /// </summary>
        public void StopPlayback()
        {
            if (!Cls.demoplayback)
            {
                return;
            }

            if (Cls.demofile != null)
            {
                Cls.demofile.Dispose();
                Cls.demofile = null;
            }
            Cls.demoplayback = false;
            Cls.state = ClientActive.ca_disconnected;

            if (Cls.timedemo)
            {
                FinishTimeDemo();
            }
        }

        /// <summary>
        /// CL_Record_f
        /// record <demoname> <map> [cd track]
        /// </summary>
        private void Record_f(CommandMessage msg)
        {
            if (msg.Source != CommandSource.Command)
            {
                return;
            }

            var c = msg.Parameters != null ? msg.Parameters.Length : 0;

            if (c is not 1 and not 2 and not 3)
            {
                Host.Console.Print("record <demoname> [<map> [cd track]]\n");
                return;
            }

            if (msg.Parameters[0].Contains(".."))
            {
                Host.Console.Print("Relative pathnames are not allowed.\n");
                return;
            }

            if (c == 2 && Cls.state == ClientActive.ca_connected)
            {
                Host.Console.Print("Can not record - already connected to server\nClient demo recording must be started before connecting\n");
                return;
            }

            // write the forced cd track number, or -1
            int track;
            if (c == 3)
            {
                track = MathLib.AToI(msg.Parameters[2]);
                Host.Console.Print("Forcing CD track to {0}\n", track);
            }
            else
            {
                track = -1;
            }

            var name = Path.Combine(FileSystem.GameDir, msg.Parameters[0]);

            //
            // start the map up
            //
            if (c > 1)
            {
                Host.Commands.ExecuteString(string.Format("map {0}", msg.Parameters[1]), CommandSource.Command);
            }

            //
            // open the demo file
            //
            name = Path.ChangeExtension(name, ".dem");

            Host.Console.Print("recording to {0}.\n", name);
            var fs = FileSystem.OpenWrite(name, true);
            if (fs == null)
            {
                Host.Console.Print("ERROR: couldn't open.\n");
                return;
            }
            var writer = new BinaryWriter(fs, Encoding.ASCII);
            Cls.demofile = new DisposableWrapper<BinaryWriter>(writer, true);
            Cls.forcetrack = track;
            var tmp = Encoding.ASCII.GetBytes(Cls.forcetrack.ToString());
            writer.Write(tmp);
            writer.Write('\n');
            Cls.demorecording = true;
        }

        /// <summary>
        /// CL_Stop_f
        /// stop recording a demo
        /// </summary>
        private void Stop_f(CommandMessage msg)
        {
            if (msg.Source != CommandSource.Command)
            {
                return;
            }

            if (!Cls.demorecording)
            {
                Host.Console.Print("Not recording a demo.\n");
                return;
            }

            // write a disconnect message to the demo file
            Host.Network.Message.Clear();
            Host.Network.Message.WriteByte(ProtocolDef.svc_disconnect);
            WriteDemoMessage();

            // finish up
            if (Cls.demofile != null)
            {
                Cls.demofile.Dispose();
                Cls.demofile = null;
            }
            Cls.demorecording = false;
            Host.Console.Print("Completed demo\n");
        }

        // CL_PlayDemo_f
        //
        // play [demoname]
        private void PlayDemo_f(CommandMessage msg)
        {
            if (msg.Source != CommandSource.Command)
            {
                return;
            }

            var c = msg.Parameters != null ? msg.Parameters.Length : 0;

            if (c != 1)
            {
                Host.Console.Print("play <demoname> : plays a demo\n");
                return;
            }

            //
            // disconnect from server
            //
            Disconnect();

            //
            // open the demo file
            //
            var name = Path.ChangeExtension(msg.Parameters[0], ".dem");

            Host.Console.Print("Playing demo from {0}.\n", name);
            if (Cls.demofile != null)
            {
                Cls.demofile.Dispose();
            }
            FileSystem.FOpenFile(name, out DisposableWrapper<BinaryReader> reader);
            Cls.demofile = reader;
            if (Cls.demofile == null)
            {
                Host.Console.Print("ERROR: couldn't open.\n");
                Cls.demonum = -1;		// stop demo loop
                return;
            }

            Cls.demoplayback = true;
            Cls.state = ClientActive.ca_connected;
            Cls.forcetrack = 0;

            var s = reader.Object;
            var neg = false;
            while (true)
            {
                c = s.ReadByte();
                if (c == '\n')
                {
                    break;
                }

                if (c == '-')
                {
                    neg = true;
                }
                else
                {
                    Cls.forcetrack = (Cls.forcetrack * 10) + (c - '0');
                }
            }

            if (neg)
            {
                Cls.forcetrack = -Cls.forcetrack;
            }
            // ZOID, fscanf is evil
            //	fscanf (cls.demofile, "%i\n", &cls.forcetrack);
        }

        /// <summary>
        /// CL_TimeDemo_f
        /// timedemo [demoname]
        /// </summary>
        private void TimeDemo_f(CommandMessage msg)
        {
            if (msg.Source != CommandSource.Command)
            {
                return;
            }

            var c = msg.Parameters != null ? msg.Parameters.Length : 0;

            if (c != 1)
            {
                Host.Console.Print("timedemo <demoname> : gets demo speeds\n");
                return;
            }

            PlayDemo_f(msg);

            // cls.td_starttime will be grabbed at the second frame of the demo, so
            // all the loading time doesn't get counted
            Cls.timedemo = true;
            Cls.td_startframe = Host.FrameCount;
            Cls.td_lastframe = -1;		// get a new message this frame
        }

        /// <summary>
        /// CL_GetMessage
        /// Handles recording and playback of demos, on top of NET_ code
        /// </summary>
        /// <returns></returns>
        private int GetMessage()
        {
            if (Cls.demoplayback)
            {
                // decide if it is time to grab the next message
                if (Cls.signon == ClientDef.SIGNONS)	// allways grab until fully connected
                {
                    if (Cls.timedemo)
                    {
                        if (Host.FrameCount == Cls.td_lastframe)
                        {
                            return 0;      // allready read this frame's message
                        }

                        Cls.td_lastframe = Host.FrameCount;
                        // if this is the second frame, grab the real td_starttime
                        // so the bogus time on the first frame doesn't count
                        if (Host.FrameCount == Cls.td_startframe + 1)
                        {
                            Cls.td_starttime = (float)Host.RealTime;
                        }
                    }
                    else if (Cl.time <= Cl.mtime[0])
                    {
                        return 0;	// don't need another message yet
                    }
                }

                // get the next message
                var reader = ((DisposableWrapper<BinaryReader>)Cls.demofile).Object;
                var size = EndianHelper.LittleLong(reader.ReadInt32());
                if (size > QDef.MAX_MSGLEN)
                {
                    Utilities.Error("Demo message > MAX_MSGLEN");
                }

                Cl.mviewangles[1] = Cl.mviewangles[0];
                Cl.mviewangles[0].X = EndianHelper.LittleFloat(reader.ReadSingle());
                Cl.mviewangles[0].Y = EndianHelper.LittleFloat(reader.ReadSingle());
                Cl.mviewangles[0].Z = EndianHelper.LittleFloat(reader.ReadSingle());

                Host.Network.Message.FillFrom(reader.BaseStream, size);
                if (Host.Network.Message.Length < size)
                {
                    StopPlayback();
                    return 0;
                }
                return 1;
            }

            int r;
            while (true)
            {
                r = Host.Network.GetMessage(Cls.netcon);

                if (r is not 1 and not 2)
                {
                    return r;
                }

                // discard nop keepalive message
                if (Host.Network.Message.Length == 1 && Host.Network.Message.Data[0] == ProtocolDef.svc_nop)
                {
                    Host.Console.Print("<-- server to client keepalive\n");
                }
                else
                {
                    break;
                }
            }

            if (Cls.demorecording)
            {
                WriteDemoMessage();
            }

            return r;
        }

        /// <summary>
        /// CL_FinishTimeDemo
        /// </summary>
        private void FinishTimeDemo()
        {
            Cls.timedemo = false;

            // the first frame didn't count
            var frames = Host.FrameCount - Cls.td_startframe - 1;
            var time = (float)Host.RealTime - Cls.td_starttime;
            if (time == 0)
            {
                time = 1;
            }

            Host.Console.Print("{0} frames {1:F5} seconds {2:F2} fps\n", frames, time, frames / time);
        }

        /// <summary>
        /// CL_WriteDemoMessage
        /// Dumps the current net message, prefixed by the length and view angles
        /// </summary>
        private void WriteDemoMessage()
        {
            var len = EndianHelper.LittleLong(Host.Network.Message.Length);
            var writer = ((DisposableWrapper<BinaryWriter>)Cls.demofile).Object;
            writer.Write(len);
            writer.Write(EndianHelper.LittleFloat(Cl.viewangles.X));
            writer.Write(EndianHelper.LittleFloat(Cl.viewangles.Y));
            writer.Write(EndianHelper.LittleFloat(Cl.viewangles.Z));
            writer.Write(Host.Network.Message.Data, 0, Host.Network.Message.Length);
            writer.Flush();
        }
    }
}
