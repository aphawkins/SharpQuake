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

namespace SharpQuake.Framework
{
    public class VidDef
    {
        public byte[] colormap;		// 256 * VID_GRADES size
        public int fullbright;		// index of first fullbright color
        public int rowbytes; // unsigned	// may be > width if displayed in a window
        public int width; // unsigned
        public int height; // unsigned
        public float aspect;		// width / height -- < 0 is taller than wide
        public int numpages;
        public bool recalc_refdef;	// if true, recalc vid-based stuff
        public int conwidth; // unsigned
        public int conheight; // unsigned
        public int maxwarpwidth;
        public int maxwarpheight;
    } // viddef_t;
}
