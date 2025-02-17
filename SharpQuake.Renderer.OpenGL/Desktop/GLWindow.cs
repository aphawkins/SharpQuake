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

namespace SharpQuake.Renderer.OpenGL.Desktop
{
    using System;
    using System.Drawing;
    using SharpQuake.Renderer.Desktop;
    using SharpQuake.Framework.IO.Input;

    public class GLWindow : BaseWindow, IDisposable
    {
        private OpenTK.GameWindow OpenTKWindow
        {
            get;
            set;
        }

        private OpenTK.DisplayDevice DisplayDevice
        {
            get;
            set;
        }

        public override VSyncMode VSync
        {

            get => OpenTKWindow.VSync switch
            {
                OpenTK.VSyncMode.On => VSyncMode.One,
                OpenTK.VSyncMode.Adaptive => VSyncMode.Other,
                _ => VSyncMode.None,
            };
            set
            {
                switch (value)
                {
                    case VSyncMode.One:
                        OpenTKWindow.VSync = OpenTK.VSyncMode.On;
                        break;

                    case VSyncMode.None:
                        OpenTKWindow.VSync = OpenTK.VSyncMode.Off;
                        break;

                    case VSyncMode.Other:
                        OpenTKWindow.VSync = OpenTK.VSyncMode.Adaptive;
                        break;
                }
            }
        }

        public override Icon Icon
        {
            get => OpenTKWindow.Icon;
            set => OpenTKWindow.Icon = value;
        }

        public override Size ClientSize
        {
            get => OpenTKWindow.ClientSize;
            set => OpenTKWindow.ClientSize = value;
        }

        public override bool IsFullScreen => OpenTKWindow.WindowState == OpenTK.WindowState.Fullscreen;

        public override bool Focused => OpenTKWindow.Focused;

        public override bool IsMinimised => OpenTKWindow.WindowState == OpenTK.WindowState.Minimized;

        public override bool CursorVisible
        {
            get => OpenTKWindow.CursorVisible;
            set => OpenTKWindow.CursorVisible = value;
        }

        public override Rectangle Bounds
        {
            get => OpenTKWindow.Bounds;
            set => OpenTKWindow.Bounds = value;
        }

        public override bool IsMouseActive => OpenTK.Input.Mouse.GetState(0).IsConnected != false;

        public GLWindow(string title, Size size, bool isFullScreen) : base()
        {
            //Workaround for SDL2 mouse input issues
            var options = new OpenTK.ToolkitOptions
            {
                Backend = OpenTK.PlatformBackend.PreferNative,
                EnableHighResolution = true //Just for testing
            };
            OpenTK.Toolkit.Init(options);

            // select display device
            DisplayDevice = OpenTK.DisplayDevice.Default;

            OpenTKWindow = new OpenTK.GameWindow(size.Width, size.Height, new OpenTK.Graphics.GraphicsMode(),
                title, isFullScreen ? OpenTK.GameWindowFlags.Fullscreen : OpenTK.GameWindowFlags.Default);

            RouteEvents();

            Device = new GLDevice(OpenTKWindow, DisplayDevice);
        }

        public override void RouteEvents()
        {
            OpenTKWindow.FocusedChanged += (sender, args) => OnFocusedChanged();

            OpenTKWindow.Closing += (sender, args) => OnClosing();

            OpenTKWindow.UpdateFrame += (sender, args) => OnUpdateFrame(args.Time);

            OpenTKWindow.KeyDown += (sender, args) => KeyDown?.Invoke(sender, new KeyboardKeyEventArgs((Key)(int)args.Key));

            OpenTKWindow.KeyUp += (sender, args) => KeyUp?.Invoke(sender, new KeyboardKeyEventArgs((Key)(int)args.Key));

            OpenTKWindow.MouseMove += (sender, args) => MouseMove?.Invoke(sender, new EventArgs());

            OpenTKWindow.MouseDown += (sender, args) => MouseDown?.Invoke(sender, new MouseButtonEventArgs((MouseButton)(int)args.Button, args.IsPressed));

            OpenTKWindow.MouseUp += (sender, args) => MouseUp?.Invoke(sender, new MouseButtonEventArgs((MouseButton)(int)args.Button, args.IsPressed));

            OpenTKWindow.MouseWheel += (sender, args) => MouseWheel?.Invoke(sender, new MouseWheelEventArgs(args.Delta));
        }

        public override void Run()
        {
            OpenTKWindow.Run();
        }

        protected override void OnFocusedChanged()
        {
            //throw new NotImplementedException( );
        }

        protected override void OnClosing()
        {
            //throw new NotImplementedException( );
        }

        protected override void OnUpdateFrame(double Time)
        {
            //throw new NotImplementedException( );
        }

        public override void Present()
        {
            OpenTKWindow.SwapBuffers();
        }

        public override void SetFullScreen(bool isFullScreen)
        {
            if (isFullScreen)
            {
                OpenTKWindow.WindowState = OpenTK.WindowState.Fullscreen;
                OpenTKWindow.WindowBorder = OpenTK.WindowBorder.Hidden;
            }
            else
            {
                OpenTKWindow.WindowState = OpenTK.WindowState.Normal;
                OpenTKWindow.WindowBorder = OpenTK.WindowBorder.Fixed;
            }
        }


        public override void ProcessEvents()
        {
            OpenTKWindow.ProcessEvents();
        }

        public override void Exit()
        {
            OpenTKWindow.Exit();
        }

        public override void SetMousePosition(int x, int y)
        {
            OpenTK.Input.Mouse.SetPosition(x, y);
        }

        public override Point GetMousePosition()
        {
            return new Point(OpenTK.Input.Mouse.GetCursorState().X,
                 OpenTK.Input.Mouse.GetCursorState().Y);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    OpenTKWindow.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                IsDisposed = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~GLWindow()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
