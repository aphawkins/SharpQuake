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

// vid.h -- video driver defs

namespace SharpQuake
{
    using System;
    using System.IO;
    using SharpQuake.Framework;
    using SharpQuake.Framework.IO;
    using SharpQuake.Renderer;

    /// <summary>
    /// Vid_functions
    /// </summary>
    public class Vid
    {
        public ushort[] Table8to16 => Device.Palette.Table8to16;//_8to16table;

        public uint[] Table8to24 => Device.Palette.Table8to24;//_8to24table;

        public byte[] Table15to8 => Device.Palette.Table15to8;//_15to8table;

        public bool GlZTrick => Host.Cvars.glZTrick.Get<bool>();

        public bool WindowedMouse => Host.Cvars.WindowedMouse.Get<bool>();

        public bool Wait => Host.Cvars.Wait.Get<bool>();

        public int ModeNum => Device.ChosenMode;//_ModeNum;

        public const int VID_CBITS = 6;
        public const int VID_GRADES = 1 << VID_CBITS;
        public const int VID_ROW_SIZE = 3;
        private const int WARP_WIDTH = 320;
        private const int WARP_HEIGHT = 200;

        // Instances
        private Host Host
        {
            get;
            set;
        }

        public BaseDevice Device => Host.MainWindow.Device;

        public Vid(Host host)
        {
            Host = host;
        }

        /// <summary>
        /// VID_Init (unsigned char *palette)
        /// Called at startup to set up translation tables, takes 256 8 bit RGB values
        /// the palette data will go away after the call, so it must be copied off if
        /// the video driver will need it again
        /// </summary>
        /// <param name="palette"></param>
        public void Initialise(byte[] palette)
        {
            if (Host.Cvars.glZTrick == null)
            {
                Host.Cvars.glZTrick = Host.CVars.Add("gl_ztrick", true);
                Host.Cvars.Mode = Host.CVars.Add("vid_mode", 0);
                Host.Cvars.DefaultMode = Host.CVars.Add("_vid_default_mode", 0, ClientVariableFlags.Archive);
                Host.Cvars.DefaultModeWin = Host.CVars.Add("_vid_default_mode_win", 3, ClientVariableFlags.Archive);
                Host.Cvars.Wait = Host.CVars.Add("vid_wait", false);
                Host.Cvars.NoPageFlip = Host.CVars.Add("vid_nopageflip", 0, ClientVariableFlags.Archive);
                Host.Cvars.WaitOverride = Host.CVars.Add("_vid_wait_override", 0, ClientVariableFlags.Archive);
                Host.Cvars.ConfigX = Host.CVars.Add("vid_config_x", 800, ClientVariableFlags.Archive);
                Host.Cvars.ConfigY = Host.CVars.Add("vid_config_y", 600, ClientVariableFlags.Archive);
                Host.Cvars.StretchBy2 = Host.CVars.Add("vid_stretch_by_2", 1, ClientVariableFlags.Archive);
                Host.Cvars.WindowedMouse = Host.CVars.Add("_windowed_mouse", true, ClientVariableFlags.Archive);
            }

            Host.Commands.Add("vid_nummodes", NumModes_f);
            Host.Commands.Add("vid_describecurrentmode", DescribeCurrentMode_f);
            Host.Commands.Add("vid_describemode", DescribeMode_f);
            Host.Commands.Add("vid_describemodes", DescribeModes_f);

            Device.Initialise(palette);

            UpdateConsole();
            UpdateScreen();

            // Moved from SetMode

            // so Con_Printfs don't mess us up by forcing vid and snd updates
            var temp = Host.Screen.IsDisabledForLoading;
            Host.Screen.IsDisabledForLoading = true;
            Host.CDAudio.Pause();

            Device.SetMode(Device.ChosenMode, palette);

            var vid = Host.Screen.VidDef;

            UpdateConsole(false);

            vid.width = Device.Desc.Width; // vid.conwidth
            vid.height = Device.Desc.Height;
            vid.numpages = 2;

            Host.CDAudio.Resume();
            Host.Screen.IsDisabledForLoading = temp;

            Host.CVars.Set("vid_mode", Device.ChosenMode);

            // fix the leftover Alt from any Alt-Tab or the like that switched us away
            ClearAllStates();

            Host.Console.SafePrint("Video mode {0} initialized.\n", Device.GetModeDescription(Device.ChosenMode));

            vid.recalc_refdef = true;

            if (Device.Desc.Renderer.StartsWith("PowerVR", StringComparison.InvariantCultureIgnoreCase))
            {
                Host.Screen.FullSbarDraw = true;
            }

            if (Device.Desc.Renderer.StartsWith("Permedia", StringComparison.InvariantCultureIgnoreCase))
            {
                Host.Screen.IsPermedia = true;
            }

            Directory.CreateDirectory(Path.Combine(FileSystem.GameDir, "glquake"));
        }

        private void UpdateScreen()
        {
            Host.Screen.VidDef.maxwarpwidth = WARP_WIDTH;
            Host.Screen.VidDef.maxwarpheight = WARP_HEIGHT;
            Host.Screen.VidDef.colormap = Host.ColorMap;
            var v = BitConverter.ToInt32(Host.ColorMap, 2048);
            Host.Screen.VidDef.fullbright = 256 - EndianHelper.LittleLong(v);
        }

        private void UpdateConsole(bool isInitialStage = true)
        {
            var vid = Host.Screen.VidDef;

            if (isInitialStage)
            {
                var i2 = CommandLine.CheckParm("-conwidth");

                vid.conwidth = i2 > 0 ? MathLib.AToI(CommandLine.Argv(i2 + 1)) : 640;

                vid.conwidth &= 0xfff8; // make it a multiple of eight

                if (vid.conwidth < 320)
                {
                    vid.conwidth = 320;
                }

                // pick a conheight that matches with correct aspect
                vid.conheight = vid.conwidth * 3 / 4;

                i2 = CommandLine.CheckParm("-conheight");

                if (i2 > 0)
                {
                    vid.conheight = MathLib.AToI(CommandLine.Argv(i2 + 1));
                }

                if (vid.conheight < 200)
                {
                    vid.conheight = 200;
                }
            }
            else
            {
                if (vid.conheight > Device.Desc.Height)
                {
                    vid.conheight = Device.Desc.Height;
                }

                if (vid.conwidth > Device.Desc.Width)
                {
                    vid.conwidth = Device.Desc.Width;
                }
            }
        }

        /// <summary>
        /// VID_Shutdown
        /// Called at shutdown
        /// </summary>
        public void Shutdown()
        {
            if (Device is IDisposable device)
            {
                device.Dispose();
            }
            //_IsInitialized = false;
        }

        /// <summary>
        /// VID_GetModeDescription
        /// </summary>
        public string GetModeDescription(int mode)
        {
            return Device.GetModeDescription(mode);
        }

        /// <summary>
        /// VID_NumModes_f
        /// </summary>
        /// <param name="msg"></param>
        private void NumModes_f(CommandMessage msg)
        {
            var nummodes = Device.AvailableModes.Length;

            if (nummodes == 1)
            {
                Host.Console.Print("{0} video mode is available\n", nummodes);
            }
            else
            {
                Host.Console.Print("{0} video modes are available\n", nummodes);
            }
        }

        /// <summary>
        /// VID_DescribeCurrentMode_f
        /// </summary>
        /// <param name="msg"></param>
        private void DescribeCurrentMode_f(CommandMessage msg)
        {
            Host.Console.Print("{0}\n", GetModeDescription(Device.ChosenMode));
        }

        /// <summary>
        /// VID_DescribeMode_f
        /// </summary>
        /// <param name="msg"></param>
        private void DescribeMode_f(CommandMessage msg)
        {
            var modenum = MathLib.AToI(msg.Parameters[0]);

            Host.Console.Print("{0}\n", GetModeDescription(modenum));
        }

        /// <summary>
        /// VID_DescribeModes_f
        /// </summary>
        /// <param name="msg"></param>
        private void DescribeModes_f(CommandMessage msg)
        {
            for (var i = 0; i < Device.AvailableModes.Length; i++)
            {
                Host.Console.Print("{0}:{1}\n", i, GetModeDescription(i));
            }
        }

        /// <summary>
        /// ClearAllStates
        /// </summary>
        private void ClearAllStates()
        {
            // send an up event for each key, to make sure the server clears them all
            for (var i = 0; i < 256; i++)
            {
                Host.Keyboard.Event(i, false);
            }

            Host.Keyboard.ClearStates();
            MainWindow.Input.ClearStates();
        }
    }
}
