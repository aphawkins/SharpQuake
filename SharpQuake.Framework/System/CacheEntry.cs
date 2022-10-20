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

    public class CacheEntry : CacheUser
    {
        public CacheEntry Next { get; private set; }

        public CacheEntry Prev { get; private set; }

        public CacheEntry LruPrev { get; private set; }

        public CacheEntry LruNext
        {
            get
            {
                return _LruNext;
            }
        }

        private Cache Cache
        {
            get;
            set;
        }

        private CacheEntry _LruNext;
        private int _Size;

        // Cache_UnlinkLRU
        public void RemoveFromLRU()
        {
            if (_LruNext == null || LruPrev == null)
                Utilities.Error("Cache_UnlinkLRU: NULL link");

            _LruNext.LruPrev = LruPrev;
            LruPrev._LruNext = _LruNext;
            LruPrev = _LruNext = null;
        }

        // inserts <this> instance after <prev> in LRU list
        public void LRUInstertAfter(CacheEntry prev)
        {
            if (_LruNext != null || LruPrev != null)
                Utilities.Error("Cache_MakeLRU: active link");

            prev._LruNext.LruPrev = this;
            _LruNext = prev._LruNext;
            LruPrev = prev;
            prev._LruNext = this;
        }

        // inserts <this> instance before <next>
        public void InsertBefore(CacheEntry next)
        {
            Next = next;
            if (next.Prev != null)
                Prev = next.Prev;
            else
                Prev = next;

            if (next.Prev != null)
                next.Prev.Next = this;
            else
                next.Prev = this;
            next.Prev = this;

            if (next.Next == null)
                next.Next = this;
        }

        public void Remove()
        {
            Prev.Next = Next;
            Next.Prev = Prev;
            Next = Prev = null;

            data = null;
            Cache.BytesAllocated -= _Size;
            _Size = 0;

            RemoveFromLRU();
        }

        public CacheEntry(Cache cache, bool isHead = false)
        {
            if (isHead)
            {
                Next = this;
                Prev = this;
                _LruNext = this;
                LruPrev = this;
            }
        }

        public CacheEntry(Cache cache, int size)
        {
            Cache = cache;

            _Size = size;
            Cache.BytesAllocated += _Size;
        }

        ~CacheEntry()
        {
            if (Cache != null)
                Cache.BytesAllocated -= _Size;
        }
    }
}
