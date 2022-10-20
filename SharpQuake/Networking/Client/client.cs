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

// client.h

namespace SharpQuake
{
    using System;
    using SharpQuake.Framework;
    using SharpQuake.Game.World;
    using SharpQuake.Framework.IO.Sound;
    using SharpQuake.Game.Data.Models;
    using OpenTK;

    public struct lightstyle_t
    {
        //public int length;
        public string map; // [MAX_STYLESTRING];
    }

    public enum cactive_t
    {
        ca_dedicated, 		// a dedicated server with no ability to start a client
        ca_disconnected, 	// full screen console with no connection
        ca_connected		// valid netcon, talking to a server
    }



    //
    // cl_input
    //
    internal struct kbutton_t
    {
        public bool IsDown
        {
            get
            {
                return (state & 1) != 0;
            }
        }

        public int down0, down1;        // key nums holding it down
        public int state;			// low bit is down state
    }

    public partial class client
    {
        public client_static_t cls { get; }

        public client_state_t cl { get; }

        public Entity[] Entities { get; } = new Entity[QDef.MAX_EDICTS];

        /// <summary>
        /// cl_entities[cl.viewentity]
        /// Player model (visible when out of body)
        /// </summary>
        public Entity ViewEntity
        {
            get
            {
                return Entities[cl.viewentity];
            }
        }

        /// <summary>
        /// cl.viewent
        /// Weapon model (only visible from inside body)
        /// </summary>
        public Entity ViewEnt
        {
            get
            {
                return cl.viewent;
            }
        }

        public float ForwardSpeed
        {
            get
            {
                return Host.Cvars.ForwardSpeed.Get<float>();
            }
        }

        public bool LookSpring
        {
            get
            {
                return Host.Cvars.LookSpring.Get<bool>();
            }
        }

        public bool LookStrafe
        {
            get
            {
                return Host.Cvars.LookStrafe.Get<bool>();
            }
        }

        public dlight_t[] DLights { get; } = new dlight_t[ClientDef.MAX_DLIGHTS];

        public lightstyle_t[] LightStyle { get; } = new lightstyle_t[QDef.MAX_LIGHTSTYLES];

        public Entity[] VisEdicts { get; } = new Entity[ClientDef.MAX_VISEDICTS];

        public float Sensitivity
        {
            get
            {
                return Host.Cvars.Sensitivity.Get<float>();
            }
        }

        public float MSide
        {
            get
            {
                return Host.Cvars.MSide.Get<float>();
            }
        }

        public float MYaw
        {
            get
            {
                return Host.Cvars.MYaw.Get<float>();
            }
        }

        public float MPitch
        {
            get
            {
                return Host.Cvars.MPitch.Get<float>();
            }
        }

        public float MForward
        {
            get
            {
                return Host.Cvars.MForward.Get<float>();
            }
        }

        public string Name
        {
            get
            {
                return Host.Cvars.Name.Get<string>();
            }
        }

        public float Color
        {
            get
            {
                return Host.Cvars.Color.Get<float>();
            }
        }

        public int NumVisEdicts;

        public client(Host host)
        {
            Host = host;
            cls = new client_static_t();
            cl = new client_state_t();
        }

        private readonly EFrag[] _EFrags = new EFrag[ClientDef.MAX_EFRAGS]; // cl_efrags
        private readonly Entity[] _StaticEntities = new Entity[ClientDef.MAX_STATIC_ENTITIES]; // cl_static_entities
    }

    // lightstyle_t;

    internal static class ColorShift
    {
        public const int CSHIFT_CONTENTS = 0;
        public const int CSHIFT_DAMAGE = 1;
        public const int CSHIFT_BONUS = 2;
        public const int CSHIFT_POWERUP = 3;
        public const int NUM_CSHIFTS = 4;
    }

    public class scoreboard_t
    {
        public string name; //[MAX_SCOREBOARDNAME];

        //public float entertime;
        public int frags;

        public int colors;			// two 4 bit fields
        public byte[] translations; // [VID_GRADES*256];

        public scoreboard_t()
        {
            translations = new byte[Vid.VID_GRADES * 256];
        }
    } // scoreboard_t;

    public class cshift_t
    {
        public int[] destcolor; // [3];
        public int percent;		// 0-256

        public void Clear()
        {
            destcolor[0] = 0;
            destcolor[1] = 0;
            destcolor[2] = 0;
            percent = 0;
        }

        public cshift_t()
        {
            destcolor = new int[3];
        }

        public cshift_t(int[] destColor, int percent)
        {
            if (destColor.Length != 3)
            {
                throw new ArgumentException("destColor must have length of 3 elements!");
            }
            destcolor = destColor;
            this.percent = percent;
        }
    } // cshift_t;


    internal class beam_t
    {
        public int entity;
        public ModelData model;
        public float endtime;
        public Vector3 start, end;

        public void Clear()
        {
            entity = 0;
            model = null;
            endtime = 0;
            start = Vector3.Zero;
            end = Vector3.Zero;
        }
    } // beam_t;

    // cactive_t;

    //
    // the client_static_t structure is persistant through an arbitrary number
    // of server connections
    //
    public class client_static_t
    {
        public cactive_t state;

        // personalization data sent to server
        public string mapstring; // [MAX_QPATH];

        public string spawnparms;//[MAX_MAPSTRING];	// to restart a level

        // demo loop control
        public int demonum;		// -1 = don't play demos

        public string[] demos; // [MAX_DEMOS][MAX_DEMONAME];		// when not playing

        // demo recording info must be here, because record is started before
        // entering a map (and clearing client_state_t)
        public bool demorecording;

        public bool demoplayback;
        public bool timedemo;
        public int forcetrack;			// -1 = use normal cd track
        public IDisposable demofile; // DisposableWrapper<BinaryReader|BinaryWriter> // FILE*
        public int td_lastframe;		// to meter out one message a frame
        public int td_startframe;		// host_framecount at start
        public float td_starttime;		// realtime at second frame of timedemo

        // connection information
        public int signon;			// 0 to SIGNONS

        public qsocket_t netcon; // qsocket_t	*netcon;
        public MessageWriter message; // sizebuf_t	message;		// writing buffer to send to server

        public client_static_t()
        {
            demos = new string[ClientDef.MAX_DEMOS];
            message = new MessageWriter(1024); // like in Client_Init()
        }
    } // client_static_t;

    //
    // the client_state_t structure is wiped completely at every
    // server signon
    //
    public class client_state_t
    {
        public int movemessages;	// since connecting to this server

        // throw out the first couple, so the player
        // doesn't accidentally do something the
        // first frame
        public usercmd_t cmd;			// last command sent to the server

        // information for local display
        public int[] stats; //[MAX_CL_STATS];	// health, etc

        public int items;			// inventory bit flags
        public float[] item_gettime; //[32];	// cl.time of aquiring item, for blinking
        public float faceanimtime;	// use anim frame if cl.time < this

        public cshift_t[] cshifts; //[NUM_CSHIFTS];	// color shifts for damage, powerups
        public cshift_t[] prev_cshifts; //[NUM_CSHIFTS];	// and content types

        // the client maintains its own idea of view angles, which are
        // sent to the server each frame.  The server sets punchangle when
        // the view is temporarliy offset, and an angle reset commands at the start
        // of each level and after teleporting.
        public Vector3[] mviewangles; //[2];	// during demo playback viewangles is lerped

        // between these
        public Vector3 viewangles;

        public Vector3[] mvelocity; //[2];	// update by server, used for lean+bob

        // (0 is newest)
        public Vector3 velocity;		// lerped between mvelocity[0] and [1]

        public Vector3 punchangle;		// temporary offset

        // pitch drifting vars
        public float idealpitch;

        public float pitchvel;
        public bool nodrift;
        public float driftmove;
        public double laststop;

        public float viewheight;
        public float crouch;			// local amount for smoothing stepups

        public bool paused;			// send over by server
        public bool onground;
        public bool inwater;

        public int intermission;	// don't change view angle, full screen, etc
        public int completed_time;	// latched at intermission start

        public double[] mtime; //[2];		// the timestamp of last two messages
        public double time;			// clients view of time, should be between

        // servertime and oldservertime to generate
        // a lerp point for other data
        public double oldtime;		// previous cl.time, time-oldtime is used

        // to decay light values and smooth step ups

        public float last_received_message;	// (realtime) for net trouble icon

        //
        // information that is static for the entire time connected to a server
        //
        public ModelData[] model_precache; // [MAX_MODELS];

        public SoundEffect_t[] sound_precache; // [MAX_SOUNDS];

        public string levelname; // char[40];	// for display on solo scoreboard
        public int viewentity;		// cl_entitites[cl.viewentity] = player
        public int maxclients;
        public int gametype;

        // refresh related state
        public BrushModelData worldmodel;	// cl_entitites[0].model

        public EFrag free_efrags; // first free efrag in list
        public int num_entities;	// held in cl_entities array
        public int num_statics;	// held in cl_staticentities array
        public Entity viewent;			// the gun model

        public int cdtrack, looptrack;	// cd audio

        // frag scoreboard
        public scoreboard_t[] scores;		// [cl.maxclients]

        public bool HasItems(int item)
        {
            return (items & item) == item;
        }

        public void Clear()
        {
            movemessages = 0;
            cmd.Clear();
            Array.Clear(stats, 0, stats.Length);
            items = 0;
            Array.Clear(item_gettime, 0, item_gettime.Length);
            faceanimtime = 0;

            foreach (var cs in cshifts)
            {
                cs.Clear();
            }

            foreach (var cs in prev_cshifts)
            {
                cs.Clear();
            }

            mviewangles[0] = Vector3.Zero;
            mviewangles[1] = Vector3.Zero;
            viewangles = Vector3.Zero;
            mvelocity[0] = Vector3.Zero;
            mvelocity[1] = Vector3.Zero;
            velocity = Vector3.Zero;
            punchangle = Vector3.Zero;

            idealpitch = 0;
            pitchvel = 0;
            nodrift = false;
            driftmove = 0;
            laststop = 0;

            viewheight = 0;
            crouch = 0;

            paused = false;
            onground = false;
            inwater = false;

            intermission = 0;
            completed_time = 0;

            mtime[0] = 0;
            mtime[1] = 0;
            time = 0;
            oldtime = 0;
            last_received_message = 0;

            Array.Clear(model_precache, 0, model_precache.Length);
            Array.Clear(sound_precache, 0, sound_precache.Length);

            levelname = null;
            viewentity = 0;
            maxclients = 0;
            gametype = 0;

            worldmodel = null;
            free_efrags = null;
            num_entities = 0;
            num_statics = 0;
            viewent.Clear();

            cdtrack = 0;
            looptrack = 0;

            scores = null;
        }

        public client_state_t()
        {
            stats = new int[QStatsDef.MAX_CL_STATS];
            item_gettime = new float[32]; // ???????????

            cshifts = new cshift_t[ColorShift.NUM_CSHIFTS];
            for (var i = 0; i < ColorShift.NUM_CSHIFTS; i++)
            {
                cshifts[i] = new cshift_t();
            }

            prev_cshifts = new cshift_t[ColorShift.NUM_CSHIFTS];
            for (var i = 0; i < ColorShift.NUM_CSHIFTS; i++)
            {
                prev_cshifts[i] = new cshift_t();
            }

            mviewangles = new Vector3[2]; //??????
            mvelocity = new Vector3[2];
            mtime = new double[2];
            model_precache = new ModelData[QDef.MAX_MODELS];
            sound_precache = new SoundEffect_t[QDef.MAX_SOUNDS];
            viewent = new Entity();
        }
    } //client_state_t;

    // usercmd_t;

    // kbutton_t;
}
