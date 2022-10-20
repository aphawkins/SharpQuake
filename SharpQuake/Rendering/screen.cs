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

// screen.h
// gl_screen.c

namespace SharpQuake
{
    using System;
    using SharpQuake.Framework;
    using SharpQuake.Framework.IO;
    using SharpQuake.Framework.IO.Input;
    using SharpQuake.Renderer;
    using SharpQuake.Renderer.Textures;

    /// <summary>
    /// SCR_functions
    /// </summary>
    public partial class Scr
    {
        public VidDef vid { get; } = new VidDef();

        public ClientVariable ViewSize
        {
            get
            {
                return Host.Cvars.ViewSize;
            }
        }

        public float ConCurrent { get; private set; }

        public bool CopyEverithing { get; set; }

        public bool IsDisabledForLoading;
        public bool BlockDrawing
        {
            get => Host.Video.Device.BlockDrawing;
            set => Host.Video.Device.BlockDrawing = value;
        }

        public bool SkipUpdate
        {
            get => Host.Video.Device.SkipUpdate;
            set => Host.Video.Device.SkipUpdate = value;
        }

        // scr_skipupdate
        public bool FullSbarDraw;

        // fullsbardraw = false
        public bool IsPermedia;

        // only the refresh window will be updated unless these variables are flagged
        public bool CopyTop;

        public int ClearNotify;
        public int glX;
        public int glY;
        public int glWidth;
        public int glHeight;
        public float CenterTimeOff;
        public int FullUpdate;
        private VRect _VRect; // scr_vrect

        // scr_disabled_for_loading
        private bool _DrawLoading; // scr_drawloading

        private double _DisabledTime; // float scr_disabled_time

        // qboolean block_drawing
        private bool _DrawDialog; // scr_drawdialog

        // isPermedia
        private bool _IsInitialized;

        private bool _InUpdate;
        private BasePicture Ram;
        private BasePicture Net;
        private BasePicture Turtle;
        private int _TurtleCount; // count from SCR_DrawTurtle()
        private float _ConLines;		// lines of console to display
        private int _ClearConsole; // clearconsole
                                   // clearnotify

        private float _OldScreenSize; // float oldscreensize
        private float _OldFov; // float oldfov
        private int _CenterLines; // scr_center_lines
        private int _EraseLines; // scr_erase_lines

        //int _EraseCenter; // scr_erase_center
        private float _CenterTimeStart; // scr_centertime_start	// for slow victory printing

        // scr_centertime_off
        private string _CenterString; // char	scr_centerstring[1024]

        private string _NotifyString; // scr_notifystring
        private bool _IsMouseWindowed; // windowed_mouse (don't confuse with _windowed_mouse cvar)
                                       // scr_fullupdate    set to 0 to force full redraw
                                       // CHANGE
        private Host Host
        {
            get;
            set;
        }

        public Scr(Host host)
        {
            Host = host;
        }

        // SCR_Init
        public void Initialise()
        {
            if (Host.Cvars.ViewSize == null)
            {
                Host.Cvars.ViewSize = Host.CVars.Add("viewsize", 100f, ClientVariableFlags.Archive);
                Host.Cvars.Fov = Host.CVars.Add("fov", 90f);	// 10 - 170
                Host.Cvars.ConSpeed = Host.CVars.Add("scr_conspeed", 3000);
                Host.Cvars.CenterTime = Host.CVars.Add("scr_centertime", 2);
                Host.Cvars.ShowRam = Host.CVars.Add("showram", true);
                Host.Cvars.ShowTurtle = Host.CVars.Add("showturtle", false);
                Host.Cvars.ShowPause = Host.CVars.Add("showpause", true);
                Host.Cvars.PrintSpeed = Host.CVars.Add("scr_printspeed", 8);
                Host.Cvars.glTripleBuffer = Host.CVars.Add("gl_triplebuffer", 1, ClientVariableFlags.Archive);
            }

            //
            // register our commands
            //
            Host.Commands.Add("screenshot", ScreenShot_f);
            Host.Commands.Add("sizeup", SizeUp_f);
            Host.Commands.Add("sizedown", SizeDown_f);

            Ram = BasePicture.FromWad(Host.Video.Device, Host.GfxWad, "ram", "GL_LINEAR");
            Net = BasePicture.FromWad(Host.Video.Device, Host.GfxWad, "net", "GL_LINEAR");
            Turtle = BasePicture.FromWad(Host.Video.Device, Host.GfxWad, "turtle", "GL_LINEAR");

            if (CommandLine.HasParam("-fullsbar"))
                FullSbarDraw = true;

            _IsInitialized = true;
        }

        // void SCR_UpdateScreen (void);
        // This is called every frame, and can also be called explicitly to flush
        // text to the screen.
        //
        // WARNING: be very careful calling this from elsewhere, because the refresh
        // needs almost the entire 256k of stack space!
        public void UpdateScreen()
        {
            if (BlockDrawing || !_IsInitialized || _InUpdate)
                return;

            _InUpdate = true;
            try
            {
                if (MainWindow.Instance != null && !MainWindow.Instance.IsDisposing)
                {
                    if (MainWindow.Instance.VSync == VSyncMode.One != Host.Video.Wait)
                        MainWindow.Instance.VSync = Host.Video.Wait ? VSyncMode.One : VSyncMode.None;
                }

                vid.numpages = 2 + (int)Host.Cvars.glTripleBuffer.Get<int>();

                CopyTop = false;
                CopyEverithing = false;

                if (IsDisabledForLoading)
                {
                    if ((Host.RealTime - _DisabledTime) > 60)
                    {
                        IsDisabledForLoading = false;
                        Host.Console.Print("Load failed.\n");
                    }
                    else
                        return;
                }

                if (!Host.Console.IsInitialized)
                    return;	// not initialized yet

                BeginRendering();

                //
                // determine size of refresh window
                //
                if (_OldFov != Host.Cvars.Fov.Get<float>())
                {
                    _OldFov = Host.Cvars.Fov.Get<float>();
                    vid.recalc_refdef = true;
                }

                if (_OldScreenSize != Host.Cvars.ViewSize.Get<float>())
                {
                    _OldScreenSize = Host.Cvars.ViewSize.Get<float>();
                    vid.recalc_refdef = true;
                }

                if (vid.recalc_refdef)
                    CalcRefdef();

                //
                // do 3D refresh drawing, and then update the screen
                //
                SetUpToDrawConsole();

                Host.View.RenderView();

                Host.Video.Device.Begin2DScene();
                //Set2D();

                //
                // draw any areas not covered by the refresh
                //
                Host.Screen.TileClear();

                if (_DrawDialog)
                {
                    Host.Hud.Draw();
                    Host.DrawingContext.FadeScreen();
                    DrawNotifyString();
                    CopyEverithing = true;
                }
                else if (_DrawLoading)
                {
                    DrawLoading();
                    Host.Hud.Draw();
                }
                else if (Host.Client.cl.intermission == 1 && Host.Keyboard.Destination == KeyDestination.key_game)
                {
                    Host.Hud.IntermissionOverlay();
                }
                else if (Host.Client.cl.intermission == 2 && Host.Keyboard.Destination == KeyDestination.key_game)
                {
                    Host.Hud.FinaleOverlay();
                    CheckDrawCenterString();
                }
                else
                {
                    if (Host.View.Crosshair > 0)
                        Host.DrawingContext.DrawCharacter(_VRect.x + (_VRect.width / 2), _VRect.y + (_VRect.height / 2), '+');

                    DrawRam();
                    DrawNet();
                    DrawTurtle();
                    DrawPause();
                    CheckDrawCenterString();
                    Host.Hud.Draw();
                    DrawConsole();
                    Host.Menu.Draw();
                }

                if (Host.ShowFPS)
                {
                    if (DateTime.Now.Subtract(Host.LastFPSUpdate).TotalSeconds >= 1)
                    {
                        Host.FPS = Host.FPSCounter;
                        Host.FPSCounter = 0;
                        Host.LastFPSUpdate = DateTime.Now;
                    }

                    Host.FPSCounter++;

                    Host.DrawingContext.DrawString(Host.Screen.vid.width - 16 - 10, 10, $"{Host.FPS}", System.Drawing.Color.Yellow);
                }
                Host.Video.Device.End2DScene();

                Host.View.UpdatePalette();
                EndRendering();
            }
            finally
            {
                _InUpdate = false;
            }
        }

        /// <summary>
        /// GL_EndRendering
        /// </summary>
        public void EndRendering()
        {
            if (MainWindow.Instance == null || MainWindow.Instance.IsDisposing)
                return;

            var form = MainWindow.Instance;
            if (form == null)
                return;

            Host.Video?.Device?.EndScene();

            //if( !SkipUpdate || BlockDrawing )
            //    form.SwapBuffers();

            // handle the mouse state
            if (!Host.Video.WindowedMouse)
            {
                if (_IsMouseWindowed)
                {
                    MainWindow.Input.DeactivateMouse();
                    MainWindow.Input.ShowMouse();
                    _IsMouseWindowed = false;
                }
            }
            else
            {
                _IsMouseWindowed = true;
                if (Host.Keyboard.Destination == KeyDestination.key_game && !MainWindow.Input.IsMouseActive &&
                    Host.Client.cls.state != cactive_t.ca_disconnected)// && ActiveApp)
                {
                    MainWindow.Input.ActivateMouse();
                    MainWindow.Input.HideMouse();
                }
                else if (MainWindow.Input.IsMouseActive && Host.Keyboard.Destination != KeyDestination.key_game)
                {
                    MainWindow.Input.DeactivateMouse();
                    MainWindow.Input.ShowMouse();
                }
            }

            if (FullSbarDraw)
                Host.Hud.Changed();
        }

        // SCR_CenterPrint
        //
        // Called for important messages that should stay in the center of the screen
        // for a few moments
        public void CenterPrint(string str)
        {
            _CenterString = str;
            CenterTimeOff = Host.Cvars.CenterTime.Get<int>();
            _CenterTimeStart = (float)Host.Client.cl.time;

            // count the number of lines for centering
            _CenterLines = 1;
            foreach (var c in _CenterString)
            {
                if (c == '\n')
                    _CenterLines++;
            }
        }

        /// <summary>
        /// SCR_EndLoadingPlaque
        /// </summary>
        public void EndLoadingPlaque()
        {
            Host.Screen.IsDisabledForLoading = false;
            Host.Screen.FullUpdate = 0;
            Host.Console.ClearNotify();
        }

        /// <summary>
        /// SCR_BeginLoadingPlaque
        /// </summary>
        public void BeginLoadingPlaque()
        {
            Host.Sound.StopAllSounds(true);

            if (Host.Client.cls.state != cactive_t.ca_connected)
                return;
            if (Host.Client.cls.signon != ClientDef.SIGNONS)
                return;

            // redraw with no console and the loading plaque
            Host.Console.ClearNotify();
            CenterTimeOff = 0;
            ConCurrent = 0;

            _DrawLoading = true;
            Host.Screen.FullUpdate = 0;
            Host.Hud.Changed();
            UpdateScreen();
            _DrawLoading = false;

            Host.Screen.IsDisabledForLoading = true;
            _DisabledTime = Host.RealTime;
            Host.Screen.FullUpdate = 0;
        }

        /// <summary>
        /// SCR_ModalMessage
        /// Displays a text string in the center of the screen and waits for a Y or N keypress.
        /// </summary>
        public bool ModalMessage(string text)
        {
            if (Host.Client.cls.state == cactive_t.ca_dedicated)
                return true;

            _NotifyString = text;

            // draw a fresh screen
            Host.Screen.FullUpdate = 0;
            _DrawDialog = true;
            UpdateScreen();
            _DrawDialog = false;

            Host.Sound.ClearBuffer();		// so dma doesn't loop current sound

            do
            {
                Host.Keyboard.KeyCount = -1;        // wait for a key down and up
                Host.MainWindow.SendKeyEvents();
            } while (Host.Keyboard.LastPress is not 'y' and not 'n' and not KeysDef.K_ESCAPE);

            Host.Screen.FullUpdate = 0;
            UpdateScreen();

            return Host.Keyboard.LastPress == 'y';
        }

        // SCR_SizeUp_f
        //
        // Keybinding command
        private void SizeUp_f(CommandMessage msg)
        {
            Host.CVars.Set("viewsize", Host.Cvars.ViewSize.Get<float>() + 10);
            vid.recalc_refdef = true;
        }

        // SCR_SizeDown_f
        //
        // Keybinding command
        private void SizeDown_f(CommandMessage msg)
        {
            Host.CVars.Set("viewsize", Host.Cvars.ViewSize.Get<float>() - 10);
            vid.recalc_refdef = true;
        }

        // SCR_ScreenShot_f
        private void ScreenShot_f(CommandMessage msg)
        {
            Host.Video.Device.ScreenShot(out var path);
        }

        /// <summary>
        /// GL_BeginRendering
        /// </summary>
        private void BeginRendering()
        {
            if (MainWindow.Instance == null || MainWindow.Instance.IsDisposing)
                return;

            glX = 0;
            glY = 0;
            glWidth = 0;
            glHeight = 0;

            var window = MainWindow.Instance;
            if (window != null)
            {
                var size = window.ClientSize;
                glWidth = size.Width;
                glHeight = size.Height;
            }

            Host.Video?.Device?.BeginScene();
        }

        // SCR_CalcRefdef
        //
        // Must be called whenever vid changes
        // Internal use only
        private void CalcRefdef()
        {
            Host.Screen.FullUpdate = 0; // force a background redraw
            vid.recalc_refdef = false;

            // force the status bar to redraw
            Host.Hud.Changed();

            // bound viewsize
            if (Host.Cvars.ViewSize.Get<float>() < 30)
                Host.CVars.Set("viewsize", 30f);
            if (Host.Cvars.ViewSize.Get<float>() > 120)
                Host.CVars.Set("viewsize", 120f);

            // bound field of view
            if (Host.Cvars.Fov.Get<float>() < 10)
                Host.CVars.Set("fov", 10f);
            if (Host.Cvars.Fov.Get<float>() > 170)
                Host.CVars.Set("fov", 170f);

            // intermission is always full screen
            float size = Host.Client.cl.intermission > 0 ? 120 : Host.Cvars.ViewSize.Get<float>();
            if (size >= 120)
                Host.Hud.Lines = 0; // no status bar at all
            else if (size >= 110)
                Host.Hud.Lines = 24; // no inventory
            else
                Host.Hud.Lines = 24 + 16 + 8;

            var full = false;
            if (Host.Cvars.ViewSize.Get<float>() >= 100.0)
            {
                full = true;
                size = 100.0f;
            }
            else
                size = Host.Cvars.ViewSize.Get<float>();

            if (Host.Client.cl.intermission > 0)
            {
                full = true;
                size = 100;
                Host.Hud.Lines = 0;
            }
            size /= 100.0f;

            var h = vid.height - Host.Hud.Lines;

            var rdef = Host.RenderContext.RefDef;
            rdef.vrect.width = (int)(vid.width * size);
            if (rdef.vrect.width < 96)
            {
                size = 96.0f / rdef.vrect.width;
                rdef.vrect.width = 96;  // min for icons
            }

            rdef.vrect.height = (int)(vid.height * size);
            if (rdef.vrect.height > vid.height - Host.Hud.Lines)
                rdef.vrect.height = vid.height - Host.Hud.Lines;
            if (rdef.vrect.height > vid.height)
                rdef.vrect.height = vid.height;
            rdef.vrect.x = (vid.width - rdef.vrect.width) / 2;
            rdef.vrect.y = full ? 0 : (h - rdef.vrect.height) / 2;

            rdef.fov_x = Host.Cvars.Fov.Get<float>();
            rdef.fov_y = CalcFov(rdef.fov_x, rdef.vrect.width, rdef.vrect.height);

            _VRect = rdef.vrect;
        }

        // CalcFov
        private float CalcFov(float fov_x, float width, float height)
        {
            if (fov_x is < 1 or > 179)
                Utilities.Error("Bad fov: {0}", fov_x);

            var x = width / Math.Tan(fov_x / 360.0 * Math.PI);
            var a = Math.Atan(height / x);
            a = a * 360.0 / Math.PI;
            return (float)a;
        }

        /// <summary>
        /// SCR_SetUpToDrawConsole
        /// </summary>
        private void SetUpToDrawConsole()
        {
            Host.Console.CheckResize();

            if (_DrawLoading)
                return;     // never a console with loading plaque

            // decide on the height of the console
            Host.Console.ForcedUp = (Host.Client.cl.worldmodel == null) || (Host.Client.cls.signon != ClientDef.SIGNONS);

            if (Host.Console.ForcedUp)
            {
                _ConLines = vid.height; // full screen
                ConCurrent = _ConLines;
            }
            else if (Host.Keyboard.Destination == KeyDestination.key_console)
                _ConLines = vid.height / 2; // half screen
            else
                _ConLines = 0; // none visible

            if (_ConLines < ConCurrent)
            {
                ConCurrent -= (int)(Host.Cvars.ConSpeed.Get<int>() * Host.FrameTime);
                if (_ConLines > ConCurrent)
                    ConCurrent = _ConLines;
            }
            else if (_ConLines > ConCurrent)
            {
                ConCurrent += (int)(Host.Cvars.ConSpeed.Get<int>() * Host.FrameTime);
                if (_ConLines < ConCurrent)
                    ConCurrent = _ConLines;
            }

            if (_ClearConsole++ < vid.numpages)
            {
                Host.Hud.Changed();
            }
            else if (ClearNotify++ < vid.numpages)
            {
                //????????????
            }
            else
                Host.Console.NotifyLines = 0;
        }

        // SCR_TileClear
        private void TileClear()
        {
            var rdef = Host.RenderContext.RefDef;
            if (rdef.vrect.x > 0)
            {
                // left
                Host.DrawingContext.TileClear(0, 0, rdef.vrect.x, vid.height - Host.Hud.Lines);
                // right
                Host.DrawingContext.TileClear(rdef.vrect.x + rdef.vrect.width, 0,
                    vid.width - rdef.vrect.x + rdef.vrect.width,
                    vid.height - Host.Hud.Lines);
            }
            if (rdef.vrect.y > 0)
            {
                // top
                Host.DrawingContext.TileClear(rdef.vrect.x, 0, rdef.vrect.x + rdef.vrect.width, rdef.vrect.y);
                // bottom
                Host.DrawingContext.TileClear(rdef.vrect.x, rdef.vrect.y + rdef.vrect.height,
                    rdef.vrect.width, vid.height - Host.Hud.Lines - (rdef.vrect.height + rdef.vrect.y));
            }
        }

        /// <summary>
        /// SCR_DrawNotifyString
        /// </summary>
        private void DrawNotifyString()
        {
            var offset = 0;
            var y = (int)(Host.Screen.vid.height * 0.35);

            do
            {
                var end = _NotifyString.IndexOf('\n', offset);
                if (end == -1)
                    end = _NotifyString.Length;
                if (end - offset > 40)
                    end = offset + 40;

                var length = end - offset;
                if (length > 0)
                {
                    var x = (vid.width - (length * 8)) / 2;
                    for (var j = 0; j < length; j++, x += 8)
                        Host.DrawingContext.DrawCharacter(x, y, _NotifyString[offset + j]);

                    y += 8;
                }
                offset = end + 1;
            } while (offset < _NotifyString.Length);
        }

        /// <summary>
        /// SCR_DrawLoading
        /// </summary>
        private void DrawLoading()
        {
            if (!_DrawLoading)
                return;

            var pic = Host.DrawingContext.CachePic("gfx/loading.lmp", "GL_LINEAR");
            Host.Video.Device.Graphics.DrawPicture(pic, (vid.width - pic.Width) / 2, (vid.height - 48 - pic.Height) / 2);
        }

        // SCR_CheckDrawCenterString
        private void CheckDrawCenterString()
        {
            CopyTop = true;
            if (_CenterLines > _EraseLines)
                _EraseLines = _CenterLines;

            CenterTimeOff -= (float)Host.FrameTime;

            if (CenterTimeOff <= 0 && Host.Client.cl.intermission == 0)
                return;
            if (Host.Keyboard.Destination != KeyDestination.key_game)
                return;

            DrawCenterString();
        }

        // SCR_DrawRam
        private void DrawRam()
        {
            if (!Host.Cvars.ShowRam.Get<bool>())
                return;

            if (!Host.RenderContext.CacheTrash)
                return;

            Host.Video.Device.Graphics.DrawPicture(Ram, _VRect.x + 32, _VRect.y);
        }

        // SCR_DrawTurtle
        private void DrawTurtle()
        {
            //int	count;

            if (!Host.Cvars.ShowTurtle.Get<bool>())
                return;

            if (Host.FrameTime < 0.1)
            {
                _TurtleCount = 0;
                return;
            }

            _TurtleCount++;
            if (_TurtleCount < 3)
                return;

            Host.Video.Device.Graphics.DrawPicture(Turtle, _VRect.x, _VRect.y);
        }

        // SCR_DrawNet
        private void DrawNet()
        {
            if (Host.RealTime - Host.Client.cl.last_received_message < 0.3)
                return;
            if (Host.Client.cls.demoplayback)
                return;

            Host.Video.Device.Graphics.DrawPicture(Net, _VRect.x + 64, _VRect.y);
        }

        // DrawPause
        private void DrawPause()
        {
            if (!Host.Cvars.ShowPause.Get<bool>())	// turn off for screenshots
                return;

            if (!Host.Client.cl.paused)
                return;

            var pic = Host.DrawingContext.CachePic("gfx/pause.lmp", "GL_NEAREST");
            Host.Video.Device.Graphics.DrawPicture(pic, (vid.width - pic.Width) / 2, (vid.height - 48 - pic.Height) / 2);
        }

        // SCR_DrawConsole
        private void DrawConsole()
        {
            if (ConCurrent > 0)
            {
                CopyEverithing = true;
                Host.Console.Draw((int)ConCurrent, true);
                _ClearConsole = 0;
            }
            else if (Host.Keyboard.Destination is KeyDestination.key_game or
                KeyDestination.key_message)
            {
                Host.Console.DrawNotify();	// only draw notify in game
            }
        }

        // SCR_DrawCenterString
        private void DrawCenterString()
        {
            int remaining = Host.Client.cl.intermission > 0 ? (int)(Host.Cvars.PrintSpeed.Get<int>() * (Host.Client.cl.time - _CenterTimeStart)) : 9999;

            // the finale prints the characters one at a time

            var y = 48;
            if (_CenterLines <= 4)
                y = (int)(vid.height * 0.35);

            var lines = _CenterString.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd('\r');
                var x = (vid.width - (line.Length * 8)) / 2;

                for (var j = 0; j < line.Length; j++, x += 8)
                {
                    Host.DrawingContext.DrawCharacter(x, y, line[j]);
                    if (remaining-- <= 0)
                        return;
                }
                y += 8;
            }
        }
    }
}
