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
    using SharpQuake.Framework;
    using System.Numerics;

    public static class RendererDef
    {
        public const float VIRTUAL_WIDTH = 640;
        public const float VIRTUAL_HEIGHT = 480;
    }


    // !!! if this is changed, it must be changed in asm_draw.h too !!!
    public class RefDef
    {
        public VRect vrect;				// subwindow in video for refresh
        public Vector3 vieworg;
        public Vector3 viewangles;
        public float fov_x, fov_y;
    }
}
