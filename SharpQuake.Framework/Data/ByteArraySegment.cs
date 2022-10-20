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
    using System;

    public class ByteArraySegment
    {
        public byte[] Data
        {
            get
            {
                return _Segment.Array;
            }
        }

        public int StartIndex
        {
            get
            {
                return _Segment.Offset;
            }
        }

        public int Length
        {
            get
            {
                return _Segment.Count;
            }
        }

        private ArraySegment<byte> _Segment;

        public ByteArraySegment(byte[] array)
            : this(array, 0, -1)
        {
        }

        public ByteArraySegment(byte[] array, int startIndex)
            : this(array, startIndex, -1)
        {
        }

        public ByteArraySegment(byte[] array, int startIndex, int length)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array");
            }
            if (length == -1)
            {
                length = array.Length - startIndex;
            }
            if (length <= 0)
            {
                throw new ArgumentException("Invalid length!");
            }
            _Segment = new ArraySegment<byte>(array, startIndex, length);
        }
    }
}
