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

// gl_rsurf.c

namespace SharpQuake
{
    using System;
    using System.Linq;
    using OpenTK;
    using SharpQuake.Framework;
    using SharpQuake.Framework.IO.BSP;
    using SharpQuake.Game.Data.Models;
    using SharpQuake.Game.Rendering.Memory;
    using SharpQuake.Game.Rendering.Textures;
    using SharpQuake.Game.World;
    using SharpQuake.Renderer;
    using SharpQuake.Renderer.OpenGL.Textures;
    using SharpQuake.Renderer.Textures;
    using SharpQuake.Rendering;

    public partial class Render
    {
        private const double COLINEAR_EPSILON = 0.001;

        //private Int32 _LightMapTextures; // lightmap_textures
        private int _LightMapBytes; // lightmap_bytes		// 1, 2, or 4
        private MemoryVertex[] _CurrentVertBase; // r_pcurrentvertbase
        private ModelData _CurrentModel; // currentmodel
                                         //private System.Boolean[] _LightMapModified = new System.Boolean[RenderDef.MAX_LIGHTMAPS]; // lightmap_modified
        private readonly GLPoly[] _LightMapPolys = new GLPoly[RenderDef.MAX_LIGHTMAPS]; // lightmap_polys
                                                                               //private glRect_t[] _LightMapRectChange = new glRect_t[RenderDef.MAX_LIGHTMAPS]; // lightmap_rectchange
        private readonly uint[] _BlockLights = new uint[18 * 18]; // blocklights

        private readonly Entity _TempEnt = new(); // for DrawWorld

        // the lightmap texture data needs to be kept in
        // main memory so texsubimage can update properly
        private readonly byte[] _LightMaps = new byte[4 * RenderDef.MAX_LIGHTMAPS * RenderDef.BLOCK_WIDTH * RenderDef.BLOCK_HEIGHT]; // lightmaps

        private BaseTexture LightMapTexture
        {
            get;
            set;
        }

        protected TextureChains TextureChains
        {
            get;
            set;
        }

        /// <summary>
        /// GL_BuildLightmaps
        /// Builds the lightmap texture with all the surfaces from all brush models
        /// </summary>
        private void BuildLightMaps()
        {
            if (LightMapTexture != null)
            {
                Array.Clear(LightMapTexture.LightMapData, 0, LightMapTexture.LightMapData.Length);
            }
            //memset (allocated, 0, sizeof(allocated));

            _FrameCount = 1;        // no dlightcache

            //if( _LightMapTextures == 0 )
            //   _LightMapTextures = Host.DrawingContext.GenerateTextureNumberRange( RenderDef.MAX_LIGHTMAPS );

            Host.DrawingContext.LightMapFormat = "GL_LUMINANCE";

            // default differently on the Permedia
            if (Host.Screen.IsPermedia)
            {
                Host.DrawingContext.LightMapFormat = "GL_RGBA";
            }

            if (CommandLine.HasParam("-lm_1"))
            {
                Host.DrawingContext.LightMapFormat = "GL_LUMINANCE";
            }

            if (CommandLine.HasParam("-lm_a"))
            {
                Host.DrawingContext.LightMapFormat = "GL_ALPHA";
            }

            //if (CommandLine.HasParam("-lm_i"))
            //    Host.DrawingContext.LightMapFormat = PixelFormat.Intensity;

            //if (CommandLine.HasParam("-lm_2"))
            //    Host.DrawingContext.LightMapFormat = PixelFormat.Rgba4;

            if (CommandLine.HasParam("-lm_4"))
            {
                Host.DrawingContext.LightMapFormat = "GL_RGBA";
            }

            switch (Host.DrawingContext.LightMapFormat)
            {
                case "GL_RGBA":
                    _LightMapBytes = 4;
                    break;

                //case PixelFormat.Rgba4:
                //_LightMapBytes = 2;
                //break;

                case "GL_LUMINANCE":
                //case PixelFormat.Intensity:
                case "GL_ALPHA":
                    _LightMapBytes = 1;
                    break;
            }

            var tempBuffer = new int[RenderDef.MAX_LIGHTMAPS, RenderDef.BLOCK_WIDTH];
            var brushes = Host.Client.Cl.model_precache.Where(m => m is BrushModelData).ToArray();

            //for ( var j = 1; j < QDef.MAX_MODELS; j++ )
            for (var j = 0; j < brushes.Length; j++)
            {
                var m = (BrushModelData)brushes[j];
                if (m == null)
                {
                    break;
                }

                if (m.Name != null && m.Name.StartsWith("*"))
                {
                    continue;
                }

                _CurrentVertBase = m.Vertices;
                _CurrentModel = m;
                for (var i = 0; i < m.NumSurfaces; i++)
                {
                    CreateSurfaceLightmap(ref tempBuffer, m.Surfaces[i]);
                    if ((m.Surfaces[i].flags & (int)Q1SurfaceFlags.Turbulence) != 0)
                    {
                        continue;
                    }

                    if ((m.Surfaces[i].flags & (int)Q1SurfaceFlags.Sky) != 0)
                    {
                        continue;
                    }

                    BuildSurfaceDisplayList(m.Surfaces[i]);
                }
            }

            if (!Host.Cvars.glTexSort.Get<bool>())
            {
                Host.DrawingContext.SelectTexture(MTexTarget.TEXTURE1_SGIS);
            }

            LightMapTexture = BaseTexture.FromBuffer(Host.Video.Device, "_Lightmaps", new ByteArraySegment(_LightMaps), 128, 128, false, false, isLightMap: true);

            LightMapTexture.Desc.LightMapBytes = _LightMapBytes;
            LightMapTexture.Desc.LightMapFormat = Host.DrawingContext.LightMapFormat;

            Array.Copy(tempBuffer, LightMapTexture.LightMapData, tempBuffer.Length);

            LightMapTexture.UploadLightmap();

            if (!Host.Cvars.glTexSort.Get<bool>())
            {
                Host.DrawingContext.SelectTexture(MTexTarget.TEXTURE0_SGIS);
            }
        }

        /// <summary>
        /// GL_CreateSurfaceLightmap
        /// </summary>
        private void CreateSurfaceLightmap(ref int[,] tempBuffer, MemorySurface surf)
        {
            if ((surf.flags & ((int)Q1SurfaceFlags.Sky | (int)Q1SurfaceFlags.Turbulence)) != 0)
            {
                return;
            }

            var smax = (surf.extents[0] >> 4) + 1;
            var tmax = (surf.extents[1] >> 4) + 1;

            surf.lightmaptexturenum = AllocBlock(ref tempBuffer, smax, tmax, ref surf.light_s, ref surf.light_t);
            var offset = surf.lightmaptexturenum * _LightMapBytes * RenderDef.BLOCK_WIDTH * RenderDef.BLOCK_HEIGHT;
            offset += ((surf.light_t * RenderDef.BLOCK_WIDTH) + surf.light_s) * _LightMapBytes;
            BuildLightMap(surf, new ByteArraySegment(_LightMaps, offset), RenderDef.BLOCK_WIDTH * _LightMapBytes);
        }

        /// <summary>
        /// BuildSurfaceDisplayList
        /// </summary>
        private void BuildSurfaceDisplayList(MemorySurface fa)
        {
            var BrushModelData = (BrushModelData)_CurrentModel;
            // reconstruct the polygon
            var pedges = BrushModelData.Edges;
            var lnumverts = fa.numedges;

            //
            // draw texture
            //
            var poly = new GLPoly();
            poly.AllocVerts(lnumverts);
            poly.next = fa.polys;
            poly.flags = fa.flags;
            fa.polys = poly;

            ushort[] r_pedge_v;
            Vector3 vec;

            for (var i = 0; i < lnumverts; i++)
            {
                var lindex = BrushModelData.SurfEdges[fa.firstedge + i];
                if (lindex > 0)
                {
                    r_pedge_v = pedges[lindex].v;
                    vec = _CurrentVertBase[r_pedge_v[0]].position;
                }
                else
                {
                    r_pedge_v = pedges[-lindex].v;
                    vec = _CurrentVertBase[r_pedge_v[1]].position;
                }
                var s = MathLib.DotProduct(ref vec, ref fa.texinfo.vecs[0]) + fa.texinfo.vecs[0].W;
                s /= fa.texinfo.texture.width;

                var t = MathLib.DotProduct(ref vec, ref fa.texinfo.vecs[1]) + fa.texinfo.vecs[1].W;
                t /= fa.texinfo.texture.height;

                poly.verts[i][0] = vec.X;
                poly.verts[i][1] = vec.Y;
                poly.verts[i][2] = vec.Z;
                poly.verts[i][3] = s;
                poly.verts[i][4] = t;

                //
                // lightmap texture coordinates
                //
                s = MathLib.DotProduct(ref vec, ref fa.texinfo.vecs[0]) + fa.texinfo.vecs[0].W;
                s -= fa.texturemins[0];
                s += fa.light_s * 16;
                s += 8;
                s /= RenderDef.BLOCK_WIDTH * 16;

                t = MathLib.DotProduct(ref vec, ref fa.texinfo.vecs[1]) + fa.texinfo.vecs[1].W;
                t -= fa.texturemins[1];
                t += fa.light_t * 16;
                t += 8;
                t /= RenderDef.BLOCK_HEIGHT * 16;

                poly.verts[i][5] = s;
                poly.verts[i][6] = t;
            }

            //
            // remove co-linear points - Ed
            //
            if (!Host.Cvars.glKeepTJunctions.Get<bool>() && (fa.flags & (int)Q1SurfaceFlags.Underwater) == 0)
            {
                for (var i = 0; i < lnumverts; ++i)
                {
                    if (Utilities.IsCollinear(poly.verts[(i + lnumverts - 1) % lnumverts],
                        poly.verts[i],
                        poly.verts[(i + 1) % lnumverts]))
                    {
                        int j;
                        for (j = i + 1; j < lnumverts; ++j)
                        {
                            //int k;
                            for (var k = 0; k < ModelDef.VERTEXSIZE; ++k)
                            {
                                poly.verts[j - 1][k] = poly.verts[j][k];
                            }
                        }
                        --lnumverts;
                        // retry next vertex next time, which is now current vertex
                        --i;
                    }
                }
            }
            poly.numverts = lnumverts;
        }

        // returns a texture number and the position inside it
        private static int AllocBlock(ref int[,] data, int w, int h, ref int x, ref int y)
        {
            for (var texnum = 0; texnum < RenderDef.MAX_LIGHTMAPS; texnum++)
            {
                var best = RenderDef.BLOCK_HEIGHT;

                for (var i = 0; i < RenderDef.BLOCK_WIDTH - w; i++)
                {
                    int j;
                    int best2 = 0;

                    for (j = 0; j < w; j++)
                    {
                        if (data[texnum, i + j] >= best)
                        {
                            break;
                        }

                        if (data[texnum, i + j] > best2)
                        {
                            best2 = data[texnum, i + j];
                        }
                    }

                    if (j == w)
                    {
                        // this is a valid spot
                        x = i;
                        y = best = best2;
                    }
                }

                if (best + h > RenderDef.BLOCK_HEIGHT)
                {
                    continue;
                }

                for (var i = 0; i < w; i++)
                {
                    data[texnum, x + i] = best + h;
                }

                return texnum;
            }

            Utilities.Error("AllocBlock: full");
            return 0; // shut up compiler
        }

        /// <summary>
        /// R_BuildLightMap
        /// Combine and scale multiple lightmaps into the 8.8 format in blocklights
        /// </summary>
        private void BuildLightMap(MemorySurface surf, ByteArraySegment dest, int stride)
        {
            surf.cached_dlight = surf.dlightframe == _FrameCount;

            var smax = (surf.extents[0] >> 4) + 1;
            var tmax = (surf.extents[1] >> 4) + 1;
            var size = smax * tmax;

            var srcOffset = surf.sampleofs;
            var lightmap = surf.sample_base;// surf.samples;

            // set to full bright if no light data
            if (Host.Cvars.FullBright.Get<bool>() || Host.Client.Cl.worldmodel.LightData == null)
            {
                for (var i = 0; i < size; i++)
                {
                    _BlockLights[i] = 255 * 256;
                }
            }
            else
            {
                // clear to no light
                for (var i = 0; i < size; i++)
                {
                    _BlockLights[i] = 0;
                }

                // add all the lightmaps
                if (lightmap != null)
                {
                    for (var maps = 0; maps < BspDef.MAXLIGHTMAPS && surf.styles[maps] != 255; maps++)
                    {
                        var scale = _LightStyleValue[surf.styles[maps]];
                        surf.cached_light[maps] = scale;    // 8.8 fraction
                        for (var i = 0; i < size; i++)
                        {
                            _BlockLights[i] += (uint)(lightmap[srcOffset + i] * scale);
                        }

                        srcOffset += size; // lightmap += size;	// skip to next lightmap
                    }
                }

                // add all the dynamic lights
                if (surf.dlightframe == _FrameCount)
                {
                    AddDynamicLights(surf);
                }
            }
            // bound, invert, and shift
            //store:
            var blOffset = 0;
            var destOffset = dest.StartIndex;
            var data = dest.Data;
            switch (Host.DrawingContext.LightMapFormat)
            {
                case "GL_RGBA":
                    stride -= smax << 2;
                    for (var i = 0; i < tmax; i++, destOffset += stride) // dest += stride
                    {
                        for (var j = 0; j < smax; j++)
                        {
                            var t = _BlockLights[blOffset++];// *bl++;
                            t >>= 7;
                            if (t > 255)
                            {
                                t = 255;
                            }

                            data[destOffset + 3] = (byte)(255 - t); //dest[3] = 255 - t;
                            destOffset += 4;
                        }
                    }
                    break;

                case "GL_ALPHA":
                case "GL_LUMINANCE":
                    //case GL_INTENSITY:
                    for (var i = 0; i < tmax; i++, destOffset += stride)
                    {
                        for (var j = 0; j < smax; j++)
                        {
                            var t = _BlockLights[blOffset++];// *bl++;
                            t >>= 7;
                            if (t > 255)
                            {
                                t = 255;
                            }

                            data[destOffset + j] = (byte)(255 - t); // dest[j] = 255 - t;
                        }
                    }
                    break;

                default:
                    Utilities.Error("Bad lightmap format");
                    break;
            }
        }

        /// <summary>
        /// R_AddDynamicLights
        /// </summary>
        private void AddDynamicLights(MemorySurface surf)
        {
            var smax = (surf.extents[0] >> 4) + 1;
            var tmax = (surf.extents[1] >> 4) + 1;
            var tex = surf.texinfo;
            var dlights = Host.Client.DLights;

            for (var lnum = 0; lnum < ClientDef.MAX_DLIGHTS; lnum++)
            {
                if ((surf.dlightbits & (1 << lnum)) == 0)
                {
                    continue;       // not lit by this light
                }

                var rad = dlights[lnum].radius;
                var dist = Vector3.Dot(dlights[lnum].origin, surf.plane.normal) - surf.plane.dist;
                rad -= Math.Abs(dist);
                var minlight = dlights[lnum].minlight;
                if (rad < minlight)
                {
                    continue;
                }

                minlight = rad - minlight;

                var impact = dlights[lnum].origin - (surf.plane.normal * dist);

                var local0 = Vector3.Dot(impact, tex.vecs[0].Xyz) + tex.vecs[0].W;
                var local1 = Vector3.Dot(impact, tex.vecs[1].Xyz) + tex.vecs[1].W;

                local0 -= surf.texturemins[0];
                local1 -= surf.texturemins[1];

                for (var t = 0; t < tmax; t++)
                {
                    var td = (int)(local1 - (t * 16));
                    if (td < 0)
                    {
                        td = -td;
                    }

                    for (var s = 0; s < smax; s++)
                    {
                        var sd = (int)(local0 - (s * 16));
                        if (sd < 0)
                        {
                            sd = -sd;
                        }

                        dist = sd > td ? sd + (td >> 1) : td + (sd >> 1);
                        if (dist < minlight)
                        {
                            _BlockLights[(t * smax) + s] += (uint)((rad - dist) * 256);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// R_DrawWaterSurfaces
        /// </summary>
        private void DrawWaterSurfaces()
        {
            if (Host.Cvars.WaterAlpha.Get<float>() == 1.0f && Host.Cvars.glTexSort.Get<bool>())
            {
                return;
            }

            //
            // go back to the world matrix
            //
            Host.Video.Device.ResetMatrix();

            // WaterAlpha is broken - will fix when we introduce GLSL...
            //if ( _WaterAlpha.Value < 1.0 )
            //{
            //    GL.Enable( EnableCap.Blend );
            //    GL.Color4( 1, 1, 1, _WaterAlpha.Value );
            //    GL.TexEnv( TextureEnvTarget.TextureEnv, TextureEnvParameter.TextureEnvMode, ( Int32 ) TextureEnvMode.Modulate );
            //}

            if (!Host.Cvars.glTexSort.Get<bool>())
            {
                if (TextureChains.WaterChain == null)
                {
                    return;
                }

                for (var s = TextureChains.WaterChain; s != null; s = s.texturechain)
                {
                    s.texinfo.texture.texture.Bind();
                    WarpableTextures.EmitWaterPolys(Host.RealTime, s);
                }
                TextureChains.WaterChain = null;
            }
            else
            {
                for (var i = 0; i < Host.Client.Cl.worldmodel.NumTextures; i++)
                {
                    var t = Host.Client.Cl.worldmodel.Textures[i];
                    if (t == null)
                    {
                        continue;
                    }

                    var s = t.texturechain;
                    if (s == null)
                    {
                        continue;
                    }

                    if ((s.flags & (int)Q1SurfaceFlags.Turbulence) == 0)
                    {
                        continue;
                    }

                    // set modulate mode explicitly

                    t.texture.Bind();

                    for (; s != null; s = s.texturechain)
                    {
                        WarpableTextures.EmitWaterPolys(Host.RealTime, s);
                    }

                    t.texturechain = null;
                }
            }

            // WaterAlpha is broken - will fix when we introduce GLSL...
            //if( _WaterAlpha.Value < 1.0 )
            //{
            //    GL.TexEnv( TextureEnvTarget.TextureEnv, TextureEnvParameter.TextureEnvMode, ( Int32 ) TextureEnvMode.Replace );
            //    GL.Color4( 1f, 1, 1, 1 );
            //    GL.Disable( EnableCap.Blend );
            //}
        }

        /// <summary>
        /// R_DrawWorld
        /// </summary>
        private void DrawWorld()
        {
            _TempEnt.Clear();
            _TempEnt.model = Host.Client.Cl.worldmodel;

            _ModelOrg = RefDef.vieworg;
            _CurrentEntity = _TempEnt;
            Host.DrawingContext.CurrentTexture = -1;

            Array.Clear(_LightMapPolys, 0, _LightMapPolys.Length);

            RecursiveWorldNode(((BrushModelData)_TempEnt.model).Nodes[0]);

            DrawTextureChains();

            BlendLightmaps();
        }

        /// <summary>
        /// R_BlendLightmaps
        /// </summary>
        private void BlendLightmaps()
        {
            if (Host.Cvars.FullBright.Get<bool>())
            {
                return;
            }

            if (!Host.Cvars.glTexSort.Get<bool>())
            {
                return;
            }

            Host.Video.Device.Graphics.BeginBlendLightMap(!Host.Cvars.LightMap.Get<bool>(), Host.DrawingContext.LightMapFormat);

            for (var i = 0; i < RenderDef.MAX_LIGHTMAPS; i++)
            {
                var p = _LightMapPolys[i];
                if (p == null)
                {
                    continue;
                }

                LightMapTexture.BindLightmap(((GLTextureDesc)LightMapTexture.Desc).TextureNumber + i);

                if (LightMapTexture.LightMapModified[i])
                {
                    CommitLightmap(i);
                }

                for (; p != null; p = p.chain)
                {
                    if ((p.flags & (int)Q1SurfaceFlags.Underwater) != 0)
                    {
                        Host.Video.Device.Graphics.DrawWaterPolyLightmap(p, Host.RealTime);
                    }
                    else
                    {
                        Host.Video.Device.Graphics.DrawPoly(p, isLightmap: true);
                    }
                }
            }

            Host.Video.Device.Graphics.EndBlendLightMap(!Host.Cvars.LightMap.Get<bool>(), Host.DrawingContext.LightMapFormat);
        }

        private void DrawTextureChains()
        {
            if (!Host.Cvars.glTexSort.Get<bool>())
            {
                Host.Video.Device.DisableMultitexture();

                if (TextureChains.SkyChain != null)
                {
                    WarpableTextures.DrawSkyChain(Host.RealTime, Host.RenderContext.Origin, TextureChains.SkyChain);
                    TextureChains.SkyChain = null;
                }
                return;
            }
            var world = Host.Client.Cl.worldmodel;
            for (var i = 0; i < world.NumTextures; i++)
            {
                var t = world.Textures[i];
                if (t == null)
                {
                    continue;
                }

                var s = t.texturechain;
                if (s == null)
                {
                    continue;
                }

                if (i == _SkyTextureNum)
                {
                    WarpableTextures.DrawSkyChain(Host.RealTime, Host.RenderContext.Origin, s);
                }
                //else if( i == _MirrorTextureNum && _MirrorAlpha.Value != 1.0f )
                //{
                //    MirrorChain( s );
                //    continue;
                //}
                else
                {
                    if ((s.flags & (int)Q1SurfaceFlags.Turbulence) != 0 && Host.Cvars.WaterAlpha.Get<float>() != 1.0f)
                    {
                        continue;   // draw translucent water later
                    }

                    for (; s != null; s = s.texturechain)
                    {
                        RenderBrushPoly(s);
                    }
                }

                t.texturechain = null;
            }
        }

        /// <summary>
        /// R_RenderBrushPoly
        /// </summary>
        private void RenderBrushPoly(MemorySurface fa)
        {
            _BrushPolys++;

            if ((fa.flags & (int)Q1SurfaceFlags.Sky) != 0)
            {   // warp texture, no lightmaps
                WarpableTextures.EmitBothSkyLayers(Host.RealTime, Host.RenderContext.Origin, fa);
                return;
            }

            var t = TextureAnimation(fa.texinfo.texture);
            t.texture.Bind();

            if ((fa.flags & (int)Q1SurfaceFlags.Turbulence) != 0)
            {   // warp texture, no lightmaps
                WarpableTextures.EmitWaterPolys(Host.RealTime, fa);
                return;
            }

            if ((fa.flags & (int)Q1SurfaceFlags.Underwater) != 0)
            {
                Host.Video.Device.Graphics.DrawWaterPoly(fa.polys, Host.RealTime);
            }
            else
            {
                Host.Video.Device.Graphics.DrawPoly(fa.polys, t.scaleX, t.scaleY);
            }

            // add the poly to the proper lightmap chain

            fa.polys.chain = _LightMapPolys[fa.lightmaptexturenum];
            _LightMapPolys[fa.lightmaptexturenum] = fa.polys;

            // check for lightmap modification
            var modified = false;
            for (var maps = 0; maps < BspDef.MAXLIGHTMAPS && fa.styles[maps] != 255; maps++)
            {
                if (_LightStyleValue[fa.styles[maps]] != fa.cached_light[maps])
                {
                    modified = true;
                    break;
                }
            }

            if (modified ||
                fa.dlightframe == _FrameCount ||    // dynamic this frame
                fa.cached_dlight)          // dynamic previously
            {
                if (Host.Cvars.Dynamic.Get<bool>())
                {
                    LightMapTexture.LightMapModified[fa.lightmaptexturenum] = true;
                    UpdateRect(fa, ref LightMapTexture.LightMapRectChange[fa.lightmaptexturenum]);
                    var offset = fa.lightmaptexturenum * _LightMapBytes * RenderDef.BLOCK_WIDTH * RenderDef.BLOCK_HEIGHT;
                    offset += (fa.light_t * RenderDef.BLOCK_WIDTH * _LightMapBytes) + (fa.light_s * _LightMapBytes);
                    BuildLightMap(fa, new ByteArraySegment(_LightMaps, offset), RenderDef.BLOCK_WIDTH * _LightMapBytes);
                }
            }
        }

        private static void UpdateRect(MemorySurface fa, ref GLRect theRect)
        {
            if (fa.light_t < theRect.t)
            {
                if (theRect.h != 0)
                {
                    theRect.h += (byte)(theRect.t - fa.light_t);
                }

                theRect.t = (byte)fa.light_t;
            }
            if (fa.light_s < theRect.l)
            {
                if (theRect.w != 0)
                {
                    theRect.w += (byte)(theRect.l - fa.light_s);
                }

                theRect.l = (byte)fa.light_s;
            }
            var smax = (fa.extents[0] >> 4) + 1;
            var tmax = (fa.extents[1] >> 4) + 1;
            if ((theRect.w + theRect.l) < (fa.light_s + smax))
            {
                theRect.w = (byte)(fa.light_s - theRect.l + smax);
            }

            if ((theRect.h + theRect.t) < (fa.light_t + tmax))
            {
                theRect.h = (byte)(fa.light_t - theRect.t + tmax);
            }
        }

        /// <summary>
        /// R_MirrorChain
        /// </summary>
        //private void MirrorChain( MemorySurface s )
        //{
        //    if( _IsMirror )
        //        return;
        //    _IsMirror = true;
        //    _MirrorPlane = s.plane;
        //}

        /// <summary>
        /// R_RecursiveWorldNode
        /// </summary>
        private void RecursiveWorldNode(MemoryNodeBase node)
        {
            Occlusion.RecursiveWorldNode(node, _ModelOrg, _FrameCount, ref _Frustum, (surf) => DrawSequentialPoly(surf), (efrags) => StoreEfrags(efrags));
        }

        /// <summary>
        /// R_DrawSequentialPoly
        /// Systems that have fast state and texture changes can
        /// just do everything as it passes with no need to sort
        /// </summary>
        private void DrawSequentialPoly(MemorySurface s)
        {
            //
            // normal lightmaped poly
            //
            if ((s.flags & ((int)Q1SurfaceFlags.Sky | (int)Q1SurfaceFlags.Turbulence | (int)Q1SurfaceFlags.Underwater)) == 0)
            {
                RenderDynamicLightmaps(s);
                var p = s.polys;
                var t = TextureAnimation(s.texinfo.texture);
                if (Host.Video.Device.Desc.SupportsMultiTexture)
                {
                    Host.Video.Device.Graphics.DrawSequentialPolyMultiTexture(t.texture, LightMapTexture, _LightMaps, p, s.lightmaptexturenum);
                    return;
                }
                else
                {
                    Host.Video.Device.Graphics.DrawSequentialPoly(t.texture, LightMapTexture, p, s.lightmaptexturenum);
                }

                return;
            }

            //
            // subdivided water surface warp
            //

            if ((s.flags & (int)Q1SurfaceFlags.Turbulence) != 0)
            {
                Host.Video.Device.DisableMultitexture();
                s.texinfo.texture.texture.Bind();
                WarpableTextures.EmitWaterPolys(Host.RealTime, s);
                return;
            }

            //
            // subdivided sky warp
            //
            if ((s.flags & (int)Q1SurfaceFlags.Sky) != 0)
            {
                WarpableTextures.EmitBothSkyLayers(Host.RealTime, Host.RenderContext.Origin, s);
                return;
            }

            //
            // underwater warped with lightmap
            //
            RenderDynamicLightmaps(s);
            if (Host.Video.Device.Desc.SupportsMultiTexture)
            {
                var t = TextureAnimation(s.texinfo.texture);

                Host.DrawingContext.SelectTexture(MTexTarget.TEXTURE0_SGIS);

                Host.Video.Device.Graphics.DrawWaterPolyMultiTexture(_LightMaps, t.texture, LightMapTexture, s.lightmaptexturenum, s.polys, Host.RealTime);
            }
            else
            {
                var p = s.polys;

                var t = TextureAnimation(s.texinfo.texture);
                t.texture.Bind();
                Host.Video.Device.Graphics.DrawWaterPoly(p, Host.RealTime);

                LightMapTexture.BindLightmap(((GLTextureDesc)LightMapTexture.Desc).TextureNumber + s.lightmaptexturenum);
                Host.Video.Device.Graphics.DrawWaterPolyLightmap(p, Host.RealTime, true);
            }
        }

        private void CommitLightmap(int i)
        {
            LightMapTexture.CommitLightmap(_LightMaps, i);
        }

        /// <summary>
        /// R_TextureAnimation
        /// Returns the proper texture for a given time and base texture
        /// </summary>
        private ModelTexture TextureAnimation(ModelTexture t)
        {
            if (_CurrentEntity.frame != 0)
            {
                if (t.alternate_anims != null)
                {
                    t = t.alternate_anims;
                }
            }

            if (t.anim_total == 0)
            {
                return t;
            }

            var reletive = (int)(Host.Client.Cl.time * 10) % t.anim_total;
            var count = 0;
            while (t.anim_min > reletive || t.anim_max <= reletive)
            {
                t = t.anim_next;
                if (t == null)
                {
                    Utilities.Error("R_TextureAnimation: broken cycle");
                }

                if (++count > 100)
                {
                    Utilities.Error("R_TextureAnimation: infinite cycle");
                }
            }

            return t;
        }

        /// <summary>
        /// R_RenderDynamicLightmaps
        /// Multitexture
        /// </summary>
        private void RenderDynamicLightmaps(MemorySurface fa)
        {
            _BrushPolys++;

            if ((fa.flags & ((int)Q1SurfaceFlags.Sky | (int)Q1SurfaceFlags.Turbulence)) != 0)
            {
                return;
            }

            fa.polys.chain = _LightMapPolys[fa.lightmaptexturenum];
            _LightMapPolys[fa.lightmaptexturenum] = fa.polys;

            // check for lightmap modification
            var flag = false;
            for (var maps = 0; maps < BspDef.MAXLIGHTMAPS && fa.styles[maps] != 255; maps++)
            {
                if (_LightStyleValue[fa.styles[maps]] != fa.cached_light[maps])
                {
                    flag = true;
                    break;
                }
            }

            if (flag ||
                fa.dlightframe == _FrameCount || // dynamic this frame
                fa.cached_dlight)  // dynamic previously
            {
                if (Host.Cvars.Dynamic.Get<bool>())
                {
                    LightMapTexture.LightMapModified[fa.lightmaptexturenum] = true;
                    UpdateRect(fa, ref LightMapTexture.LightMapRectChange[fa.lightmaptexturenum]);
                    var offset = (fa.lightmaptexturenum * _LightMapBytes * RenderDef.BLOCK_WIDTH * RenderDef.BLOCK_HEIGHT) +
                        (fa.light_t * RenderDef.BLOCK_WIDTH * _LightMapBytes) + (fa.light_s * _LightMapBytes);
                    BuildLightMap(fa, new ByteArraySegment(_LightMaps, offset), RenderDef.BLOCK_WIDTH * _LightMapBytes);
                }
            }
        }

        /// <summary>
        /// R_DrawBrushModel
        /// </summary>
        private void DrawBrushModel(Entity e)
        {
            _CurrentEntity = e;
            Host.DrawingContext.CurrentTexture = -1;

            var clmodel = (BrushModelData)e.model;
            var rotated = false;
            Vector3 mins, maxs;
            if (e.angles.X != 0 || e.angles.Y != 0 || e.angles.Z != 0)
            {
                rotated = true;
                mins = e.origin;
                mins.X -= clmodel.Radius;
                mins.Y -= clmodel.Radius;
                mins.Z -= clmodel.Radius;
                maxs = e.origin;
                maxs.X += clmodel.Radius;
                maxs.Y += clmodel.Radius;
                maxs.Z += clmodel.Radius;
            }
            else
            {
                mins = e.origin + clmodel.BoundsMin;
                maxs = e.origin + clmodel.BoundsMax;
            }

            if (Utilities.CullBox(ref mins, ref maxs, ref _Frustum))
            {
                return;
            }

            Array.Clear(_LightMapPolys, 0, _LightMapPolys.Length);
            _ModelOrg = RefDef.vieworg - e.origin;
            if (rotated)
            {
                var temp = _ModelOrg;
                MathLib.AngleVectors(ref e.angles, out Vector3 forward, out Vector3 right, out Vector3 up);
                _ModelOrg.X = Vector3.Dot(temp, forward);
                _ModelOrg.Y = -Vector3.Dot(temp, right);
                _ModelOrg.Z = Vector3.Dot(temp, up);
            }

            // calculate dynamic lighting for bmodel if it's not an
            // instanced model
            if (clmodel.FirstModelSurface != 0 && !Host.Cvars.glFlashBlend.Get<bool>())
            {
                for (var k = 0; k < ClientDef.MAX_DLIGHTS; k++)
                {
                    if ((Host.Client.DLights[k].die < Host.Client.Cl.time) || (Host.Client.DLights[k].radius == 0))
                    {
                        continue;
                    }

                    MarkLights(Host.Client.DLights[k], 1 << k, clmodel.Nodes[clmodel.Hulls[0].firstclipnode]);
                }
            }

            Host.Video.Device.PushMatrix();
            e.angles.X = -e.angles.X;   // stupid quake bug
            Host.Video.Device.RotateForEntity(e.origin, e.angles);
            e.angles.X = -e.angles.X;   // stupid quake bug

            var surfOffset = clmodel.FirstModelSurface;
            var psurf = clmodel.Surfaces; //[clmodel.firstmodelsurface];

            //
            // draw texture
            //
            for (var i = 0; i < clmodel.NumModelSurfaces; i++, surfOffset++)
            {
                // find which side of the node we are on
                var pplane = psurf[surfOffset].plane;

                var dot = Vector3.Dot(_ModelOrg, pplane.normal) - pplane.dist;

                // draw the polygon
                var planeBack = (psurf[surfOffset].flags & (int)Q1SurfaceFlags.PlaneBack) != 0;
                if ((planeBack && (dot < -QDef.BACKFACE_EPSILON)) || (!planeBack && (dot > QDef.BACKFACE_EPSILON)))
                {
                    if (Host.Cvars.glTexSort.Get<bool>())
                    {
                        RenderBrushPoly(psurf[surfOffset]);
                    }
                    else
                    {
                        DrawSequentialPoly(psurf[surfOffset]);
                    }
                }
            }

            BlendLightmaps();

            Host.Video.Device.PopMatrix();
        }
    }

    //glRect_t;
}
