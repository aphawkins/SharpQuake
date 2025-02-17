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

namespace SharpQuake.Renderer
{
    using System;
    using System.Drawing;
    using System.IO;
    using System.Linq;
    using System.Numerics;
    using SharpQuake.Framework;
    using SharpQuake.Framework.IO;
    using SharpQuake.Renderer.Textures;

    public class BaseDevice
    {
        public BaseDeviceDesc Desc
        {
            get;
            private set;
        }

        public Type TextureType
        {
            get;
            private set;
        }

        public Type TextureAtlasType
        {
            get;
            private set;
        }

        public Type TextureDescType
        {
            get;
            private set;
        }

        public Type ModelType
        {
            get;
            private set;
        }

        public Type ModelDescType
        {
            get;
            private set;
        }
        public Type AliasModelType
        {
            get;
            private set;
        }

        public Type AliasModelDescType
        {
            get;
            private set;
        }

        public Palette Palette
        {
            get;
            private set;
        }

        public BaseGraphics Graphics
        {
            get;
            private set;
        }

        public BaseTextureAtlas TextureAtlas
        {
            get;
            private set;
        }

        public VideoMode[] AvailableModes
        {
            get;
            protected set;
        }

        public VideoMode FirstAvailableMode
        {
            get;
            protected set;
        }

        public int ChosenMode
        {
            get;
            private set;
        }

        public virtual BaseTextureFilter[] TextureFilters
        {
            get;
            protected set;
        }

        public virtual BaseTextureBlendMode[] BlendModes
        {
            get;
            protected set;
        }

        public virtual BasePixelFormat[] PixelFormats
        {
            get;
            protected set;
        }

        protected VideoMode Mode
        {
            get;
            private set;
        }

        public bool SkipUpdate
        {
            get;
            set;
        }

        public bool BlockDrawing
        {
            get;
            set;
        }

        public BaseDevice(Type descType, Type graphicsType, Type textureAtlasType, Type modelType, Type modelDescType, Type aliasModelType, Type aliasModelDescType, Type textureType, Type textureDescType)
        {
            Desc = (BaseDeviceDesc)Activator.CreateInstance(descType);
            TextureType = textureType;
            TextureAtlasType = textureAtlasType;
            TextureDescType = textureDescType;
            ModelType = modelType;
            ModelDescType = modelDescType;
            AliasModelType = aliasModelType;
            AliasModelDescType = aliasModelDescType;
            Palette = new Palette(this);
            Graphics = (BaseGraphics)Activator.CreateInstance(graphicsType, this);
            TextureAtlas = (BaseTextureAtlas)Activator.CreateInstance(TextureAtlasType, this, DrawDef.MAX_SCRAPS, DrawDef.BLOCK_WIDTH, DrawDef.BLOCK_HEIGHT);
        }

        public virtual void Initialise(byte[] palette)
        {
            Graphics.Initialise();
            TextureAtlas.Initialise();

            GetAvailableModes();
            CheckCommandLineForOptions();

            // Console stuff was here

            Palette.CorrectGamma(palette);
            Palette.Initialise(palette);

            ChooseMode();

            ConsoleWrapper.Print("GL_VENDOR: {0}\n", Desc.Vendor);
            ConsoleWrapper.Print("GL_RENDERER: {0}\n", Desc.Renderer);
            ConsoleWrapper.Print("GL_VERSION: {0}\n", Desc.Version);
            ConsoleWrapper.Print("GL_EXTENSIONS: {0}\n", Desc.Extensions);

            // Multitexturing is a bit buggy, water doesn't work
            if (Desc.Extensions.Contains("GL_SGIS_multitexture ") /*|| Desc.Extensions.Contains( "GL_ARB_multitexture " ) */ && !CommandLine.HasParam("-nomtex"))
            {
                ConsoleWrapper.Print("Multitexture extensions found.\n");
                Desc.SupportsMultiTexture = true;
            }
        }

        public virtual void ResetMatrix()
        {
            throw new NotImplementedException();
        }

        public virtual void BeginScene()
        {
            Desc.ViewRect = new Rectangle(0, 0, Desc.ActualWidth, Desc.ActualHeight);
        }

        public virtual void EndScene()
        {
            if (!SkipUpdate || BlockDrawing)
            {
                Present();
            }
        }

        protected virtual void Present()
        {
            throw new NotImplementedException();
        }

        public virtual void Begin2DScene()
        {
            throw new NotImplementedException();
        }

        public virtual void End2DScene()
        {
            throw new NotImplementedException();
        }

        public virtual void Setup3DScene(bool cull, RefDef renderDef, bool isEnvMap)
        {
            throw new NotImplementedException();
        }

        public virtual void SetViewport(Rectangle rect)
        {
            SetViewport(rect.X, rect.Y, rect.Width, rect.Height);
        }

        public virtual void SetViewport(int x, int y, int width, int height)
        {
            throw new NotImplementedException();
        }

        public virtual BaseTextureFilter GetTextureFilters(string name)
        {
            return TextureFilters?.Where(tf => tf.Name == name).FirstOrDefault();
        }

        public virtual void SetTextureFilters(string name)
        {
            throw new NotImplementedException();
        }

        public virtual BaseTextureBlendMode GetBlendMode(string name)
        {
            return BlendModes?.Where(tf => tf.Name == name).FirstOrDefault();
        }

        public virtual void SetBlendMode(string name)
        {
            throw new NotImplementedException();
        }

        public virtual void SetDepth(float minimum, float maximum)
        {
            throw new NotImplementedException();
        }

        public virtual void SetZWrite(bool enable)
        {
            throw new NotImplementedException();
        }

        public virtual void SetDrawBuffer(bool isFront)
        {
            throw new NotImplementedException();
        }

        public virtual void Clear(bool zTrick, float clear)
        {
            throw new NotImplementedException();
        }

        ///<summary>
        /// Needed probably for GL only
        ///</summary>
        public virtual void Finish()
        {
            throw new NotImplementedException();
        }

        public virtual void SelectTexture(MTexTarget target)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// GL_DisableMultitexture
        /// </summary>
        public virtual void DisableMultitexture()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// GL_EnableMultitexture
        /// </summary>
        public virtual void EnableMultitexture()
        {
            throw new NotImplementedException();
        }

        // VID_SetMode (int modenum, unsigned char *palette)
        // sets the mode; only used by the Quake engine for resetting to mode 0 (the
        // base mode) on memory allocation failures
        public void SetMode(int index, byte[] palette)
        {
            if (index < 0 || index >= AvailableModes.Length)
            {
                Utilities.Error("Bad video mode\n");
            }

            var mode = AvailableModes[index];

            // Disable screen for loading was here            

            ChangeMode(mode);

            // Adjust conheight was here

            // Set aspect ratio
            Desc.AspectRatio = Desc.ActualWidth / (double)Desc.ActualHeight;
            Desc.Width = (int)(RendererDef.VIRTUAL_HEIGHT * Desc.AspectRatio);
            Desc.Height = (int)RendererDef.VIRTUAL_HEIGHT;

            // Set num pages

            // Resume screen and audio

            // Apply cvar

            // Clear all states

            //ConsoleWrapper.SafePrint( "Video mode {0} initialized.\n", GetModeDescription( _ModeNum ) );

            Palette.Initialise(palette);

            // vid.recalc_refdef = true;
        }

        /// <summary>
        /// VID_GetModeDescription
        /// </summary>
        public virtual string GetModeDescription(int mode)
        {
            if (mode < 0 || mode >= AvailableModes.Length)
            {
                return string.Empty;
            }

            var m = AvailableModes[mode];

            return string.Format("{0}x{1}x{2} {3}", m.Width, m.Height, m.BitsPerPixel, !Desc.IsFullScreen ? "windowed" : "fullscreen");
        }

        public virtual void ScreenShot(out string path)
        {
            path = null;

            //
            // find a file name to save it to
            //
            int i;
            for (i = 0; i <= 999; i++)
            {
                path = Path.Combine(FileSystem.GameDir, string.Format("quake{0:D3}.jpg", i));
                if (FileSystem.GetFileTime(path) == DateTime.MinValue)
                {
                    break;  // file doesn't exist
                }
            }
            if (i == 100)
            {
                ConsoleWrapper.Print("SCR_ScreenShot_f: Couldn't create a file\n");
            }
        }

        protected virtual void ChangeMode(VideoMode mode)
        {
            throw new NotImplementedException();
        }

        protected virtual void GetAvailableModes()
        {
            throw new NotImplementedException();
        }

        public virtual void PushMatrix()
        {
            throw new NotImplementedException();
        }

        public virtual void PopMatrix()
        {
            throw new NotImplementedException();
        }

        public virtual void RotateForEntity(Vector3 origin, Vector3 angles)
        {
            throw new NotImplementedException();
        }

        private void ChooseMode()
        {
            FirstAvailableMode.FullScreen = Desc.IsFullScreen;

            ChosenMode = -1;

            for (var i = 0; i < AvailableModes.Length; i++)
            {
                var m = AvailableModes[i];

                if (m.Width != FirstAvailableMode.Width
                    || m.Height != FirstAvailableMode.Height)
                {
                    continue;
                }

                ChosenMode = i;

                if (m.BitsPerPixel == FirstAvailableMode.BitsPerPixel
                    && m.RefreshRate == FirstAvailableMode.RefreshRate)
                {
                    break;
                }
            }

            if (ChosenMode == -1)
            {
                ChosenMode = 0;
            }

            Mode = AvailableModes[0];
        }

        private void CheckCommandLineForOptions()
        {
            var deviceWidth = FirstAvailableMode.Width;
            var deviceHeight = FirstAvailableMode.Height;

            int width = deviceWidth, height = deviceHeight;

            var i = CommandLine.CheckParm("-width");

            if (i > 0 && i < CommandLine.Argc - 1)
            {
                width = MathLib.AToI(CommandLine.Argv(i + 1));

                foreach (var res in AvailableModes)
                {
                    if (res.Width == width)
                    {
                        height = res.Height;
                        break;
                    }
                }
            }

            i = CommandLine.CheckParm("-height");

            if (i > 0 && i < CommandLine.Argc - 1)
            {
                height = MathLib.AToI(CommandLine.Argv(i + 1));
            }

            FirstAvailableMode.Width = width;
            FirstAvailableMode.Height = height;

            if (CommandLine.HasParam("-window"))
            {
                Desc.IsFullScreen = false;
            }
            else
            {
                Desc.IsFullScreen = true;

                if (CommandLine.HasParam("-current"))
                {
                    FirstAvailableMode.Width = deviceWidth;
                    FirstAvailableMode.Height = deviceHeight;
                }
                else
                {
                    var bpp = FirstAvailableMode.BitsPerPixel;

                    i = CommandLine.CheckParm("-bpp");

                    if (i > 0 && i < CommandLine.Argc - 1)
                    {
                        bpp = MathLib.AToI(CommandLine.Argv(i + 1));
                    }

                    FirstAvailableMode.BitsPerPixel = bpp;
                }
            }
        }

        public virtual void BlendedRotateForEntity(Vector3 origin, Vector3 angles, double realTime, ref Vector3 origin1, ref Vector3 origin2, ref float translateStartTime, ref Vector3 angles1, ref Vector3 angles2, ref float rotateStartTime)
        {
            throw new NotImplementedException();
        }
    }
}
