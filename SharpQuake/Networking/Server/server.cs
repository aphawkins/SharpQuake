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

namespace SharpQuake
{
    using System;
    using SharpQuake.Framework;
    using SharpQuake.Framework.IO;
    using SharpQuake.Game.Networking.Server;

    public partial class server
    {
        public server_t sv { get; }

        public server_static_t svs { get; }

        public bool IsActive
        {
            get
            {
                return sv.active;
            }
        }

        public float Gravity
        {
            get
            {
                return Host.Cvars.Gravity.Get<float>();
            }
        }

        public bool IsLoading
        {
            get
            {
                return sv.state == server_state_t.Loading;
            }
        }

        public float Aim
        {
            get
            {
                return Host.Cvars.Aim.Get<float>();
            }
        }

        private readonly string[] _LocalModels = new string[QDef.MAX_MODELS]; //[MAX_MODELS][5];	// inline model names for precache

        /// <summary>
        /// EDICT_NUM
        /// </summary>
        public MemoryEdict EdictNum(int n)
        {
            if (n < 0 || n >= sv.max_edicts)
            {
                Utilities.Error("EDICT_NUM: bad number {0}", n);
            }

            return sv.edicts[n];
        }

        /// <summary>
        /// ED_Alloc
        /// Either finds a free edict, or allocates a new one.
        /// Try to avoid reusing an entity that was recently freed, because it
        /// can cause the client to think the entity morphed into something else
        /// instead of being removed and recreated, which can cause interpolated
        /// angles and bad trails.
        /// </summary>
        public MemoryEdict AllocEdict()
        {
            MemoryEdict e;
            int i;
            for (i = svs.maxclients + 1; i < sv.num_edicts; i++)
            {
                e = EdictNum(i);

                // the first couple seconds of server time can involve a lot of
                // freeing and allocating, so relax the replacement policy
                if (e.free && (e.freetime < 2 || sv.time - e.freetime > 0.5))
                {
                    e.Clear();
                    return e;
                }
            }

            if (i == QDef.MAX_EDICTS)
            {
                Utilities.Error("ED_Alloc: no free edicts");
            }

            sv.num_edicts++;
            e = EdictNum(i);
            e.Clear();

            return e;
        }

        /// <summary>
        /// ED_Free
        /// Marks the edict as free
        /// FIXME: walk all entities and NULL out references to this entity
        /// </summary>
        public void FreeEdict(MemoryEdict ed)
        {
            UnlinkEdict(ed);		// unlink from world bsp

            ed.free = true;
            ed.v.model = 0;
            ed.v.takedamage = 0;
            ed.v.modelindex = 0;
            ed.v.colormap = 0;
            ed.v.skin = 0;
            ed.v.frame = 0;
            ed.v.origin = default;
            ed.v.angles = default;
            ed.v.nextthink = -1;
            ed.v.solid = 0;

            ed.freetime = (float)sv.time;
        }

        /// <summary>
        /// EDICT_TO_PROG(e)
        /// </summary>
        public int EdictToProg(MemoryEdict e)
        {
            return Array.IndexOf(sv.edicts, e); // todo: optimize this
        }

        /// <summary>
        /// PROG_TO_EDICT(e)
        /// Offset in bytes!
        /// </summary>
        public MemoryEdict ProgToEdict(int e)
        {
            if (e < 0 || e > sv.edicts.Length)
            {
                Utilities.Error("ProgToEdict: Bad prog!");
            }

            return sv.edicts[e];
        }

        /// <summary>
        /// NUM_FOR_EDICT
        /// </summary>
        public int NumForEdict(MemoryEdict e)
        {
            var i = Array.IndexOf(sv.edicts, e); // todo: optimize this

            if (i < 0)
            {
                Utilities.Error("NUM_FOR_EDICT: bad pointer");
            }

            return i;
        }

        public server(Host host)
        {
            Host = host;

            sv = new server_t();
            svs = new server_static_t();
        }
    }
}
