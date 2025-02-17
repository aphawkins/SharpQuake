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

// pr_cmds.c

namespace SharpQuake
{
    using System;
    using System.Numerics;
    using System.Text;
    using SharpQuake.Framework;
    using SharpQuake.Game.Data.Models;

    public class ProgramsBuiltIn
    {
        public static int Count => _Builtin.Length;

        /// <summary>
        /// WriteDest()
        /// </summary>
        private MessageWriter WriteDest
        {
            get
            {
                var dest = (int)GetFloat(ProgramOperatorDef.OFS_PARM0);
                switch (dest)
                {
                    case MSG_BROADCAST:
                        return Host.Server.NetServer.datagram;

                    case MSG_ONE:
                        var ent = Host.Server.ProgToEdict(Host.Programs.GlobalStruct.msg_entity);
                        var entnum = Host.Server.NumForEdict(ent);
                        if (entnum < 1 || entnum > Host.Server.ServerStatic.maxclients)
                        {
                            Host.Programs.RunError("WriteDest: not a client");
                        }

                        return Host.Server.ServerStatic.clients[entnum - 1].message;

                    case MSG_ALL:
                        return Host.Server.NetServer.reliable_datagram;

                    case MSG_INIT:
                        return Host.Server.NetServer.signon;

                    default:
                        Host.Programs.RunError("WriteDest: bad destination");
                        break;
                }

                return null;
            }
        }

        private const int MSG_BROADCAST = 0;	// unreliable to all
        private const int MSG_ONE = 1;		// reliable to one (msg_entity)
        private const int MSG_ALL = 2;		// reliable to all
        private const int MSG_INIT = 3;		// write to the init string

        private static builtin_t[] _Builtin;

        private readonly byte[] _CheckPvs = new byte[BspDef.MAX_MAP_LEAFS / 8]; // checkpvs

        private int _TempString = -1;

        private int _InVisCount; // c_invis
        private int _NotVisCount; // c_notvis

        // Instances
        private Host Host
        {
            get;
            set;
        }

        public ProgramsBuiltIn(Host host)
        {
            Host = host;
        }

        public void Initialise()
        {
            _Builtin = new builtin_t[]
            {
                PF_Fixme,
                PF_makevectors,	// void(entity e)	makevectors 		= #1;
                PF_setorigin,	// void(entity e, vector o) setorigin	= #2;
                PF_setmodel,	// void(entity e, string m) setmodel	= #3;
                PF_setsize,	// void(entity e, vector min, vector max) setsize = #4;
                PF_Fixme,	// void(entity e, vector min, vector max) setabssize = #5;
                PF_break,	// void() break						= #6;
                PF_random,	// float() random						= #7;
                PF_sound,	// void(entity e, float chan, string samp) sound = #8;
                PF_normalize,	// vector(vector v) normalize			= #9;
                PF_error,	// void(string e) error				= #10;
                PF_objerror,	// void(string e) objerror				= #11;
                PF_vlen,	// float(vector v) vlen				= #12;
                PF_vectoyaw,	// float(vector v) vectoyaw		= #13;
                PF_Spawn,	// entity() spawn						= #14;
                PF_Remove,	// void(entity e) remove				= #15;
                PF_traceline,	// float(vector v1, vector v2, float tryents) traceline = #16;
                PF_checkclient,	// entity() clientlist					= #17;
                PF_Find,	// entity(entity start, .string fld, string match) find = #18;
                PF_precache_sound,	// void(string s) precache_sound		= #19;
                PF_precache_model,	// void(string s) precache_model		= #20;
                PF_stuffcmd,	// void(entity client, string s)stuffcmd = #21;
                PF_findradius,	// entity(vector org, float rad) findradius = #22;
                PF_bprint,	// void(string s) bprint				= #23;
                PF_sprint,	// void(entity client, string s) sprint = #24;
                PF_dprint,	// void(string s) dprint				= #25;
                PF_ftos,	// void(string s) ftos				= #26;
                PF_vtos,	// void(string s) vtos				= #27;
                PF_coredump,
                PF_traceon,
                PF_traceoff,
                PF_eprint,	// void(entity e) debug print an entire entity
                PF_walkmove, // float(float yaw, float dist) walkmove
                PF_Fixme, // float(float yaw, float dist) walkmove
                PF_droptofloor,
                PF_lightstyle,
                PF_rint,
                PF_floor,
                PF_ceil,
                PF_Fixme,
                PF_checkbottom,
                PF_pointcontents,
                PF_Fixme,
                PF_fabs,
                PF_aim,
                PF_cvar,
                PF_localcmd,
                PF_nextent,
                PF_particle,
                PF_changeyaw,
                PF_Fixme,
                PF_vectoangles,

                PF_WriteByte,
                PF_WriteChar,
                PF_WriteShort,
                PF_WriteLong,
                PF_WriteCoord,
                PF_WriteAngle,
                PF_WriteString,
                PF_WriteEntity,

                PF_Fixme,
                PF_Fixme,
                PF_Fixme,
                PF_Fixme,
                PF_Fixme,
                PF_Fixme,
                PF_Fixme,

                Host.Server.MoveToGoal,
                PF_precache_file,
                PF_makestatic,

                PF_changelevel,
                PF_Fixme,

                PF_cvar_set,
                PF_centerprint,

                PF_ambientsound,

                PF_precache_model,
                PF_precache_sound,		// precache_sound2 is different only for qcc
                PF_precache_file,

                PF_setspawnparms
            };
        }

        public static void Execute(int num)
        {
            _Builtin[num]();
        }

        /// <summary>
        /// Called by Host.Programs.LoadProgs()
        /// </summary>
        public void ClearState()
        {
            _TempString = -1;
        }

        /// <summary>
        /// RETURN_EDICT(e) (((int *)pr_globals)[OFS_RETURN] = EDICT_TO_PROG(e))
        /// </summary>
        public unsafe void ReturnEdict(MemoryEdict e)
        {
            var prog = Host.Server.EdictToProg(e);
            ReturnInt(prog);
        }

        /// <summary>
        /// G_INT(OFS_RETURN) = value
        /// </summary>
        public unsafe void ReturnInt(int value)
        {
            var ptr = (int*)Host.Programs.GlobalStructAddr;
            ptr[ProgramOperatorDef.OFS_RETURN] = value;
        }

        /// <summary>
        /// G_FLOAT(OFS_RETURN) = value
        /// </summary>
        public unsafe void ReturnFloat(float value)
        {
            var ptr = (float*)Host.Programs.GlobalStructAddr;
            ptr[ProgramOperatorDef.OFS_RETURN] = value;
        }

        /// <summary>
        /// G_VECTOR(OFS_RETURN) = value
        /// </summary>
        public unsafe void ReturnVector(ref Vector3f value)
        {
            var ptr = (float*)Host.Programs.GlobalStructAddr;
            ptr[ProgramOperatorDef.OFS_RETURN + 0] = value.x;
            ptr[ProgramOperatorDef.OFS_RETURN + 1] = value.y;
            ptr[ProgramOperatorDef.OFS_RETURN + 2] = value.z;
        }

        /// <summary>
        /// G_VECTOR(OFS_RETURN) = value
        /// </summary>
        public unsafe void ReturnVector(ref Vector3 value)
        {
            var ptr = (float*)Host.Programs.GlobalStructAddr;
            ptr[ProgramOperatorDef.OFS_RETURN + 0] = value.X;
            ptr[ProgramOperatorDef.OFS_RETURN + 1] = value.Y;
            ptr[ProgramOperatorDef.OFS_RETURN + 2] = value.Z;
        }

        /// <summary>
        /// #define	G_STRING(o) (pr_strings + *(string_t *)&pr_globals[o])
        /// </summary>
        public unsafe string GetString(int parm)
        {
            var ptr = (int*)Host.Programs.GlobalStructAddr;
            return Host.Programs.GetString(ptr[parm]);
        }

        /// <summary>
        /// G_INT(o)
        /// </summary>
        public unsafe int GetInt(int parm)
        {
            var ptr = (int*)Host.Programs.GlobalStructAddr;
            return ptr[parm];
        }

        /// <summary>
        /// G_FLOAT(o)
        /// </summary>
        public unsafe float GetFloat(int parm)
        {
            var ptr = (float*)Host.Programs.GlobalStructAddr;
            return ptr[parm];
        }

        /// <summary>
        /// G_VECTOR(o)
        /// </summary>
        public unsafe float* GetVector(int parm)
        {
            var ptr = (float*)Host.Programs.GlobalStructAddr;
            return &ptr[parm];
        }

        /// <summary>
        /// #define	G_EDICT(o) ((edict_t *)((byte *)sv.edicts+ *(int *)&pr_globals[o]))
        /// </summary>
        public unsafe MemoryEdict GetEdict(int parm)
        {
            var ptr = (int*)Host.Programs.GlobalStructAddr;
            var ed = Host.Server.ProgToEdict(ptr[parm]);
            return ed;
        }

        public void PF_changeyaw()
        {
            var ent = Host.Server.ProgToEdict(Host.Programs.GlobalStruct.self);
            var current = MathLib.AngleMod(ent.v.angles.y);
            var ideal = ent.v.ideal_yaw;
            var speed = ent.v.yaw_speed;

            if (current == ideal)
            {
                return;
            }

            var move = ideal - current;
            if (ideal > current)
            {
                if (move >= 180)
                {
                    move -= 360;
                }
            }
            else
            {
                if (move <= -180)
                {
                    move += 360;
                }
            }
            if (move > 0)
            {
                if (move > speed)
                {
                    move = speed;
                }
            }
            else
            {
                if (move < -speed)
                {
                    move = -speed;
                }
            }

            ent.v.angles.y = MathLib.AngleMod(current + move);
        }

        private int SetTempString(string value)
        {
            if (_TempString == -1)
            {
                _TempString = Host.Programs.NewString(value);
            }
            else
            {
                Host.Programs.SetString(_TempString, value);
            }
            return _TempString;
        }

        private string PF_VarString(int first)
        {
            var sb = new StringBuilder(256);
            for (var i = first; i < Host.Programs.Argc; i++)
            {
                sb.Append(GetString(ProgramOperatorDef.OFS_PARM0 + (i * 3)));
            }
            return sb.ToString();
        }

        private unsafe void Copy(float* src, ref Vector3f dest)
        {
            dest.x = src[0];
            dest.y = src[1];
            dest.z = src[2];
        }

        private unsafe void Copy(float* src, out Vector3 dest)
        {
            dest.X = src[0];
            dest.Y = src[1];
            dest.Z = src[2];
        }

        /// <summary>
        /// PF_errror
        /// This is a TERMINAL error, which will kill off the entire Host.Server.
        /// Dumps self.
        /// error(value)
        /// </summary>
        private void PF_error()
        {
            var s = PF_VarString(0);
            Host.Console.Print("======SERVER ERROR in {0}:\n{1}\n",
                Host.Programs.GetString(Host.Programs.xFunction.s_name), s);
            var ed = Host.Server.ProgToEdict(Host.Programs.GlobalStruct.self);
            Host.Programs.Print(ed);
            Host.Error("Program error");
        }

        /*
        =================
        PF_objerror

        Dumps out self, then an error message.  The program is aborted and self is
        removed, but the level can continue.

        objerror(value)
        =================
        */

        private void PF_objerror()
        {
            var s = PF_VarString(0);
            Host.Console.Print("======OBJECT ERROR in {0}:\n{1}\n",
                GetString(Host.Programs.xFunction.s_name), s);
            var ed = Host.Server.ProgToEdict(Host.Programs.GlobalStruct.self);
            Host.Programs.Print(ed);
            Host.Server.FreeEdict(ed);
            Host.Error("Program error");
        }

        /*
        ==============
        PF_makevectors

        Writes new values for v_forward, v_up, and v_right based on angles
        makevectors(vector)
        ==============
        */

        private unsafe void PF_makevectors()
        {
            var av = GetVector(ProgramOperatorDef.OFS_PARM0);
            var a = new Vector3(av[0], av[1], av[2]);
            MathLib.AngleVectors(ref a, out Vector3 forward, out Vector3 right, out Vector3 up);
            MathLib.Copy(ref forward, out Host.Programs.GlobalStruct.v_forward);
            MathLib.Copy(ref right, out Host.Programs.GlobalStruct.v_right);
            MathLib.Copy(ref up, out Host.Programs.GlobalStruct.v_up);
        }

        /// <summary>
        /// PF_setorigin
        /// This is the only valid way to move an object without using the physics of the world (setting velocity and waiting).
        /// Directly changing origin will not set internal links correctly, so clipping would be messed up.
        /// This should be called when an object is spawned, and then only if it is teleported.
        /// setorigin (entity, origin)
        /// </summary>
        private unsafe void PF_setorigin()
        {
            var e = GetEdict(ProgramOperatorDef.OFS_PARM0);
            var org = GetVector(ProgramOperatorDef.OFS_PARM1);
            Copy(org, ref e.v.origin);

            Host.Server.LinkEdict(e, false);
        }

        private void SetMinMaxSize(MemoryEdict e, ref Vector3 min, ref Vector3 max)
        {
            if (min.X > max.X || min.Y > max.Y || min.Z > max.Z)
            {
                Host.Programs.RunError("backwards mins/maxs");
            }

            Vector3 rmin = min, rmax = max;

            // set derived values
            MathLib.Copy(ref rmin, out e.v.mins);
            MathLib.Copy(ref rmax, out e.v.maxs);
            var s = max - min;
            MathLib.Copy(ref s, out e.v.size);

            Host.Server.LinkEdict(e, false);
        }

        /*
        =================
        PF_setsize

        the size box is rotated by the current angle

        setsize (entity, minvector, maxvector)
        =================
        */

        private unsafe void PF_setsize()
        {
            var e = GetEdict(ProgramOperatorDef.OFS_PARM0);
            var min = GetVector(ProgramOperatorDef.OFS_PARM1);
            var max = GetVector(ProgramOperatorDef.OFS_PARM2);
            Copy(min, out Vector3 vmin);
            Copy(max, out Vector3 vmax);
            SetMinMaxSize(e, ref vmin, ref vmax);
        }

        /*
        =================
        PF_setmodel

        setmodel(entity, model)
        =================
        */

        private void PF_setmodel()
        {
            var e = GetEdict(ProgramOperatorDef.OFS_PARM0);
            var m_idx = GetInt(ProgramOperatorDef.OFS_PARM1);
            var m = Host.Programs.GetString(m_idx);

            // check to see if model was properly precached
            for (var i = 0; i < Host.Server.NetServer.model_precache.Length; i++)
            {
                var check = Host.Server.NetServer.model_precache[i];

                if (check == null)
                {
                    break;
                }

                if (check == m)
                {
                    e.v.model = m_idx; // m - pr_strings;
                    e.v.modelindex = i;

                    var mod = Host.Server.NetServer.models[(int)e.v.modelindex];

                    if (mod != null)
                    {
                        var mins = mod.BoundsMin;
                        var maxs = mod.BoundsMax;

                        SetMinMaxSize(e, ref mins, ref maxs);

                        mod.BoundsMin = mins;
                        mod.BoundsMax = maxs;
                    }
                    else
                    {
                        SetMinMaxSize(e, ref Utilities.ZeroVector, ref Utilities.ZeroVector);
                    }

                    return;
                }
            }

            Host.Programs.RunError("no precache: {0}\n", m);
        }

        /*
        =================
        PF_bprint

        broadcast print to everyone on server

        bprint(value)
        =================
        */

        private void PF_bprint()
        {
            var s = PF_VarString(0);
            Host.Server.BroadcastPrint(s);
        }

        /// <summary>
        /// PF_sprint
        /// single print to a specific client
        /// sprint(clientent, value)
        /// </summary>
        private void PF_sprint()
        {
            var entnum = Host.Server.NumForEdict(GetEdict(ProgramOperatorDef.OFS_PARM0));
            var s = PF_VarString(1);

            if (entnum < 1 || entnum > Host.Server.ServerStatic.maxclients)
            {
                Host.Console.Print("tried to sprint to a non-client\n");
                return;
            }

            var client = Host.Server.ServerStatic.clients[entnum - 1];

            client.message.WriteChar(ProtocolDef.svc_print);
            client.message.WriteString(s);
        }

        /*
        =================
        PF_centerprint

        single print to a specific client

        centerprint(clientent, value)
        =================
        */

        private void PF_centerprint()
        {
            var entnum = Host.Server.NumForEdict(GetEdict(ProgramOperatorDef.OFS_PARM0));
            var s = PF_VarString(1);

            if (entnum < 1 || entnum > Host.Server.ServerStatic.maxclients)
            {
                Host.Console.Print("tried to centerprint to a non-client\n");
                return;
            }

            var client = Host.Server.ServerStatic.clients[entnum - 1];

            client.message.WriteChar(ProtocolDef.svc_centerprint);
            client.message.WriteString(s);
        }

        /*
        =================
        PF_normalize

        vector normalize(vector)
        =================
        */

        private unsafe void PF_normalize()
        {
            var value1 = GetVector(ProgramOperatorDef.OFS_PARM0);
            Copy(value1, out Vector3 tmp);
            MathLib.Normalize(ref tmp);

            ReturnVector(ref tmp);
        }

        /*
        =================
        PF_vlen

        scalar vlen(vector)
        =================
        */

        private unsafe void PF_vlen()
        {
            var v = GetVector(ProgramOperatorDef.OFS_PARM0);
            var result = (float)Math.Sqrt((v[0] * v[0]) + (v[1] * v[1]) + (v[2] * v[2]));

            ReturnFloat(result);
        }

        /// <summary>
        /// PF_vectoyaw
        /// float vectoyaw(vector)
        /// </summary>
        private unsafe void PF_vectoyaw()
        {
            var value1 = GetVector(ProgramOperatorDef.OFS_PARM0);
            float yaw;
            if (value1[1] == 0 && value1[0] == 0)
            {
                yaw = 0;
            }
            else
            {
                yaw = (int)(Math.Atan2(value1[1], value1[0]) * 180 / Math.PI);
                if (yaw < 0)
                {
                    yaw += 360;
                }
            }

            ReturnFloat(yaw);
        }

        /*
        =================
        PF_vectoangles

        vector vectoangles(vector)
        =================
        */

        private unsafe void PF_vectoangles()
        {
            float yaw, pitch, forward;
            var value1 = GetVector(ProgramOperatorDef.OFS_PARM0);

            if (value1[1] == 0 && value1[0] == 0)
            {
                yaw = 0;
                pitch = value1[2] > 0 ? 90 : 270;
            }
            else
            {
                yaw = (int)(Math.Atan2(value1[1], value1[0]) * 180 / Math.PI);
                if (yaw < 0)
                {
                    yaw += 360;
                }

                forward = (float)Math.Sqrt((value1[0] * value1[0]) + (value1[1] * value1[1]));
                pitch = (int)(Math.Atan2(value1[2], forward) * 180 / Math.PI);
                if (pitch < 0)
                {
                    pitch += 360;
                }
            }

            var result = new Vector3(pitch, yaw, 0);
            ReturnVector(ref result);
        }

        /*
        =================
        PF_Random

        Returns a number from 0<= num < 1

        random()
        =================
        */

        private void PF_random()
        {
            var num = (MathLib.Random() & 0x7fff) / ((float)0x7fff);
            ReturnFloat(num);
        }

        /*
        =================
        PF_particle

        particle(origin, color, count)
        =================
        */

        private unsafe void PF_particle()
        {
            var org = GetVector(ProgramOperatorDef.OFS_PARM0);
            var dir = GetVector(ProgramOperatorDef.OFS_PARM1);
            var color = GetFloat(ProgramOperatorDef.OFS_PARM2);
            var count = GetFloat(ProgramOperatorDef.OFS_PARM3);
            Copy(org, out Vector3 vorg);
            Copy(dir, out Vector3 vdir);
            Host.Server.StartParticle(ref vorg, ref vdir, (int)color, (int)count);
        }

        /*
        =================
        PF_ambientsound

        =================
        */

        private unsafe void PF_ambientsound()
        {
            var pos = GetVector(ProgramOperatorDef.OFS_PARM0);
            var samp = GetString(ProgramOperatorDef.OFS_PARM1);
            var vol = GetFloat(ProgramOperatorDef.OFS_PARM2);
            var attenuation = GetFloat(ProgramOperatorDef.OFS_PARM3);

            // check to see if samp was properly precached
            for (var i = 0; i < Host.Server.NetServer.sound_precache.Length; i++)
            {
                if (Host.Server.NetServer.sound_precache[i] == null)
                {
                    break;
                }

                if (samp == Host.Server.NetServer.sound_precache[i])
                {
                    // add an svc_spawnambient command to the level signon packet
                    var msg = Host.Server.NetServer.signon;

                    msg.WriteByte(ProtocolDef.svc_spawnstaticsound);
                    for (var i2 = 0; i2 < 3; i2++)
                    {
                        msg.WriteCoord(pos[i2]);
                    }

                    msg.WriteByte(i);

                    msg.WriteByte((int)(vol * 255));
                    msg.WriteByte((int)(attenuation * 64));

                    return;
                }
            }

            Host.Console.Print("no precache: {0}\n", samp);
        }

        /*
        =================
        PF_sound

        Each entity can have eight independant sound sources, like voice,
        weapon, feet, etc.

        Channel 0 is an auto-allocate channel, the others override anything
        allready running on that entity/channel pair.

        An attenuation of 0 will play full volume everywhere in the level.
        Larger attenuations will drop off.

        =================
        */

        private void PF_sound()
        {
            var entity = GetEdict(ProgramOperatorDef.OFS_PARM0);
            var channel = (int)GetFloat(ProgramOperatorDef.OFS_PARM1);
            var sample = GetString(ProgramOperatorDef.OFS_PARM2);
            var volume = (int)(GetFloat(ProgramOperatorDef.OFS_PARM3) * 255);
            var attenuation = GetFloat(ProgramOperatorDef.OFS_PARM4);

            Host.Server.StartSound(entity, channel, sample, volume, attenuation);
        }

        /*
        =================
        PF_break

        break()
        =================
        */

        private void PF_break()
        {
            Host.Console.Print("break statement\n");
            //*(int *)-4 = 0;	// dump to debugger
        }

        /*
        =================
        PF_traceline

        Used for use tracing and shot targeting
        Traces are blocked by bbox and exact bsp entityes, and also slide box entities
        if the tryents flag is set.

        traceline (vector1, vector2, tryents)
        =================
        */

        private unsafe void PF_traceline()
        {
            var v1 = GetVector(ProgramOperatorDef.OFS_PARM0);
            var v2 = GetVector(ProgramOperatorDef.OFS_PARM1);
            var nomonsters = (int)GetFloat(ProgramOperatorDef.OFS_PARM2);
            var ent = GetEdict(ProgramOperatorDef.OFS_PARM3);

            Copy(v1, out Vector3 vec1);
            Copy(v2, out Vector3 vec2);
            var trace = Host.Server.Move(ref vec1, ref Utilities.ZeroVector, ref Utilities.ZeroVector, ref vec2, nomonsters, ent);

            Host.Programs.GlobalStruct.trace_allsolid = trace.allsolid ? 1 : 0;
            Host.Programs.GlobalStruct.trace_startsolid = trace.startsolid ? 1 : 0;
            Host.Programs.GlobalStruct.trace_fraction = trace.fraction;
            Host.Programs.GlobalStruct.trace_inwater = trace.inwater ? 1 : 0;
            Host.Programs.GlobalStruct.trace_inopen = trace.inopen ? 1 : 0;
            MathLib.Copy(ref trace.endpos, out Host.Programs.GlobalStruct.trace_endpos);
            MathLib.Copy(ref trace.plane.normal, out Host.Programs.GlobalStruct.trace_plane_normal);
            Host.Programs.GlobalStruct.trace_plane_dist = trace.plane.dist;
            Host.Programs.GlobalStruct.trace_ent = trace.ent != null ? Host.Server.EdictToProg(trace.ent) : Host.Server.EdictToProg(Host.Server.NetServer.edicts[0]);
        }

        private int PF_newcheckclient(int check)
        {
            // cycle to the next one

            if (check < 1)
            {
                check = 1;
            }

            if (check > Host.Server.ServerStatic.maxclients)
            {
                check = Host.Server.ServerStatic.maxclients;
            }

            var i = check + 1;
            if (check == Host.Server.ServerStatic.maxclients)
            {
                i = 1;
            }

            MemoryEdict ent;
            for (; ; i++)
            {
                if (i == Host.Server.ServerStatic.maxclients + 1)
                {
                    i = 1;
                }

                ent = Host.Server.EdictNum(i);

                if (i == check)
                {
                    break;  // didn't find anything else
                }

                if (ent.free)
                {
                    continue;
                }

                if (ent.v.health <= 0)
                {
                    continue;
                }

                if (((int)ent.v.flags & EdictFlags.FL_NOTARGET) != 0)
                {
                    continue;
                }

                // anything that is a client, or has a client as an enemy
                break;
            }

            // get the PVS for the entity
            var org = Utilities.ToVector(ref ent.v.origin) + Utilities.ToVector(ref ent.v.view_ofs);
            var leaf = Host.Server.NetServer.worldmodel.PointInLeaf(ref org);
            var pvs = Host.Server.NetServer.worldmodel.LeafPVS(leaf);
            Buffer.BlockCopy(pvs, 0, _CheckPvs, 0, pvs.Length);

            return i;
        }

        /// <summary>
        /// PF_checkclient
        /// Returns a client (or object that has a client enemy) that would be a
        /// valid target.
        ///
        /// If there are more than one valid options, they are cycled each frame
        ///
        /// If (self.origin + self.viewofs) is not in the PVS of the current target,
        /// it is not returned at all.
        ///
        /// name checkclient ()
        /// </summary>
        private void PF_checkclient()
        {
            // find a new check if on a new frame
            if (Host.Server.NetServer.time - Host.Server.NetServer.lastchecktime >= 0.1)
            {
                Host.Server.NetServer.lastcheck = PF_newcheckclient(Host.Server.NetServer.lastcheck);
                Host.Server.NetServer.lastchecktime = Host.Server.NetServer.time;
            }

            // return check if it might be visible
            var ent = Host.Server.EdictNum(Host.Server.NetServer.lastcheck);
            if (ent.free || ent.v.health <= 0)
            {
                ReturnEdict(Host.Server.NetServer.edicts[0]);
                return;
            }

            // if current entity can't possibly see the check entity, return 0
            var self = Host.Server.ProgToEdict(Host.Programs.GlobalStruct.self);
            var view = Utilities.ToVector(ref self.v.origin) + Utilities.ToVector(ref self.v.view_ofs);
            var leaf = Host.Server.NetServer.worldmodel.PointInLeaf(ref view);
            var l = Array.IndexOf(Host.Server.NetServer.worldmodel.Leaves, leaf) - 1;
            if ((l < 0) || (_CheckPvs[l >> 3] & (1 << (l & 7))) == 0)
            {
                _NotVisCount++;
                ReturnEdict(Host.Server.NetServer.edicts[0]);
                return;
            }

            // might be able to see it
            _InVisCount++;
            ReturnEdict(ent);
        }

        //============================================================================

        /// <summary>
        /// PF_stuffcmd
        /// Sends text over to the client's execution buffer
        /// stuffcmd (clientent, value)
        /// </summary>
        private void PF_stuffcmd()
        {
            var entnum = Host.Server.NumForEdict(GetEdict(ProgramOperatorDef.OFS_PARM0));
            if (entnum < 1 || entnum > Host.Server.ServerStatic.maxclients)
            {
                Host.Programs.RunError("Parm 0 not a client");
            }

            var str = GetString(ProgramOperatorDef.OFS_PARM1);

            var old = Host.HostClient;
            Host.HostClient = Host.Server.ServerStatic.clients[entnum - 1];
            Host.ClientCommands("{0}", str);
            Host.HostClient = old;
        }

        /// <summary>
        /// PF_localcmd
        /// Sends text over to the client's execution buffer
        /// localcmd (string)
        /// </summary>
        private void PF_localcmd()
        {
            var cmd = GetString(ProgramOperatorDef.OFS_PARM0);
            Host.Commands.Buffer.Append(cmd);
        }

        /*
        =================
        PF_cvar

        float cvar (string)
        =================
        */

        private void PF_cvar()
        {
            var str = GetString(ProgramOperatorDef.OFS_PARM0);
            var cvar = Host.CVars.Get(str);
            var singleValue = 0f;

            if (cvar != null)
            {
                if (cvar.ValueType == typeof(bool))
                {
                    singleValue = cvar.Get<bool>() ? 1f : 0f;
                }
                else if (cvar.ValueType == typeof(string))
                {
                    return;
                }
                else if (cvar.ValueType == typeof(float))
                {
                    singleValue = cvar.Get<float>();
                }
                else if (cvar.ValueType == typeof(int))
                {
                    singleValue = (float)cvar.Get<int>();
                }
            }

            ReturnFloat(singleValue);
        }

        /*
        =================
        PF_cvar_set

        float cvar (string)
        =================
        */

        private void PF_cvar_set()
        {
            Host.CVars.Set(GetString(ProgramOperatorDef.OFS_PARM0), GetString(ProgramOperatorDef.OFS_PARM1));
        }

        /*
        =================
        PF_findradius

        Returns a chain of entities that have origins within a spherical area

        findradius (origin, radius)
        =================
        */

        private unsafe void PF_findradius()
        {
            var chain = Host.Server.NetServer.edicts[0];

            var org = GetVector(ProgramOperatorDef.OFS_PARM0);
            var rad = GetFloat(ProgramOperatorDef.OFS_PARM1);

            Copy(org, out Vector3 vorg);

            for (var i = 1; i < Host.Server.NetServer.num_edicts; i++)
            {
                var ent = Host.Server.NetServer.edicts[i];
                if (ent.free)
                {
                    continue;
                }

                if (ent.v.solid == Solids.SOLID_NOT)
                {
                    continue;
                }

                var v = vorg - (Utilities.ToVector(ref ent.v.origin) +
                    ((Utilities.ToVector(ref ent.v.mins) + Utilities.ToVector(ref ent.v.maxs)) * 0.5f));
                if (v.Length() > rad)
                {
                    continue;
                }

                ent.v.chain = Host.Server.EdictToProg(chain);
                chain = ent;
            }

            ReturnEdict(chain);
        }

        /*
        =========
        PF_dprint
        =========
        */

        private void PF_dprint()
        {
            Host.Console.DPrint(PF_VarString(0));
        }

        private void PF_ftos()
        {
            var v = GetFloat(ProgramOperatorDef.OFS_PARM0);

            if (v == (int)v)
            {
                SetTempString(string.Format("{0}", (int)v));
            }
            else
            {
                SetTempString(string.Format("{0:F1}", v)); //  sprintf(pr_string_temp, "%5.1f", v);
            }

            ReturnInt(_TempString);
        }

        private void PF_fabs()
        {
            var v = GetFloat(ProgramOperatorDef.OFS_PARM0);
            ReturnFloat(Math.Abs(v));
        }

        private unsafe void PF_vtos()
        {
            var v = GetVector(ProgramOperatorDef.OFS_PARM0);
            SetTempString(string.Format("'{0,5:F1} {1,5:F1} {2,5:F1}'", v[0], v[1], v[2]));
            ReturnInt(_TempString);
        }

        private void PF_Spawn()
        {
            var ed = Host.Server.AllocEdict();
            ReturnEdict(ed);
        }

        private void PF_Remove()
        {
            var ed = GetEdict(ProgramOperatorDef.OFS_PARM0);
            Host.Server.FreeEdict(ed);
        }

        /// <summary>
        /// PF_Find
        /// entity (entity start, .string field, string match) find = #5;
        /// </summary>
        private void PF_Find()
        {
            var e = GetInt(ProgramOperatorDef.OFS_PARM0);
            var f = GetInt(ProgramOperatorDef.OFS_PARM1);
            var s = GetString(ProgramOperatorDef.OFS_PARM2);
            if (s == null)
            {
                Host.Programs.RunError("PF_Find: bad search string");
            }

            for (e++; e < Host.Server.NetServer.num_edicts; e++)
            {
                var ed = Host.Server.EdictNum(e);
                if (ed.free)
                {
                    continue;
                }

                var t = Host.Programs.GetString(ed.GetInt(f)); // E_STRING(ed, f);
                if (string.IsNullOrEmpty(t))
                {
                    continue;
                }

                if (t == s)
                {
                    ReturnEdict(ed);
                    return;
                }
            }

            ReturnEdict(Host.Server.NetServer.edicts[0]);
        }

        private void CheckEmptyString(string s)
        {
            if (s == null || s.Length == 0 || s[0] <= ' ')
            {
                Host.Programs.RunError("Bad string");
            }
        }

        private void PF_precache_file()
        {
            // precache_file is only used to copy files with qcc, it does nothing
            ReturnInt(GetInt(ProgramOperatorDef.OFS_PARM0));
        }

        private void PF_precache_sound()
        {
            if (!Host.Server.IsLoading)
            {
                Host.Programs.RunError("PF_Precache_*: Precache can only be done in spawn functions");
            }

            var s = GetString(ProgramOperatorDef.OFS_PARM0);
            ReturnInt(GetInt(ProgramOperatorDef.OFS_PARM0)); //  G_INT(OFS_RETURN) = G_INT(OFS_PARM0);
            CheckEmptyString(s);

            for (var i = 0; i < QDef.MAX_SOUNDS; i++)
            {
                if (Host.Server.NetServer.sound_precache[i] == null)
                {
                    Host.Server.NetServer.sound_precache[i] = s;
                    return;
                }
                if (Host.Server.NetServer.sound_precache[i] == s)
                {
                    return;
                }
            }
            Host.Programs.RunError("PF_precache_sound: overflow");
        }

        private void PF_precache_model()
        {
            if (!Host.Server.IsLoading)
            {
                Host.Programs.RunError("PF_Precache_*: Precache can only be done in spawn functions");
            }

            var s = GetString(ProgramOperatorDef.OFS_PARM0);
            ReturnInt(GetInt(ProgramOperatorDef.OFS_PARM0)); //G_INT(OFS_RETURN) = G_INT(OFS_PARM0);
            CheckEmptyString(s);

            for (var i = 0; i < QDef.MAX_MODELS; i++)
            {
                if (Host.Server.NetServer.model_precache[i] == null)
                {
                    Host.Server.NetServer.model_precache[i] = s;

                    var n = s.ToLower();
                    ModelType type = (n.StartsWith("*") && !n.Contains(".mdl")) || n.Contains(".bsp")
                        ? ModelType.mod_brush
                        : n.Contains(".mdl") ? ModelType.mod_alias : ModelType.mod_sprite;
                    Host.Server.NetServer.models[i] = Host.Model.ForName(s, true, type);
                    return;
                }
                if (Host.Server.NetServer.model_precache[i] == s)
                {
                    return;
                }
            }
            Host.Programs.RunError("PF_precache_model: overflow");
        }

        private void PF_coredump()
        {
            Host.Programs.PrintEdicts(null);
        }

        private void PF_traceon()
        {
            Host.Programs.Trace = true;
        }

        private void PF_traceoff()
        {
            Host.Programs.Trace = false;
        }

        private void PF_eprint()
        {
            Host.Programs.PrintNum(Host.Server.NumForEdict(GetEdict(ProgramOperatorDef.OFS_PARM0)));
        }

        /// <summary>
        /// PF_walkmove
        /// float(float yaw, float dist) walkmove
        /// </summary>
        private void PF_walkmove()
        {
            var ent = Host.Server.ProgToEdict(Host.Programs.GlobalStruct.self);
            var yaw = GetFloat(ProgramOperatorDef.OFS_PARM0);
            var dist = GetFloat(ProgramOperatorDef.OFS_PARM1);

            if (((int)ent.v.flags & (EdictFlags.FL_ONGROUND | EdictFlags.FL_FLY | EdictFlags.FL_SWIM)) == 0)
            {
                ReturnFloat(0);
                return;
            }

            yaw = (float)(yaw * Math.PI * 2.0 / 360.0);

            Vector3f move;
            move.x = (float)Math.Cos(yaw) * dist;
            move.y = (float)Math.Sin(yaw) * dist;
            move.z = 0;

            // save program state, because SV_movestep may call other progs
            var oldf = Host.Programs.xFunction;
            var oldself = Host.Programs.GlobalStruct.self;

            ReturnFloat(Host.Server.MoveStep(ent, ref move, true) ? 1 : 0);

            // restore program state
            Host.Programs.xFunction = oldf;
            Host.Programs.GlobalStruct.self = oldself;
        }

        /*
        ===============
        PF_droptofloor

        void() droptofloor
        ===============
        */

        private void PF_droptofloor()
        {
            var ent = Host.Server.ProgToEdict(Host.Programs.GlobalStruct.self);

            MathLib.Copy(ref ent.v.origin, out Vector3 org);
            MathLib.Copy(ref ent.v.mins, out Vector3 mins);
            MathLib.Copy(ref ent.v.maxs, out Vector3 maxs);
            var end = org;
            end.Z -= 256;

            var trace = Host.Server.Move(ref org, ref mins, ref maxs, ref end, 0, ent);

            if (trace.fraction == 1 || trace.allsolid)
            {
                ReturnFloat(0);
            }
            else
            {
                MathLib.Copy(ref trace.endpos, out ent.v.origin);
                Host.Server.LinkEdict(ent, false);
                ent.v.flags = (int)ent.v.flags | EdictFlags.FL_ONGROUND;
                ent.v.groundentity = Host.Server.EdictToProg(trace.ent);
                ReturnFloat(1);
            }
        }

        /*
        ===============
        PF_lightstyle

        void(float style, string value) lightstyle
        ===============
        */

        private void PF_lightstyle()
        {
            var style = (int)GetFloat(ProgramOperatorDef.OFS_PARM0); // Uze: ???
            var val = GetString(ProgramOperatorDef.OFS_PARM1);

            // change the string in sv
            Host.Server.NetServer.lightstyles[style] = val;

            // send message to all clients on this server
            if (!Host.Server.IsActive)
            {
                return;
            }

            for (var j = 0; j < Host.Server.ServerStatic.maxclients; j++)
            {
                var client = Host.Server.ServerStatic.clients[j];
                if (client.active || client.spawned)
                {
                    client.message.WriteChar(ProtocolDef.svc_lightstyle);
                    client.message.WriteChar(style);
                    client.message.WriteString(val);
                }
            }
        }

        private void PF_rint()
        {
            var f = GetFloat(ProgramOperatorDef.OFS_PARM0);
            if (f > 0)
            {
                ReturnFloat((int)(f + 0.5));
            }
            else
            {
                ReturnFloat((int)(f - 0.5));
            }
        }

        private void PF_floor()
        {
            ReturnFloat((float)Math.Floor(GetFloat(ProgramOperatorDef.OFS_PARM0)));
        }

        private void PF_ceil()
        {
            ReturnFloat((float)Math.Ceiling(GetFloat(ProgramOperatorDef.OFS_PARM0)));
        }

        /// <summary>
        /// PF_checkbottom
        /// </summary>
        private void PF_checkbottom()
        {
            var ent = GetEdict(ProgramOperatorDef.OFS_PARM0);
            ReturnFloat(Host.Server.CheckBottom(ent) ? 1 : 0);
        }

        /// <summary>
        /// PF_pointcontents
        /// </summary>
        private unsafe void PF_pointcontents()
        {
            var v = GetVector(ProgramOperatorDef.OFS_PARM0);
            Copy(v, out Vector3 tmp);
            ReturnFloat(Host.Server.PointContents(ref tmp));
        }

        /*
        =============
        PF_nextent

        entity nextent(entity)
        =============
        */

        private void PF_nextent()
        {
            var i = Host.Server.NumForEdict(GetEdict(ProgramOperatorDef.OFS_PARM0));
            while (true)
            {
                i++;
                if (i == Host.Server.NetServer.num_edicts)
                {
                    ReturnEdict(Host.Server.NetServer.edicts[0]);
                    return;
                }
                var ent = Host.Server.EdictNum(i);
                if (!ent.free)
                {
                    ReturnEdict(ent);
                    return;
                }
            }
        }

        /*
        =============
        PF_aim

        Pick a vector for the player to shoot along
        vector aim(entity, missilespeed)
        =============
        */

        private void PF_aim()
        {
            var ent = GetEdict(ProgramOperatorDef.OFS_PARM0);
            var start = Utilities.ToVector(ref ent.v.origin);
            start.Z += 20;

            // try sending a trace straight
            MathLib.Copy(ref Host.Programs.GlobalStruct.v_forward, out Vector3 dir);
            var end = start + (dir * 2048);
            var tr = Host.Server.Move(ref start, ref Utilities.ZeroVector, ref Utilities.ZeroVector, ref end, 0, ent);
            if (tr.ent != null && tr.ent.v.takedamage == Damages.DAMAGE_AIM &&
                (Host.Cvars.TeamPlay.Get<int>() == 0 || ent.v.team <= 0 || ent.v.team != tr.ent.v.team))
            {
                ReturnVector(ref Host.Programs.GlobalStruct.v_forward);
                return;
            }

            // try all possible entities
            var bestdir = dir;
            var bestdist = Host.Server.Aim;
            MemoryEdict bestent = null;

            for (var i = 1; i < Host.Server.NetServer.num_edicts; i++)
            {
                var check = Host.Server.NetServer.edicts[i];
                if (check.v.takedamage != Damages.DAMAGE_AIM)
                {
                    continue;
                }

                if (check == ent)
                {
                    continue;
                }

                if (Host.Cvars.TeamPlay.Get<int>() > 0 && ent.v.team > 0 && ent.v.team == check.v.team)
                {
                    continue;   // don't aim at teammate
                }

                MathLib.VectorAdd(ref check.v.mins, ref check.v.maxs, out Vector3f tmp);
                MathLib.VectorMA(ref check.v.origin, 0.5f, ref tmp, out tmp);
                MathLib.Copy(ref tmp, out end);

                dir = end - start;
                MathLib.Normalize(ref dir);
                var dist = Vector3.Dot(dir, Utilities.ToVector(ref Host.Programs.GlobalStruct.v_forward));
                if (dist < bestdist)
                {
                    continue;   // to far to turn
                }

                tr = Host.Server.Move(ref start, ref Utilities.ZeroVector, ref Utilities.ZeroVector, ref end, 0, ent);
                if (tr.ent == check)
                {	// can shoot at this one
                    bestdist = dist;
                    bestent = check;
                }
            }

            if (bestent != null)
            {
                MathLib.VectorSubtract(ref bestent.v.origin, ref ent.v.origin, out Vector3f dir2);
                var dist = MathLib.DotProduct(ref dir2, ref Host.Programs.GlobalStruct.v_forward);
                MathLib.VectorScale(ref Host.Programs.GlobalStruct.v_forward, dist, out Vector3f end2);
                end2.z = dir2.z;
                MathLib.Normalize(ref end2);
                ReturnVector(ref end2);
            }
            else
            {
                ReturnVector(ref bestdir);
            }
        }

        /*
        ==============
        PF_changeyaw

        This was a major timewaster in progs, so it was converted to C
        ==============
        */

        private void PF_WriteByte()
        {
            WriteDest.WriteByte((int)GetFloat(ProgramOperatorDef.OFS_PARM1));
        }

        private void PF_WriteChar()
        {
            WriteDest.WriteChar((int)GetFloat(ProgramOperatorDef.OFS_PARM1));
        }

        private void PF_WriteShort()
        {
            WriteDest.WriteShort((int)GetFloat(ProgramOperatorDef.OFS_PARM1));
        }

        private void PF_WriteLong()
        {
            WriteDest.WriteLong((int)GetFloat(ProgramOperatorDef.OFS_PARM1));
        }

        private void PF_WriteAngle()
        {
            WriteDest.WriteAngle(GetFloat(ProgramOperatorDef.OFS_PARM1));
        }

        private void PF_WriteCoord()
        {
            WriteDest.WriteCoord(GetFloat(ProgramOperatorDef.OFS_PARM1));
        }

        private void PF_WriteString()
        {
            WriteDest.WriteString(GetString(ProgramOperatorDef.OFS_PARM1));
        }

        private void PF_WriteEntity()
        {
            WriteDest.WriteShort(Host.Server.NumForEdict(GetEdict(ProgramOperatorDef.OFS_PARM1)));
        }

        private void PF_makestatic()
        {
            var ent = GetEdict(ProgramOperatorDef.OFS_PARM0);
            var msg = Host.Server.NetServer.signon;

            msg.WriteByte(ProtocolDef.svc_spawnstatic);
            msg.WriteByte(Host.Server.ModelIndex(Host.Programs.GetString(ent.v.model)));
            msg.WriteByte((int)ent.v.frame);
            msg.WriteByte((int)ent.v.colormap);
            msg.WriteByte((int)ent.v.skin);
            for (var i = 0; i < 3; i++)
            {
                msg.WriteCoord(MathLib.Comp(ref ent.v.origin, i));
                msg.WriteAngle(MathLib.Comp(ref ent.v.angles, i));
            }

            // throw the entity away now
            Host.Server.FreeEdict(ent);
        }

        /*
        ==============
        PF_setspawnparms
        ==============
        */

        private void PF_setspawnparms()
        {
            var ent = GetEdict(ProgramOperatorDef.OFS_PARM0);
            var i = Host.Server.NumForEdict(ent);
            if (i < 1 || i > Host.Server.ServerStatic.maxclients)
            {
                Host.Programs.RunError("Entity is not a client");
            }

            // copy spawn parms out of the client_t
            var client = Host.Server.ServerStatic.clients[i - 1];

            Host.Programs.GlobalStruct.SetParams(client.spawn_parms);
        }

        /*
        ==============
        PF_changelevel
        ==============
        */

        private void PF_changelevel()
        {
            // make sure we don't issue two changelevels
            if (Host.Server.ServerStatic.changelevel_issued)
            {
                return;
            }

            Host.Server.ServerStatic.changelevel_issued = true;

            var s = GetString(ProgramOperatorDef.OFS_PARM0);
            Host.Commands.Buffer.Append(string.Format("changelevel {0}\n", s));
        }

        private void PF_Fixme()
        {
            Host.Programs.RunError("unimplemented bulitin");
        }
    }
}
