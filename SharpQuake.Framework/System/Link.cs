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

namespace SharpQuake.Framework
{
    using System;
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class Link
    {
        public Link Prev { get; private set; }

        public Link Next { get; private set; }

        public object Owner { get; }

        public Link(object owner)
        {
            Owner = owner;
        }

        public void Clear()
        {
            Prev = Next = this;
        }

        public void ClearToNulls()
        {
            Prev = Next = null;
        }

        public void Remove()
        {
            Next.Prev = Prev;
            Prev.Next = Next;
            Next = null;
            Prev = null;
        }

        public void InsertBefore(Link before)
        {
            Next = before;
            Prev = before.Prev;
            Prev.Next = this;
            Next.Prev = this;
        }

        public void InsertAfter(Link after)
        {
            Next = after.Next;
            Prev = after;
            Prev.Next = this;
            Next.Prev = this;
        }
    } // link_t;
}
