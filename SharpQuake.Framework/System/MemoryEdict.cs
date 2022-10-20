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

    using string_t = System.Int32;

    /// <summary>
    /// In-memory edict
    /// </summary>
    public class MemoryEdict
    {
        public bool free;
        public Link area; // linked to a division node or leaf

        public string_t num_leafs;
        public short[] leafnums; // [MAX_ENT_LEAFS];

        public EntityState baseline;

        public float freetime;			// sv.time when the object was freed
        public EntVars v;					// C exported fields from progs
        public float[] fields; // other fields from progs

        public void Clear()
        {
            v = default;
            if (fields != null)
                Array.Clear(fields, 0, fields.Length);
            free = false;
        }

        public bool IsV(string_t offset, out string_t correctedOffset)
        {
            if (offset < (EntVars.SizeInBytes >> 2))
            {
                correctedOffset = offset;
                return true;
            }
            correctedOffset = offset - (EntVars.SizeInBytes >> 2);
            return false;
        }

        public unsafe void LoadInt(string_t offset, EVal* result)
        {
            if (IsV(offset, out int offset1))
            {
                fixed (void* pv = &v)
                {
                    var a = (EVal*)((Int32*)pv + offset1);
                    result->_int = a->_int;
                }
            }
            else
            {
                fixed (void* pv = fields)
                {
                    var a = (EVal*)((Int32*)pv + offset1);
                    result->_int = a->_int;
                }
            }
        }

        public unsafe void StoreInt(string_t offset, EVal* value)
        {
            if (IsV(offset, out int offset1))
            {
                fixed (void* pv = &v)
                {
                    var a = (EVal*)((Int32*)pv + offset1);
                    a->_int = value->_int;
                }
            }
            else
            {
                fixed (void* pv = fields)
                {
                    var a = (EVal*)((Int32*)pv + offset1);
                    a->_int = value->_int;
                }
            }
        }

        public unsafe void LoadVector(string_t offset, EVal* result)
        {
            if (IsV(offset, out int offset1))
            {
                fixed (void* pv = &v)
                {
                    var a = (EVal*)((Int32*)pv + offset1);
                    result->vector[0] = a->vector[0];
                    result->vector[1] = a->vector[1];
                    result->vector[2] = a->vector[2];
                }
            }
            else
            {
                fixed (void* pf = fields)
                {
                    var a = (EVal*)((Int32*)pf + offset1);
                    result->vector[0] = a->vector[0];
                    result->vector[1] = a->vector[1];
                    result->vector[2] = a->vector[2];
                }
            }
        }

        public unsafe void StoreVector(string_t offset, EVal* value)
        {
            if (IsV(offset, out int offset1))
            {
                fixed (void* pv = &v)
                {
                    var a = (EVal*)((Int32*)pv + offset1);
                    a->vector[0] = value->vector[0];
                    a->vector[1] = value->vector[1];
                    a->vector[2] = value->vector[2];
                }
            }
            else
            {
                fixed (void* pf = fields)
                {
                    var a = (EVal*)((Int32*)pf + offset1);
                    a->vector[0] = value->vector[0];
                    a->vector[1] = value->vector[1];
                    a->vector[2] = value->vector[2];
                }
            }
        }

        public unsafe string_t GetInt(string_t offset)
        {
            Int32 result;
            if (IsV(offset, out int offset1))
            {
                fixed (void* pv = &v)
                {
                    var a = (EVal*)((Int32*)pv + offset1);
                    result = a->_int;
                }
            }
            else
            {
                fixed (void* pv = fields)
                {
                    var a = (EVal*)((Int32*)pv + offset1);
                    result = a->_int;
                }
            }
            return result;
        }

        public unsafe float GetFloat(string_t offset)
        {
            float result;
            if (IsV(offset, out int offset1))
            {
                fixed (void* pv = &v)
                {
                    var a = (EVal*)((float*)pv + offset1);
                    result = a->_float;
                }
            }
            else
            {
                fixed (void* pv = fields)
                {
                    var a = (EVal*)((float*)pv + offset1);
                    result = a->_float;
                }
            }
            return result;
        }

        public unsafe void SetFloat(string_t offset, float value)
        {
            if (IsV(offset, out int offset1))
            {
                fixed (void* pv = &v)
                {
                    var a = (EVal*)((float*)pv + offset1);
                    a->_float = value;
                }
            }
            else
            {
                fixed (void* pv = fields)
                {
                    var a = (EVal*)((float*)pv + offset1);
                    a->_float = value;
                }
            }
        }

        public MemoryEdict()
        {
            area = new Link(this);
            leafnums = new short[ProgramDef.MAX_ENT_LEAFS];
            fields = new float[(ProgramDef.EdictSize - EntVars.SizeInBytes) >> 2];
        }
    } // edict_t;
}
