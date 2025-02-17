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

// gr_rlights.c

namespace SharpQuake
{
    using SharpQuake.Framework;
    using SharpQuake.Framework.IO.BSP;
    using SharpQuake.Game.Rendering.Memory;
    using System.Drawing;
    using System.Numerics;

    public partial class Render
    {
        private int _DlightFrameCount; // r_dlightframecount

        /// <summary>
        /// R_PushDlights
        /// </summary>
        public void PushDlights()
        {
            if (Host.Cvars.glFlashBlend.Get<bool>())
            {
                return;
            }

            _DlightFrameCount = _FrameCount + 1;    // because the count hasn't advanced yet for this frame

            for (var i = 0; i < ClientDef.MAX_DLIGHTS; i++)
            {
                var l = Host.Client.DLights[i];
                if (l.die < Host.Client.Cl.time || l.radius == 0)
                {
                    continue;
                }

                MarkLights(l, 1 << i, Host.Client.Cl.worldmodel.Nodes[0]);
            }
        }

        /// <summary>
        /// R_MarkLights
        /// </summary>
        private void MarkLights(DLight light, int bit, MemoryNodeBase node)
        {
            if (node.contents < 0)
            {
                return;
            }

            var n = (MemoryNode)node;
            var splitplane = n.plane;
            var dist = Vector3.Dot(light.origin, splitplane.normal) - splitplane.dist;

            if (dist > light.radius)
            {
                MarkLights(light, bit, n.children[0]);
                return;
            }
            if (dist < -light.radius)
            {
                MarkLights(light, bit, n.children[1]);
                return;
            }

            // mark the polygons
            for (var i = 0; i < n.numsurfaces; i++)
            {
                var surf = Host.Client.Cl.worldmodel.Surfaces[n.firstsurface + i];
                if (surf.dlightframe != _DlightFrameCount)
                {
                    surf.dlightbits = 0;
                    surf.dlightframe = _DlightFrameCount;
                }
                surf.dlightbits |= bit;
            }

            MarkLights(light, bit, n.children[0]);
            MarkLights(light, bit, n.children[1]);
        }

        /// <summary>
        /// R_RenderDlights
        /// </summary>
        private void RenderDlights()
        {
            //int i;
            //dlight_t* l;

            if (!Host.Cvars.glFlashBlend.Get<bool>())
            {
                return;
            }

            _DlightFrameCount = _FrameCount + 1;    // because the count hasn't advanced yet for this frame

            Host.Video.Device.Graphics.BeginDLights();
            Host.Video.Device.SetZWrite(false);

            for (var i = 0; i < ClientDef.MAX_DLIGHTS; i++)
            {
                var l = Host.Client.DLights[i];
                if (l.die < Host.Client.Cl.time || l.radius == 0)
                {
                    continue;
                }

                RenderDlight(l);
            }

            Host.Video.Device.Graphics.EndDLights();
        }

        /// <summary>
        /// R_AnimateLight
        /// </summary>
        private void AnimateLight()
        {
            //
            // light animations
            // 'm' is normal light, 'a' is no light, 'z' is double bright
            var i = (int)(Host.Client.Cl.time * 10);
            for (var j = 0; j < QDef.MAX_LIGHTSTYLES; j++)
            {
                if (string.IsNullOrEmpty(Host.Client.LightStyle[j].map))
                {
                    _LightStyleValue[j] = 256;
                    continue;
                }
                var map = Host.Client.LightStyle[j].map;
                var k = i % map.Length;
                k = map[k] - 'a';
                k *= 22;
                _LightStyleValue[j] = k;
            }
        }

        /// <summary>
        /// R_LightPoint
        /// </summary>
        private int LightPoint(ref Vector3 p)
        {
            if (Host.Client.Cl.worldmodel.LightData == null)
            {
                return 255;
            }

            var end = p;
            end.Z -= 2048;

            var r = RecursiveLightPoint(Host.Client.Cl.worldmodel.Nodes[0], ref p, ref end);
            if (r == -1)
            {
                r = 0;
            }

            return r;
        }

        private int RecursiveLightPoint(MemoryNodeBase node, ref Vector3 start, ref Vector3 end)
        {
            if (node.contents < 0)
            {
                return -1;      // didn't hit anything
            }

            var n = (MemoryNode)node;

            // calculate mid point

            // FIXME: optimize for axial
            var plane = n.plane;
            var front = Vector3.Dot(start, plane.normal) - plane.dist;
            var back = Vector3.Dot(end, plane.normal) - plane.dist;
            var side = front < 0 ? 1 : 0;

            if ((back < 0 ? 1 : 0) == side)
            {
                return RecursiveLightPoint(n.children[side], ref start, ref end);
            }

            var frac = front / (front - back);
            var mid = start + ((end - start) * frac);

            // go down front side
            var r = RecursiveLightPoint(n.children[side], ref start, ref mid);
            if (r >= 0)
            {
                return r;       // hit something
            }

            if ((back < 0 ? 1 : 0) == side)
            {
                return -1;      // didn't hit anuthing
            }

            // check for impact on this node
            _LightSpot = mid;

            var surf = Host.Client.Cl.worldmodel.Surfaces;
            int offset = n.firstsurface;
            for (var i = 0; i < n.numsurfaces; i++, offset++)
            {
                if ((surf[offset].flags & (int)Q1SurfaceFlags.Tiled) != 0)
                {
                    continue;   // no lightmaps
                }

                var tex = surf[offset].texinfo;

                var s = (int)(Vector3.Dot(mid, new(tex.vecs[0].X, tex.vecs[0].Y, tex.vecs[0].Z)) + tex.vecs[0].W);
                var t = (int)(Vector3.Dot(mid, new(tex.vecs[1].X, tex.vecs[1].Y, tex.vecs[1].Z)) + tex.vecs[1].W);

                if (s < surf[offset].texturemins[0] || t < surf[offset].texturemins[1])
                {
                    continue;
                }

                var ds = s - surf[offset].texturemins[0];
                var dt = t - surf[offset].texturemins[1];

                if (ds > surf[offset].extents[0] || dt > surf[offset].extents[1])
                {
                    continue;
                }

                if (surf[offset].sample_base == null)
                {
                    return 0;
                }

                ds >>= 4;
                dt >>= 4;

                var lightmap = surf[offset].sample_base;
                var lmOffset = surf[offset].sampleofs;
                var extents = surf[offset].extents;
                r = 0;
                if (lightmap != null)
                {
                    lmOffset += (dt * ((extents[0] >> 4) + 1)) + ds;

                    for (var maps = 0; maps < BspDef.MAXLIGHTMAPS && surf[offset].styles[maps] != 255; maps++)
                    {
                        var scale = _LightStyleValue[surf[offset].styles[maps]];
                        r += lightmap[lmOffset] * scale;
                        lmOffset += ((extents[0] >> 4) + 1) * ((extents[1] >> 4) + 1);
                    }

                    r >>= 8;
                }

                return r;
            }

            // go down back side
            return RecursiveLightPoint(n.children[side == 0 ? 1 : 0], ref mid, ref end);
        }

        /// <summary>
        /// R_RenderDlight
        /// </summary>
        private void RenderDlight(DLight light)
        {
            var rad = light.radius * 0.35f;
            var v = light.origin - Origin;
            if (v.Length() < rad)
            {   // view is inside the dlight
                AddLightBlend(1, 0.5f, 0, light.radius * 0.0003f);
                return;
            }

            Host.Video.Device.Graphics.DrawDLight(light, ViewPn, ViewUp, ViewRight);
        }

        private void AddLightBlend(float r, float g, float b, float a)
        {
            float fA = Host.View.Blend.A + (a * (1 - Host.View.Blend.A));
            float fa = a / fA;
            float fR = (Host.View.Blend.R * (1 - fa)) + (r * fa); // error? - v_blend[0] = v_blend[1] * (1 - a2) + r * a2;
            float fG = (Host.View.Blend.G * (1 - fa)) + (g * fa);
            float fB = (Host.View.Blend.B * (1 - fa)) + (b * fa);

            Host.View.Blend = Color.FromArgb((int)fA, (int)fR, (int)fG, (int)fB);
        }
    }
}
