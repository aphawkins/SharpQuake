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
    using System.Numerics;

    public class FrameworkClient
    {
        public bool active;             // false = client is free
        public bool spawned;            // false = don't send datagrams
        public bool dropasap;           // has been told to go to another level
        public bool privileged;         // can execute any host command
        public bool sendsignon;         // only valid before spawned

        public double last_message;     // reliable messages must be sent

        // periodically
        public QuakeSocket netconnection; // communications handle

        public UserCommand cmd;               // movement
        public Vector3 wishdir;			// intended motion calced from cmd

        public MessageWriter message;
        //public sizebuf_t		message;			// can be added to at any time,
        // copied and clear once per frame
        //public byte[] msgbuf;//[MAX_MSGLEN];

        public MemoryEdict edict; // edict_t *edict	// EDICT_NUM(clientnum+1)
        public string name;//[32];			// for printing to other people
        public int colors;

        public float[] ping_times;//[NUM_PING_TIMES];
        public int num_pings;           // ping_times[num_pings%NUM_PING_TIMES]

        // spawn parms are carried from level to level
        public float[] spawn_parms;//[NUM_SPAWN_PARMS];

        // client known data for deltas
        public int old_frags;

        public void Clear()
        {
            active = false;
            spawned = false;
            dropasap = false;
            privileged = false;
            sendsignon = false;
            last_message = 0;
            netconnection = null;
            cmd.Clear();
            wishdir = Vector3.Zero;
            message.Clear();
            edict = null;
            name = null;
            colors = 0;
            Array.Clear(ping_times, 0, ping_times.Length);
            num_pings = 0;
            Array.Clear(spawn_parms, 0, spawn_parms.Length);
            old_frags = 0;
        }

        public FrameworkClient()
        {
            ping_times = new float[ServerDef.NUM_PING_TIMES];
            spawn_parms = new float[ServerDef.NUM_SPAWN_PARMS];
            message = new MessageWriter(QDef.MAX_MSGLEN);
        }
    }// client_t;
}
