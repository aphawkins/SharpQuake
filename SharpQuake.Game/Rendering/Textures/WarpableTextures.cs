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

// gl_warp.c

namespace SharpQuake.Game.Rendering.Textures
{
    using System;
    using OpenTK;
    using SharpQuake.Framework;
    using SharpQuake.Framework.Definitions;
    using SharpQuake.Game.Rendering.Memory;
    using SharpQuake.Renderer;
    using SharpQuake.Renderer.Textures;

    public class WarpableTextures
    {
        private BaseTexture SolidSkyTexture
        {
            get;
            set;
        }

        private BaseTexture AlphaSkyTexture
        {
            get;
            set;
        }

        private BaseDevice Device
        {
            get;
            set;
        }

        private float SpeedScale
        {
            get;
            set;
        }

        public WarpableTextures(BaseDevice device)
        {
            Device = device;
        }

        /// <summary>
        /// R_InitSky
        /// called at level load
        /// A sky texture is 256*128, with the right side being a masked overlay
        /// </summary>
        public void InitSky(ModelTexture mt)
        {
            var src = mt.pixels;
            var offset = mt.offsets[0];

            // make an average value for the back to avoid
            // a fringe on the top level
            const int size = 128 * 128;
            var trans = new uint[size];
            var v8to24 = Device.Palette.Table8to24;
            var r = 0;
            var g = 0;
            var b = 0;
            var rgba = Union4b.Empty;
            for (var i = 0; i < 128; i++)
            {
                for (var j = 0; j < 128; j++)
                {
                    int p = src[offset + (i * 256) + j + 128];
                    rgba.ui0 = v8to24[p];
                    trans[(i * 128) + j] = rgba.ui0;
                    r += rgba.b0;
                    g += rgba.b1;
                    b += rgba.b2;
                }
            }

            rgba.b0 = (byte)(r / size);
            rgba.b1 = (byte)(g / size);
            rgba.b2 = (byte)(b / size);
            rgba.b3 = 0;

            var transpix = rgba.ui0;

            SolidSkyTexture = BaseTexture.FromBuffer(Device, "_SolidSkyTexture", trans, 128, 128, false, false, "GL_LINEAR");

            for (var i = 0; i < 128; i++)
            {
                for (var j = 0; j < 128; j++)
                {
                    int p = src[offset + (i * 256) + j];
                    trans[(i * 128) + j] = p == 0 ? transpix : v8to24[p];
                }
            }

            AlphaSkyTexture = BaseTexture.FromBuffer(Device, "_AlphaSkyTexture", trans, 128, 128, false, true, "GL_LINEAR");
        }


        /// <summary>
        /// EmitWaterPolys
        /// Does a water warp on the pre-fragmented glpoly_t chain
        /// </summary>
        public void EmitWaterPolys(double realTime, MemorySurface fa)
        {
            Device.Graphics.EmitWaterPolys(ref WarpDef._TurbSin, realTime, WarpDef.TURBSCALE, fa.polys);
        }

        /// <summary>
        /// R_DrawSkyChain
        /// </summary>
        public void DrawSkyChain(double realTime, Vector3 origin, MemorySurface s)
        {
            Device.DisableMultitexture();

            SolidSkyTexture.Bind();

            // used when gl_texsort is on
            SpeedScale = (float)realTime * 8;
            SpeedScale -= (int)SpeedScale & ~127;

            for (var fa = s; fa != null; fa = fa.texturechain)
            {
                Device.Graphics.EmitSkyPolys(fa.polys, origin, SpeedScale);
            }

            AlphaSkyTexture.Bind();
            SpeedScale = (float)realTime * 16;
            SpeedScale -= (int)SpeedScale & ~127;

            for (var fa = s; fa != null; fa = fa.texturechain)
            {
                Device.Graphics.EmitSkyPolys(fa.polys, origin, SpeedScale, true);
            }
        }

        /// <summary>
        /// EmitBothSkyLayers
        /// Does a sky warp on the pre-fragmented glpoly_t chain
        /// This will be called for brushmodels, the world
        /// will have them chained together.
        /// </summary>
        public void EmitBothSkyLayers(double realTime, Vector3 origin, MemorySurface fa)
        {
            Device.DisableMultitexture();

            SolidSkyTexture.Bind();
            SpeedScale = (float)realTime * 8;
            SpeedScale -= (int)SpeedScale & ~127;

            Device.Graphics.EmitSkyPolys(fa.polys, origin, SpeedScale);

            AlphaSkyTexture.Bind();
            SpeedScale = (float)realTime * 16;
            SpeedScale -= (int)SpeedScale & ~127;

            Device.Graphics.EmitSkyPolys(fa.polys, origin, SpeedScale, true);
        }
    }
}
