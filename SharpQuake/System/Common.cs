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

//
// Source: common.h + common.c
//

// All of Quake's data access is through a hierarchical file system, but the contents of the file system can be transparently merged from several sources.
//
// The "base directory" is the path to the directory holding the quake.exe and all game directories.  The sys_* files pass this to host_init in quakeparms_t->basedir.  This can be overridden with the "-basedir" command line parm to allow code debugging in a different directory.  The base directory is
// only used during filesystem initialization.
//
// The "game directory" is the first tree on the search path and directory that all generated files (savegames, screenshots, demos, config files) will be saved to.  This can be overridden with the "-game" command line parameter.  The game directory can never be changed while quake is executing.  This is a precacution against having a malicious server instruct clients to write files over areas they shouldn't.
//
// The "cache directory" is only used during development to save network bandwidth, especially over ISDN / T1 lines.  If there is a cache directory
// specified, when a file is found by the normal search path, it will be mirrored
// into the cache directory, then opened there.
//
//
//
// FIXME:
// The file "parms.txt" will be read out of the game directory and appended to the current command line arguments to allow different games to initialize startup parms differently.  This could be used to add a "-sspeed 22050" for the high quality sound edition.  Because they are added at the end, they will not override an explicit setting on the original command line.

namespace SharpQuake
{
    using System;
    using SharpQuake.Framework;
    using SharpQuake.Framework.IO;

    public class Common
    {
        public GameKind GameKind { get; private set; }

        public bool IsRegistered
        {
            get
            {
                return MainWindow.Host.Cvars.Registered.Get<bool>();
            }
        }

        // if a packfile directory differs from this, it is assumed to be hacked
        public const int PAK0_COUNT = 339;

        public const int PAK0_CRC = 32981;

        // this graphic needs to be in the pak file to use registered features
        private static readonly ushort[] _Pop = new ushort[]
        {
             0x0000,0x0000,0x0000,0x0000,0x0000,0x0000,0x0000,0x0000
            ,0x0000,0x0000,0x6600,0x0000,0x0000,0x0000,0x6600,0x0000
            ,0x0000,0x0066,0x0000,0x0000,0x0000,0x0000,0x0067,0x0000
            ,0x0000,0x6665,0x0000,0x0000,0x0000,0x0000,0x0065,0x6600
            ,0x0063,0x6561,0x0000,0x0000,0x0000,0x0000,0x0061,0x6563
            ,0x0064,0x6561,0x0000,0x0000,0x0000,0x0000,0x0061,0x6564
            ,0x0064,0x6564,0x0000,0x6469,0x6969,0x6400,0x0064,0x6564
            ,0x0063,0x6568,0x6200,0x0064,0x6864,0x0000,0x6268,0x6563
            ,0x0000,0x6567,0x6963,0x0064,0x6764,0x0063,0x6967,0x6500
            ,0x0000,0x6266,0x6769,0x6a68,0x6768,0x6a69,0x6766,0x6200
            ,0x0000,0x0062,0x6566,0x6666,0x6666,0x6666,0x6562,0x0000
            ,0x0000,0x0000,0x0062,0x6364,0x6664,0x6362,0x0000,0x0000
            ,0x0000,0x0000,0x0000,0x0062,0x6662,0x0000,0x0000,0x0000
            ,0x0000,0x0000,0x0000,0x0061,0x6661,0x0000,0x0000,0x0000
            ,0x0000,0x0000,0x0000,0x0000,0x6500,0x0000,0x0000,0x0000
            ,0x0000,0x0000,0x0000,0x0000,0x6400,0x0000,0x0000,0x0000
        };


        // Instances
        private MainWindow MainWindow
        {
            get;
            set;
        }

        // void COM_Init (char *path)
        public void Initialise(MainWindow mainWindow, string path, string[] argv)
        {
            MainWindow = mainWindow;

            CommandLine.Args = argv;

            mainWindow.Host.Cvars.Registered = mainWindow.Host.CVars.Add("registered", false);
            mainWindow.Host.Cvars.CmdLine = mainWindow.Host.CVars.Add("cmdline", "0", ClientVariableFlags.Server);

            MainWindow.Host.Commands.Add("path", FileSystem.Path_f);

            CommandLine.Init(path, argv);
            FileSystem.InitFileSystem(MainWindow.Host.Parameters);

            CheckRegistered();
        }

        // void COM_InitArgv (int argc, char **argv)
        public void InitArgv(string[] argv)
        {
            CommandLine.InitArgv(argv);

            GameKind = GameKind.StandardQuake;

            if (CommandLine.HasParam("-rogue"))
                GameKind = GameKind.Rogue;

            if (CommandLine.HasParam("-hipnotic"))
                GameKind = GameKind.Hipnotic;
        }

        // COM_CheckRegistered
        //
        // Looks for the pop.txt file and verifies it.
        // Sets the "registered" cvar.
        // Immediately exits out if an alternate game was attempted to be started without
        // being registered.
        private void CheckRegistered()
        {
            FileSystem._StaticRegistered = false;

            var buf = FileSystem.LoadFile("gfx/pop.lmp");
            if (buf == null || buf.Length < 256)
            {
                MainWindow.Host.Console.Print("Playing shareware version.\n");
                if (FileSystem._IsModified)
                    Utilities.Error("You must have the registered version to use modified games");
                return;
            }

            var check = new ushort[buf.Length / 2];
            Buffer.BlockCopy(buf, 0, check, 0, buf.Length);
            for (var i = 0; i < 128; i++)
            {
                if (_Pop[i] != (ushort)EndianHelper.Converter.BigShort((short)check[i]))
                    Utilities.Error("Corrupted data file.");
            }

            MainWindow.Host.CVars.Set("cmdline", CommandLine._Args);
            MainWindow.Host.CVars.Set("registered", true);
            FileSystem._StaticRegistered = true;
            MainWindow.Host.Console.Print("Playing registered version.\n");
        }
    }
}
