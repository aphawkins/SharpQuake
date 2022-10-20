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

    using string_t = System.Int32;
    using func_t = System.Int32;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct EntVars
    {
        public float modelindex;
        public Vector3f absmin;
        public Vector3f absmax;
        public float ltime;
        public float movetype;
        public float solid;
        public Vector3f origin;
        public Vector3f oldorigin;
        public Vector3f velocity;
        public Vector3f angles;
        public Vector3f avelocity;
        public Vector3f punchangle;
        public string_t classname;
        public string_t model;
        public float frame;
        public float skin;
        public float effects;
        public Vector3f mins;
        public Vector3f maxs;
        public Vector3f size;
        public func_t touch;
        public func_t use;
        public func_t think;
        public func_t blocked;
        public float nextthink;
        public string_t groundentity;
        public float health;
        public float frags;
        public float weapon;
        public string_t weaponmodel;
        public float weaponframe;
        public float currentammo;
        public float ammo_shells;
        public float ammo_nails;
        public float ammo_rockets;
        public float ammo_cells;
        public float items;
        public float takedamage;
        public string_t chain;
        public float deadflag;
        public Vector3f view_ofs;
        public float button0;
        public float button1;
        public float button2;
        public float impulse;
        public float fixangle;
        public Vector3f v_angle;
        public float idealpitch;
        public string_t netname;
        public string_t enemy;
        public float flags;
        public float colormap;
        public float team;
        public float max_health;
        public float teleport_time;
        public float armortype;
        public float armorvalue;
        public float waterlevel;
        public float watertype;
        public float ideal_yaw;
        public float yaw_speed;
        public string_t aiment;
        public string_t goalentity;
        public float spawnflags;
        public string_t target;
        public string_t targetname;
        public float dmg_take;
        public float dmg_save;
        public string_t dmg_inflictor;
        public string_t owner;
        public Vector3f movedir;
        public string_t message;
        public float sounds;
        public string_t noise;
        public string_t noise1;
        public string_t noise2;
        public string_t noise3;

        public static string_t SizeInBytes = Marshal.SizeOf(typeof(EntVars));
    } // entvars_t
}
