﻿/// <copyright>
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
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using SharpQuake.Framework;
    using SharpQuake.Framework.Factories.IO;
    using SharpQuake.Framework.IO;
    using SharpQuake.Framework.IO.Input;
    using SharpQuake.Rendering.UI;
    using SharpQuake.Sys;

    public partial class Host : MasterFactory
    {
        private bool _disposedValue;

        public QuakeParameters Parameters
        {
            get;
            private set;
        }

        public bool IsDedicated
        {
            get;
            private set;
        }

        public bool IsInitialised
        {
            get;
            private set;
        }

        public double Time
        {
            get;
            private set;
        }

        public int FrameCount
        {
            get;
            private set;
        }

        public bool IsDeveloper
        {
            get;
            private set;
        }

        public byte[] ColorMap
        {
            get;
            private set;
        }

        public byte[] BasePal
        {
            get;
            private set;
        }

        public int ClientNum => Array.IndexOf(Server.ServerStatic.clients, HostClient);

        public double RealTime
        {
            get;
            private set;
        }

        public double FrameTime
        {
            get;
            set;
        }

        public BinaryReader VcrReader
        {
            get;
            private set;
        }

        public BinaryWriter VcrWriter
        {
            get;
            private set;
        }

        public int CurrentSkill
        {
            get;
            set;
        }

        public bool NoClipAngleHack
        {
            get;
            set;
        }

        public bool IsDisposing
        {
            get;
            private set;
        }

        // Instances
        public MainWindow MainWindow
        {
            get;
            private set;
        }

        public FrameworkClient HostClient
        {
            get;
            set;
        }

        public Cache Cache
        {
            get;
            private set;
        }

        //private CommandBuffer CommandBuffer
        //{
        //    get;
        //    set;
        //}

        //private Command Command
        //{
        //    get;
        //    set;
        //}

        public View View
        {
            get;
            private set;
        }

        public ChaseView ChaseView
        {
            get;
            private set;
        }

        public Wad GfxWad
        {
            get;
            private set;
        }

        public Dictionary<string, Wad> WadFiles
        {
            get;
            private set;
        }

        public Dictionary<string, string> WadTextures
        {
            get;
            private set;
        }

        public Keyboard Keyboard
        {
            get;
            private set;
        }

        public Con Console
        {
            get;
            private set;
        }

        public Menu Menu
        {
            get;
            private set;
        }

        public Programs Programs
        {
            get;
            private set;
        }

        public ProgramsBuiltIn ProgramsBuiltIn
        {
            get;
            private set;
        }

        public Mod Model
        {
            get;
            private set;
        }

        public Network Network
        {
            get;
            private set;
        }

        public Server Server
        {
            get;
            private set;
        }

        public Client Client
        {
            get;
            private set;
        }

        public Vid Video
        {
            get;
            private set;
        }

        public Drawer DrawingContext
        {
            get;
            private set;
        }

        public Scr Screen
        {
            get;
            private set;
        }

        public Render RenderContext
        {
            get;
            private set;
        }

        public Sound Sound
        {
            get;
            private set;
        }

        public CdAudio CDAudio
        {
            get;
            private set;
        }

        public Hud Hud
        {
            get;
            private set;
        }

        public DedicatedServer DedicatedServer
        {
            get;
            private set;
        }

        // Factories
        public ClientVariableFactory CVars
        {
            get;
            private set;
        }

        public CommandFactory Commands
        {
            get;
            private set;
        }

        // CVars
        public Cvars Cvars
        {
            get;
            private set;
        }

        private double _TimeTotal; // static double timetotal from Host_Frame
        private int _TimeCount; // static int timecount from Host_Frame
        private double _OldRealTime; //double oldrealtime;	
        private double _Time1 = 0; // static double time1 from _Host_Frame
        private double _Time2 = 0; // static double time2 from _Host_Frame
        private double _Time3 = 0; // static double time3 from _Host_Frame

        private static int _ShutdownDepth;
        private static int _ErrorDepth;

        public Host(MainWindow window)
        {
            MainWindow = window;
            Cvars = new Cvars();

            // Factories
            Commands = AddFactory<CommandFactory>();
            CVars = AddFactory<ClientVariableFactory>();

            Commands.Initialise(CVars);

            // Old
            Cache = new Cache();
            //CommandBuffer = new CommandBuffer( this );
            //Command = new Command( this );
            //CVar.Initialise( Command );
            View = new View(this);
            ChaseView = new ChaseView(this);
            GfxWad = new Wad();
            Keyboard = new Keyboard(this);
            Console = new Con(this);
            Menu = new Menu(this);
            Programs = new Programs(this);
            ProgramsBuiltIn = new ProgramsBuiltIn(this);
            Model = new Mod(this);
            Network = new Network(this);
            Server = new Server(this);
            Client = new Client(this);
            Video = new Vid(this);
            DrawingContext = new Drawer(this);
            Screen = new Scr(this);
            RenderContext = new Render(this);
            Sound = new Sound(this);
            CDAudio = new CdAudio(this);
            Hud = new Hud(this);
            DedicatedServer = new DedicatedServer();

            WadFiles = new Dictionary<string, Wad>();
            WadTextures = new Dictionary<string, string>();
        }

        /// <summary>
        /// Host_ServerFrame
        /// </summary>
        public void ServerFrame()
        {
            // run the world state
            Programs.GlobalStruct.frametime = (float)FrameTime;

            // set the time and clear the general datagram
            Server.ClearDatagram();

            // check for new clients
            Server.CheckForNewClients();

            // read client messages
            Server.RunClients();

            // move things around and think
            // always pause in single player if in console or menus
            if (!Server.NetServer.paused && (Server.ServerStatic.maxclients > 1 || Keyboard.Destination == KeyDestination.key_game))
            {
                Server.Physics();
            }

            // send all messages to the clients
            Server.SendClientMessages();
        }

        /// <summary>
        /// host_old_ClearMemory
        /// </summary>
        public void ClearMemory()
        {
            Console.DPrint("Clearing memory\n");

            Model.ClearAll();
            Client.Cls.signon = 0;
            Server.NetServer.Clear();
            Client.Cl.Clear();
        }

        /// <summary>
        /// host_Error
        /// This shuts down both the client and server
        /// </summary>
        public void Error(string error, params object[] args)
        {
            _ErrorDepth++;
            try
            {
                if (_ErrorDepth > 1)
                {
                    Utilities.Error("host_Error: recursively entered. " + error, args);
                }

                Screen.EndLoadingPlaque();		// reenable screen updates

                var message = args.Length > 0 ? string.Format(error, args) : error;
                Console.Print("host_Error: {0}\n", message);

                if (Server.NetServer.active)
                {
                    ShutdownServer(false);
                }

                if (Client.Cls.state == ClientActive.ca_dedicated)
                {
                    Utilities.Error("host_Error: {0}\n", message); // dedicated servers exit
                }

                Client.Disconnect();
                Client.Cls.demonum = -1;

                throw new EndGameException(); // longjmp (host_old_abortserver, 1);
            }
            finally
            {
                _ErrorDepth--;
            }
        }

        public void Initialise(QuakeParameters parms)
        {
            Parameters = parms;

            //Command.SetupWrapper( ); // Temporary workaround - change soon!
            Cache.Initialise(1024 * 1024 * 512); // debug

            Commands.Add("flush", Cache.Flush);

            //CommandBuffer.Initialise( );
            // Command.Initialise( );
            View.Initialise();
            ChaseView.Initialise();
            InitialiseVCR(parms);
            MainWindow.Common.Initialise(MainWindow, parms.argv);
            InitialiseLocal();

            // Search wads
            foreach (var wadFile in FileSystem.Search("*.wad"))
            {
                if (wadFile == "radiant.wad")
                {
                    continue;
                }

                if (wadFile == "gfx.wad")
                {
                    continue;
                }

                var data = FileSystem.LoadFile(wadFile);

                if (data == null)
                {
                    continue;
                }

                var wad = new Wad();
                wad.LoadWadFile(wadFile, data);

                WadFiles.Add(wadFile, wad);

                var textures = wad.Lumps.Values
                    .Select(s => Encoding.ASCII.GetString(s.name).Replace("\0", ""))
                    .ToArray();

                foreach (var texture in textures)
                {
                    if (!WadTextures.ContainsKey(texture))
                    {
                        WadTextures.Add(texture, wadFile);
                    }
                }
            }

            GfxWad.LoadWadFile("gfx.wad");
            Keyboard.Initialise();
            Console.Initialise();
            Menu.Initialise();
            Programs.Initialise();
            ProgramsBuiltIn.Initialise();
            Model.Initialise();
            Network.Initialise();
            Server.Initialise();

            //Con.Print("Exe: "__TIME__" "__DATE__"\n");
            //Con.Print("%4.1f megabyte heap\n",parms->memsize/ (1024*1024.0));

            RenderContext.InitTextures();		// needed even for dedicated servers

            if (Client.Cls.state != ClientActive.ca_dedicated)
            {
                BasePal = FileSystem.LoadFile("gfx/palette.lmp");
                if (BasePal == null)
                {
                    Utilities.Error("Couldn't load gfx/palette.lmp");
                }

                ColorMap = FileSystem.LoadFile("gfx/colormap.lmp");
                if (ColorMap == null)
                {
                    Utilities.Error("Couldn't load gfx/colormap.lmp");
                }

                // on non win32, mouse comes before video for security reasons
                MainWindow.Input.Initialise(this);
                Video.Initialise(BasePal);
                DrawingContext.Initialise();
                Screen.Initialise();
                RenderContext.Initialise();
                Sound.Initialise();
                CDAudio.Initialise();
                Hud.Initialise();
                Client.Initialise();
            }
            else
            {
                DedicatedServer.Initialise();
            }

            Commands.Buffer.Insert("exec quake.rc\n");

            IsInitialised = true;

            Console.DPrint("========Quake Initialized=========\n");
        }

        /// <summary>
        /// host_ClientCommands
        /// Send text over to the client to be executed
        /// </summary>
        public void ClientCommands(string fmt, params object[] args)
        {
            var tmp = string.Format(fmt, args);
            HostClient.message.WriteByte(ProtocolDef.svc_stufftext);
            HostClient.message.WriteString(tmp);
        }

        // Host_InitLocal
        private void InitialiseLocal()
        {
            InititaliseCommands();

            if (Cvars.SystemTickRate == null)
            {
                Cvars.SystemTickRate = CVars.Add("sys_ticrate", 0.05);
                Cvars.Developer = CVars.Add("developer", false);
                Cvars.FrameRate = CVars.Add("host_framerate", 0.0); // set for slow motion
                Cvars.HostSpeeds = CVars.Add("host_speeds", false); // set for running times
                Cvars.ServerProfile = CVars.Add("serverprofile", false);
                Cvars.FragLimit = CVars.Add("fraglimit", 0, ClientVariableFlags.Server);
                Cvars.TimeLimit = CVars.Add("timelimit", 0, ClientVariableFlags.Server);
                Cvars.TeamPlay = CVars.Add("teamplay", 0, ClientVariableFlags.Server);
                Cvars.SameLevel = CVars.Add("samelevel", false);
                Cvars.NoExit = CVars.Add("noexit", false, ClientVariableFlags.Server);
                Cvars.Skill = CVars.Add("skill", 1); // 0 - 3
                Cvars.Deathmatch = CVars.Add("deathmatch", 0); // 0, 1, or 2
                Cvars.Coop = CVars.Add("coop", false);
                Cvars.Pausable = CVars.Add("pausable", true);
                Cvars.Temp1 = CVars.Add("temp1", 0);
            }

            FindMaxClients();

            Time = 1.0;		// so a think at time 0 won't get called
        }

        private void InitialiseVCR(QuakeParameters parms)
        {
            if (CommandLine.HasParam("-playback"))
            {
                if (CommandLine.Argc != 2)
                {
                    Utilities.Error("No other parameters allowed with -playback\n");
                }

                Stream file = FileSystem.OpenRead("quake.vcr");
                if (file == null)
                {
                    Utilities.Error("playback file not found\n");
                }

                VcrReader = new BinaryReader(file, Encoding.ASCII);
                var signature = VcrReader.ReadInt32();  //Sys_FileRead(vcrFile, &i, sizeof(int));
                if (signature != HostDef.VCR_SIGNATURE)
                {
                    Utilities.Error("Invalid signature in vcr file\n");
                }

                var argc = VcrReader.ReadInt32(); // Sys_FileRead(vcrFile, &com_argc, sizeof(int));
                var argv = new string[argc + 1];
                argv[0] = parms.argv[0];

                for (var i = 1; i < argv.Length; i++)
                {
                    argv[i] = Utilities.ReadString(VcrReader);
                }
                CommandLine.Args = argv;
                parms.argv = argv;
            }

            var n = CommandLine.CheckParm("-record");
            if (n != 0)
            {
                Stream file = FileSystem.OpenWrite("quake.vcr"); // vcrFile = Sys_FileOpenWrite("quake.vcr");
                VcrWriter = new BinaryWriter(file, Encoding.ASCII);

                VcrWriter.Write(HostDef.VCR_SIGNATURE); //  Sys_FileWrite(vcrFile, &i, sizeof(int));
                VcrWriter.Write(CommandLine.Argc - 1);
                for (var i = 1; i < CommandLine.Argc; i++)
                {
                    if (i == n)
                    {
                        Utilities.WriteString(VcrWriter, "-playback");
                        continue;
                    }
                    Utilities.WriteString(VcrWriter, CommandLine.Argv(i));
                }
            }
        }
        /// <summary>
        /// Host_FindMaxClients
        /// </summary>
        private void FindMaxClients()
        {
            var svs = Server.ServerStatic;
            var cls = Client.Cls;

            svs.maxclients = 1;

            var i = CommandLine.CheckParm("-dedicated");
            if (i > 0)
            {
                cls.state = ClientActive.ca_dedicated;
                svs.maxclients = i != (CommandLine.Argc - 1) ? MathLib.AToI(CommandLine.Argv(i + 1)) : 8;
            }
            else
            {
                cls.state = ClientActive.ca_disconnected;
            }

            i = CommandLine.CheckParm("-listen");
            if (i > 0)
            {
                if (cls.state == ClientActive.ca_dedicated)
                {
                    Utilities.Error("Only one of -dedicated or -listen can be specified");
                }

                svs.maxclients = i != (CommandLine.Argc - 1) ? MathLib.AToI(CommandLine.Argv(i + 1)) : 8;
            }
            if (svs.maxclients < 1)
            {
                svs.maxclients = 8;
            }
            else if (svs.maxclients > QDef.MAX_SCOREBOARD)
            {
                svs.maxclients = QDef.MAX_SCOREBOARD;
            }

            svs.maxclientslimit = svs.maxclients;
            if (svs.maxclientslimit < 4)
            {
                svs.maxclientslimit = 4;
            }

            svs.clients = new FrameworkClient[svs.maxclientslimit]; // Hunk_AllocName (svs.maxclientslimit*sizeof(client_t), "clients");
            for (i = 0; i < svs.clients.Length; i++)
            {
                svs.clients[i] = new FrameworkClient();
            }

            if (svs.maxclients > 1)
            {
                CVars.Set("deathmatch", 1);
            }
            else
            {
                CVars.Set("deathmatch", 0);
            }
        }

        /// <summary>
        /// Host_FilterTime
        /// Returns false if the time is too short to run a frame
        /// </summary>
        private bool FilterTime(double time)
        {
            RealTime += time;

            if (!Client.Cls.timedemo && RealTime - _OldRealTime < 1.0 / 72.0)
            {
                return false;  // framerate is too high
            }

            FrameTime = RealTime - _OldRealTime;
            _OldRealTime = RealTime;

            if (Cvars.FrameRate.Get<double>() > 0)
            {
                FrameTime = Cvars.FrameRate.Get<double>();
            }
            else
            {	// don't allow really long or short frames
                if (FrameTime > 0.1)
                {
                    FrameTime = 0.1;
                }

                if (FrameTime < 0.001)
                {
                    FrameTime = 0.001;
                }
            }

            return true;
        }

        // _Host_Frame
        //
        //Runs all active servers
        private void InternalFrame(double time)
        {
            // keep the random time dependent
            MathLib.Random();

            // decide the simulation time
            if (!FilterTime(time))
            {
                return;         // don't run too fast, or packets will flood out
            }

            // get new key events
            MainWindow.SendKeyEvents();

            // allow mice or other external controllers to add commands
            Input.Commands();

            // process console commands
            Commands.Buffer.Execute();

            Network.Poll();

            // if running the server locally, make intentions now
            if (Server.NetServer.active)
            {
                Client.SendCmd();
            }

            //-------------------
            //
            // server operations
            //
            //-------------------

            // check for commands typed to the host
            GetConsoleCommands();

            if (Server.NetServer.active)
            {
                ServerFrame();
            }

            //-------------------
            //
            // client operations
            //
            //-------------------

            // if running the server remotely, send intentions now after
            // the incoming messages have been read
            if (!Server.NetServer.active)
            {
                Client.SendCmd();
            }

            Time += FrameTime;

            // fetch results from server
            if (Client.Cls.state == ClientActive.ca_connected)
            {
                Client.ReadFromServer();
            }

            // update video
            if (Cvars.HostSpeeds.Get<bool>())
            {
                _Time1 = Timer.GetFloatTime();
            }

            Screen.UpdateScreen();

            if (Cvars.HostSpeeds.Get<bool>())
            {
                _Time2 = Timer.GetFloatTime();
            }

            // update audio
            if (Client.Cls.signon == ClientDef.SIGNONS)
            {
                Sound.Update(ref RenderContext.Origin, ref RenderContext.ViewRight);
                Client.DecayLights();
            }
            else
            {
                Sound.Update(ref Utilities.ZeroVector, ref Utilities.ZeroVector);
            }

            CDAudio.Update();

            if (Cvars.HostSpeeds.Get<bool>())
            {
                var pass1 = (int)((_Time1 - _Time3) * 1000);
                _Time3 = Timer.GetFloatTime();
                var pass2 = (int)((_Time2 - _Time1) * 1000);
                var pass3 = (int)((_Time3 - _Time2) * 1000);
                Console.Print("{0,3} tot {1,3} server {2,3} gfx {3,3} snd\n", pass1 + pass2 + pass3, pass1, pass2, pass3);
            }

            FrameCount++;
        }

        // Host_GetConsoleCommands
        //
        // Add them exactly as if they had been typed at the console
        private void GetConsoleCommands()
        {
            while (true)
            {
                var cmd = DedicatedServer.ConsoleInput();

                if (string.IsNullOrEmpty(cmd))
                {
                    break;
                }

                Commands.Buffer.Append(cmd);
            }
        }

        /// <summary>
        /// host_EndGame
        /// </summary>
        public void EndGame(string message, params object[] args)
        {
            var str = string.Format(message, args);
            Console.DPrint("host_old_EndGame: {0}\n", str);

            if (Server.IsActive)
            {
                ShutdownServer(false);
            }

            if (Client.Cls.state == ClientActive.ca_dedicated)
            {
                Utilities.Error("host_old_EndGame: {0}\n", str);   // dedicated servers exit
            }

            if (Client.Cls.demonum != -1)
            {
                Client.NextDemo();
            }
            else
            {
                Client.Disconnect();
            }

            throw new EndGameException();  //longjmp (host_old_abortserver, 1);
        }

        // Host_Frame
        public void Frame(double time)
        {
            if (!Cvars.ServerProfile.Get<bool>())
            {
                InternalFrame(time);
                return;
            }

            var time1 = Timer.GetFloatTime();
            InternalFrame(time);
            var time2 = Timer.GetFloatTime();

            _TimeTotal += time2 - time1;
            _TimeCount++;

            if (_TimeCount < 1000)
            {
                return;
            }

            var m = (int)(_TimeTotal * 1000 / _TimeCount);
            _TimeCount = 0;
            _TimeTotal = 0;
            var c = 0;
            foreach (var cl in Server.ServerStatic.clients)
            {
                if (cl.active)
                {
                    c++;
                }
            }

            Console.Print("serverprofile: {0,2:d} clients {1,2:d} msec\n", c, m);
        }

        /// <summary>
        /// Host_WriteConfiguration
        /// Writes key bindings and archived cvars to config.cfg
        /// </summary>
        private void WriteConfiguration()
        {
            // dedicated servers initialize the host but don't parse and set the
            // config.cfg cvars
            if (IsInitialised & !IsDedicated)
            {
                var path = Path.Combine(FileSystem.GameDir, "config.cfg");

                using var fs = FileSystem.OpenWrite(path, true);
                if (fs != null)
                {
                    Keyboard.WriteBindings(fs);
                    CVars.WriteVariables(fs);
                }
            }
        }

        /// <summary>
        /// Host_ShutdownServer
        /// This only happens at the end of a game, not between levels
        /// </summary>
        public void ShutdownServer(bool crash)
        {
            if (!Server.IsActive)
            {
                return;
            }

            Server.NetServer.active = false;

            // stop all client sounds immediately
            if (Client.Cls.state == ClientActive.ca_connected)
            {
                Client.Disconnect();
            }

            // flush any pending messages - like the score!!!
            var start = Timer.GetFloatTime();
            int count;
            do
            {
                count = 0;
                for (var i = 0; i < Server.ServerStatic.maxclients; i++)
                {
                    HostClient = Server.ServerStatic.clients[i];
                    if (HostClient.active && !HostClient.message.IsEmpty)
                    {
                        if (Network.CanSendMessage(HostClient.netconnection))
                        {
                            Network.SendMessage(HostClient.netconnection, HostClient.message);
                            HostClient.message.Clear();
                        }
                        else
                        {
                            Network.GetMessage(HostClient.netconnection);
                            count++;
                        }
                    }
                }
                if ((Timer.GetFloatTime() - start) > 3.0)
                {
                    break;
                }
            }
            while (count > 0);

            // make sure all the clients know we're disconnecting
            var writer = new MessageWriter(4);
            writer.WriteByte(ProtocolDef.svc_disconnect);
            count = Network.SendToAll(writer, 5);

            if (count != 0)
            {
                Console.Print("Host_ShutdownServer: NET_SendToAll failed for {0} clients\n", count);
            }

            for (var i = 0; i < Server.ServerStatic.maxclients; i++)
            {
                HostClient = Server.ServerStatic.clients[i];

                if (HostClient.active)
                {
                    Server.DropClient(crash);
                }
            }

            //
            // clear structures
            //
            Server.NetServer.Clear();

            for (var i = 0; i < Server.ServerStatic.clients.Length; i++)
            {
                Server.ServerStatic.clients[i].Clear();
            }
        }

        /// <summary>
        /// Host_Shutdown
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            IsDisposing = true;

            if (!_disposedValue)
            {
                if (disposing)
                {
                    _ShutdownDepth++;
                    try
                    {
                        if (_ShutdownDepth > 1)
                        {
                            return;
                        }

                        // keep Con_Printf from trying to update the screen
                        Screen.IsDisabledForLoading = true;

                        WriteConfiguration();

                        CDAudio.Shutdown();
                        Network.Shutdown();
                        Sound.Shutdown();
                        MainWindow.Input.Shutdown();

                        if (VcrWriter != null)
                        {
                            Console.Print("Closing vcrfile.\n");
                            VcrWriter.Close();
                            VcrWriter = null;
                        }
                        if (VcrReader != null)
                        {
                            Console.Print("Closing vcrfile.\n");
                            VcrReader.Close();
                            VcrReader = null;
                        }

                        if (Client.Cls.state != ClientActive.ca_dedicated)
                        {
                            Video.Shutdown();
                        }

                        Console.Shutdown();
                    }
                    finally
                    {
                        _ShutdownDepth--;

                        // Hack to close process property
                        // Environment.Exit( 0 );
                    }
                }

                _disposedValue = true;
            }

            // Call base class implementation.
            base.Dispose(disposing);
        }
    }
}
