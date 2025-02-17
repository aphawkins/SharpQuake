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

namespace SharpQuake.Rendering
{
    using SharpQuake.Framework;
    using SharpQuake.Framework.IO.BSP;
    using SharpQuake.Game.Rendering.Memory;
    using SharpQuake.Game.World;
    using System;
    using System.Numerics;

    public class Occlusion
    {
        public int VisFrameCount
        {
            get;
            set;
        } // rVisFrameCount	// bumped when going to a new PVS

        public MemoryLeaf ViewLeaf
        {
            get;
            set;
        } // r_viewleaf

        public MemoryLeaf OldViewLeaf
        {
            get;
            set;
        } // r_oldviewleaf

        private Host Host
        {
            get;
            set;
        }

        private TextureChains TextureChains
        {
            get;
            set;
        }

        public Occlusion(Host host, TextureChains textureChains)
        {
            Host = host;
            TextureChains = textureChains;
        }

        public void SetupFrame(ref Vector3 origin)
        {
            OldViewLeaf = ViewLeaf;
            ViewLeaf = Host.Client.Cl.worldmodel.PointInLeaf(ref origin);
        }

        /// <summary>
        /// R_MarkLeaves
        /// </summary>
        public void MarkLeaves()
        {
            if (OldViewLeaf == ViewLeaf && !Host.Cvars.NoVis.Get<bool>())
            {
                return;
            }

            //if( _IsMirror )
            //  return;

            VisFrameCount++;
            OldViewLeaf = ViewLeaf;

            byte[] vis;
            if (Host.Cvars.NoVis.Get<bool>())
            {
                vis = new byte[4096];
                Utilities.FillArray<byte>(vis, 0xff); // todo: add count parameter?
                                                      //memset(solid, 0xff, (cl.worldmodel->numleafs + 7) >> 3);
            }
            else
            {
                vis = Host.Client.Cl.worldmodel.LeafPVS(ViewLeaf);
            }

            var world = Host.Client.Cl.worldmodel;
            for (var i = 0; i < world.NumLeafs; i++)
            {
                if (vis[i >> 3] != 0 & (1 << (i & 7)) != 0)
                {
                    MemoryNodeBase node = world.Leaves[i + 1];
                    do
                    {
                        if (node.visframe == VisFrameCount)
                        {
                            break;
                        }

                        node.visframe = VisFrameCount;
                        node = node.parent;
                    } while (node != null);
                }
            }
        }

        /// <summary>
        /// R_RecursiveWorldNode
        /// </summary>
        public void RecursiveWorldNode(MemoryNodeBase node, Vector3 modelOrigin, int frameCount, ref QuakePlane[] frustum, Action<MemorySurface> onDrawSurface, Action<EFrag> onStoreEfrags)
        {
            if (node.contents == (int)Q1Contents.Solid)
            {
                return;     // solid
            }

            if (node.visframe != VisFrameCount)
            {
                return;
            }

            if (Utilities.CullBox(ref node.mins, ref node.maxs, ref frustum))
            {
                return;
            }

            int c;

            // if a leaf node, draw stuff
            if (node.contents < 0)
            {
                var pleaf = (MemoryLeaf)node;
                var marks = pleaf.marksurfaces;
                var mark = pleaf.firstmarksurface;
                c = pleaf.nummarksurfaces;

                if (c != 0)
                {
                    do
                    {
                        marks[mark].visframe = frameCount;
                        mark++;
                    } while (--c != 0);
                }

                // deal with model fragments in this leaf
                if (pleaf.efrags != null)
                {
                    onStoreEfrags(pleaf.efrags);
                }

                return;
            }

            // node is just a decision point, so go down the apropriate sides

            var n = (MemoryNode)node;

            // find which side of the node we are on
            var plane = n.plane;
            var dot = plane.type switch
            {
                PlaneDef.PLANE_X => (double)(modelOrigin.X - plane.dist),
                PlaneDef.PLANE_Y => (double)(modelOrigin.Y - plane.dist),
                PlaneDef.PLANE_Z => (double)(modelOrigin.Z - plane.dist),
                _ => (double)(Vector3.Dot(modelOrigin, plane.normal) - plane.dist),
            };
            var side = dot >= 0 ? 0 : 1;

            // recurse down the children, front side first
            RecursiveWorldNode(n.children[side], modelOrigin, frameCount, ref frustum, onDrawSurface, onStoreEfrags);

            // draw stuff
            c = n.numsurfaces;

            if (c != 0)
            {
                var surf = Host.Client.Cl.worldmodel.Surfaces;
                int offset = n.firstsurface;

                if (dot < 0 - QDef.BACKFACE_EPSILON)
                {
                    side = (int)Q1SurfaceFlags.PlaneBack;
                }
                else if (dot > QDef.BACKFACE_EPSILON)
                {
                    side = 0;
                }

                for (; c != 0; c--, offset++)
                {
                    if (surf[offset].visframe != frameCount)
                    {
                        continue;
                    }

                    // don't backface underwater surfaces, because they warp
                    if ((surf[offset].flags & (int)Q1SurfaceFlags.Underwater) == 0 && ((dot < 0) ^ ((surf[offset].flags & (int)Q1SurfaceFlags.PlaneBack) != 0)))
                    {
                        continue;       // wrong side
                    }

                    // if sorting by texture, just store it out
                    if (Host.Cvars.glTexSort.Get<bool>())
                    {
                        //if( !_IsMirror || surf[offset].texinfo.texture != Host.Client.cl.worldmodel.textures[_MirrorTextureNum] )
                        //{
                        surf[offset].texturechain = surf[offset].texinfo.texture.texturechain;
                        surf[offset].texinfo.texture.texturechain = surf[offset];
                        //}
                    }
                    else if ((surf[offset].flags & (int)Q1SurfaceFlags.Sky) != 0)
                    {
                        surf[offset].texturechain = TextureChains.SkyChain;
                        TextureChains.SkyChain = surf[offset];
                    }
                    else if ((surf[offset].flags & (int)Q1SurfaceFlags.Turbulence) != 0)
                    {
                        surf[offset].texturechain = TextureChains.WaterChain;
                        TextureChains.WaterChain = surf[offset];
                    }
                    else
                    {
                        onDrawSurface(surf[offset]);
                    }
                }
            }

            // recurse down the back side
            RecursiveWorldNode(n.children[side == 0 ? 1 : 0], modelOrigin, frameCount, ref frustum, onDrawSurface, onStoreEfrags);
        }
    }
}
