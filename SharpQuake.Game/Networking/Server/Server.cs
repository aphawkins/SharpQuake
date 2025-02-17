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

namespace SharpQuake.Game.Networking.Server
{
    using System;
    using SharpQuake.Framework;
    using SharpQuake.Game.Data.Models;

    public enum ServerState
    {
        Loading,
        Active
    }

    //=============================================================================

    // server_state_t;

    public class Server
    {
        public bool active;             // false if only a net client
        public bool paused;
        public bool loadgame;           // handle connections specially
        public double time;
        public int lastcheck;           // used by PF_checkclient
        public double lastchecktime;
        public string name;// char		name[64];			// map name
        public string modelname;// char		modelname[64];		// maps/<name>.bsp, for model_precache[0]
        public BrushModelData worldmodel;
        public string[] model_precache; //[MAX_MODELS];	// NULL terminated
        public ModelData[] models; //[MAX_MODELS];
        public string[] sound_precache; //[MAX_SOUNDS];	// NULL terminated
        public string[] lightstyles; // [MAX_LIGHTSTYLES];
        public int num_edicts;
        public int max_edicts;
        public MemoryEdict[] edicts;        // can NOT be array indexed, because

        // edict_t is variable sized, but can
        // be used to reference the world ent
        public ServerState state;			// some actions are only valid during load

        public MessageWriter datagram;
        public MessageWriter reliable_datagram; // copied to all clients at end of frame
        public MessageWriter signon;

        public void Clear()
        {
            active = false;
            paused = false;
            loadgame = false;
            time = 0;
            lastcheck = 0;
            lastchecktime = 0;
            name = null;
            modelname = null;
            worldmodel = null;
            Array.Clear(model_precache, 0, model_precache.Length);
            Array.Clear(models, 0, models.Length);
            Array.Clear(sound_precache, 0, sound_precache.Length);
            Array.Clear(lightstyles, 0, lightstyles.Length);
            num_edicts = 0;
            max_edicts = 0;
            edicts = null;
            state = 0;
            datagram.Clear();
            reliable_datagram.Clear();
            signon.Clear();
            GC.Collect();
        }

        public Server()
        {
            model_precache = new string[QDef.MAX_MODELS];
            models = new ModelData[QDef.MAX_MODELS];
            sound_precache = new string[QDef.MAX_SOUNDS];
            lightstyles = new string[QDef.MAX_LIGHTSTYLES];
            datagram = new MessageWriter(QDef.MAX_DATAGRAM);
            reliable_datagram = new MessageWriter(QDef.MAX_DATAGRAM);
            signon = new MessageWriter(8192);
        }
    }// server_t;
}
