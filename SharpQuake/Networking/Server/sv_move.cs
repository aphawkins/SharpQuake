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

// sv_move.c

namespace SharpQuake
{
    using System;
    using System.Numerics;
    using SharpQuake.Framework;
    using SharpQuake.Framework.IO.BSP;
    using SharpQuake.Framework.World;

    public partial class Server
    {
        private const float DI_NODIR = -1;

        /// <summary>
        /// SV_movestep
        /// Called by monster program code.
        /// The move will be adjusted for slopes and stairs, but if the move isn't
        /// possible, no move is done, false is returned, and
        /// pr_global_struct.trace_normal is set to the normal of the blocking wall
        /// </summary>
        public bool MoveStep(MemoryEdict ent, ref Vector3f move, bool relink)
        {
            Trace_t trace;

            // try the move
            var oldorg = ent.v.origin;
            MathLib.VectorAdd(ref ent.v.origin, ref move, out Vector3f neworg);

            // flying monsters don't step up
            if (((int)ent.v.flags & (EdictFlags.FL_SWIM | EdictFlags.FL_FLY)) != 0)
            {
                // try one move with vertical motion, then one without
                for (var i = 0; i < 2; i++)
                {
                    MathLib.VectorAdd(ref ent.v.origin, ref move, out neworg);
                    var enemy = ProgToEdict(ent.v.enemy);
                    if (i == 0 && enemy != NetServer.edicts[0])
                    {
                        var dz = ent.v.origin.z - enemy.v.origin.z;
                        if (dz > 40)
                        {
                            neworg.z -= 8;
                        }

                        if (dz < 30)
                        {
                            neworg.z += 8;
                        }
                    }

                    trace = Move(ref ent.v.origin, ref ent.v.mins, ref ent.v.maxs, ref neworg, 0, ent);
                    if (trace.fraction == 1)
                    {
                        if (((int)ent.v.flags & EdictFlags.FL_SWIM) != 0 &&
                            PointContents(ref trace.endpos) == (int)Q1Contents.Empty)
                        {
                            return false;  // swim monster left water
                        }

                        MathLib.Copy(ref trace.endpos, out ent.v.origin);
                        if (relink)
                        {
                            LinkEdict(ent, true);
                        }

                        return true;
                    }

                    if (enemy == NetServer.edicts[0])
                    {
                        break;
                    }
                }

                return false;
            }

            // push down from a step height above the wished position
            neworg.z += STEPSIZE;
            var end = neworg;
            end.z -= STEPSIZE * 2;

            trace = Move(ref neworg, ref ent.v.mins, ref ent.v.maxs, ref end, 0, ent);

            if (trace.allsolid)
            {
                return false;
            }

            if (trace.startsolid)
            {
                neworg.z -= STEPSIZE;
                trace = Move(ref neworg, ref ent.v.mins, ref ent.v.maxs, ref end, 0, ent);
                if (trace.allsolid || trace.startsolid)
                {
                    return false;
                }
            }
            if (trace.fraction == 1)
            {
                // if monster had the ground pulled out, go ahead and fall
                if (((int)ent.v.flags & EdictFlags.FL_PARTIALGROUND) != 0)
                {
                    MathLib.VectorAdd(ref ent.v.origin, ref move, out ent.v.origin);
                    if (relink)
                    {
                        LinkEdict(ent, true);
                    }

                    ent.v.flags = (int)ent.v.flags & ~EdictFlags.FL_ONGROUND;
                    return true;
                }

                return false;		// walked off an edge
            }

            // check point traces down for dangling corners
            MathLib.Copy(ref trace.endpos, out ent.v.origin);

            if (!CheckBottom(ent))
            {
                if (((int)ent.v.flags & EdictFlags.FL_PARTIALGROUND) != 0)
                {
                    // entity had floor mostly pulled out from underneath it
                    // and is trying to correct
                    if (relink)
                    {
                        LinkEdict(ent, true);
                    }

                    return true;
                }
                ent.v.origin = oldorg;
                return false;
            }

            if (((int)ent.v.flags & EdictFlags.FL_PARTIALGROUND) != 0)
            {
                ent.v.flags = (int)ent.v.flags & ~EdictFlags.FL_PARTIALGROUND;
            }
            ent.v.groundentity = EdictToProg(trace.ent);

            // the move is ok
            if (relink)
            {
                LinkEdict(ent, true);
            }

            return true;
        }

        /// <summary>
        /// SV_CheckBottom
        /// </summary>
        public bool CheckBottom(MemoryEdict ent)
        {
            MathLib.VectorAdd(ref ent.v.origin, ref ent.v.mins, out Vector3f mins);
            MathLib.VectorAdd(ref ent.v.origin, ref ent.v.maxs, out Vector3f maxs);

            // if all of the points under the corners are solid world, don't bother
            // with the tougher checks
            // the corners must be within 16 of the midpoint
            Vector3 start;
            start.Z = mins.z - 1;
            for (var x = 0; x <= 1; x++)
            {
                for (var y = 0; y <= 1; y++)
                {
                    start.X = x != 0 ? maxs.x : mins.x;
                    start.Y = y != 0 ? maxs.y : mins.y;
                    if (PointContents(ref start) != (int)Q1Contents.Solid)
                    {
                        goto RealCheck;
                    }
                }
            }

            return true;        // we got out easy

        RealCheck:

            //
            // check it for real...
            //
            start.Z = mins.z;

            // the midpoint must be within 16 of the bottom
            start.X = (mins.x + maxs.x) * 0.5f;
            start.Y = (mins.y + maxs.y) * 0.5f;
            var stop = start;
            stop.Z -= 2 * STEPSIZE;
            var trace = Move(ref start, ref Utilities.ZeroVector, ref Utilities.ZeroVector, ref stop, 1, ent);

            if (trace.fraction == 1.0)
            {
                return false;
            }

            var mid = trace.endpos.Z;
            var bottom = mid;

            // the corners must be within 16 of the midpoint
            for (var x = 0; x <= 1; x++)
            {
                for (var y = 0; y <= 1; y++)
                {
                    start.X = stop.X = x != 0 ? maxs.x : mins.x;
                    start.Y = stop.Y = y != 0 ? maxs.y : mins.y;

                    trace = Move(ref start, ref Utilities.ZeroVector, ref Utilities.ZeroVector, ref stop, 1, ent);

                    if (trace.fraction != 1.0 && trace.endpos.Z > bottom)
                    {
                        bottom = trace.endpos.Z;
                    }

                    if (trace.fraction == 1.0 || mid - trace.endpos.Z > STEPSIZE)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// SV_MoveToGoal
        /// </summary>
        public void MoveToGoal()
        {
            var ent = ProgToEdict(Host.Programs.GlobalStruct.self);
            var goal = ProgToEdict(ent.v.goalentity);
            var dist = Host.ProgramsBuiltIn.GetFloat(ProgramOperatorDef.OFS_PARM0);

            if (((int)ent.v.flags & (EdictFlags.FL_ONGROUND | EdictFlags.FL_FLY | EdictFlags.FL_SWIM)) == 0)
            {
                Host.ProgramsBuiltIn.ReturnFloat(0);
                return;
            }

            // if the next step hits the enemy, return immediately
            if (ProgToEdict(ent.v.enemy) != NetServer.edicts[0] && CloseEnough(ent, goal, dist))
            {
                return;
            }

            // bump around...
            if ((MathLib.Random() & 3) == 1 || !StepDirection(ent, ent.v.ideal_yaw, dist))
            {
                NewChaseDir(ent, goal, dist);
            }
        }

        /// <summary>
        /// SV_CloseEnough
        /// </summary>
        private static bool CloseEnough(MemoryEdict ent, MemoryEdict goal, float dist)
        {
            if (goal.v.absmin.x > ent.v.absmax.x + dist)
            {
                return false;
            }

            if (goal.v.absmin.y > ent.v.absmax.y + dist)
            {
                return false;
            }

            if (goal.v.absmin.z > ent.v.absmax.z + dist)
            {
                return false;
            }

            if (goal.v.absmax.x < ent.v.absmin.x - dist)
            {
                return false;
            }

            if (goal.v.absmax.y < ent.v.absmin.y - dist)
            {
                return false;
            }

            if (goal.v.absmax.z < ent.v.absmin.z - dist)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// SV_StepDirection
        /// Turns to the movement direction, and walks the current distance if facing it.
        /// </summary>
        private bool StepDirection(MemoryEdict ent, float yaw, float dist)
        {
            ent.v.ideal_yaw = yaw;
            Host.ProgramsBuiltIn.PF_changeyaw();

            yaw = (float)(yaw * Math.PI * 2.0 / 360);
            Vector3f move;
            move.x = (float)Math.Cos(yaw) * dist;
            move.y = (float)Math.Sin(yaw) * dist;
            move.z = 0;

            var oldorigin = ent.v.origin;
            if (MoveStep(ent, ref move, false))
            {
                var delta = ent.v.angles.y - ent.v.ideal_yaw;
                if (delta is > 45 and < 315)
                {
                    // not turned far enough, so don't take the step
                    ent.v.origin = oldorigin;
                }
                LinkEdict(ent, true);
                return true;
            }
            LinkEdict(ent, true);

            return false;
        }

        /// <summary>
        /// SV_NewChaseDir
        /// </summary>
        private void NewChaseDir(MemoryEdict actor, MemoryEdict enemy, float dist)
        {
            var olddir = MathLib.AngleMod((int)(actor.v.ideal_yaw / 45) * 45);
            var turnaround = MathLib.AngleMod(olddir - 180);

            var deltax = enemy.v.origin.x - actor.v.origin.x;
            var deltay = enemy.v.origin.y - actor.v.origin.y;
            Vector3f d;
            d.y = deltax > 10 ? 0 : deltax < -10 ? 180 : DI_NODIR;
            d.z = deltay < -10 ? 270 : deltay > 10 ? 90 : DI_NODIR;

            // try direct route
            float tdir;
            if (d.y != DI_NODIR && d.z != DI_NODIR)
            {
                tdir = d.y == 0 ? d.z == 90 ? 45 : 315 : d.z == 90 ? 135 : 215;

                if (tdir != turnaround && StepDirection(actor, tdir, dist))
                {
                    return;
                }
            }

            // try other directions
            if ((MathLib.Random() & 3 & 1) != 0 || Math.Abs(deltay) > Math.Abs(deltax))
            {
                tdir = d.y;
                d.y = d.z;
                d.z = tdir;
            }

            if (d.y != DI_NODIR && d.y != turnaround && StepDirection(actor, d.y, dist))
            {
                return;
            }

            if (d.z != DI_NODIR && d.z != turnaround && StepDirection(actor, d.z, dist))
            {
                return;
            }

            // there is no direct path to the player, so pick another direction

            if (olddir != DI_NODIR && StepDirection(actor, olddir, dist))
            {
                return;
            }

            if ((MathLib.Random() & 1) != 0) 	//randomly determine direction of search
            {
                for (tdir = 0; tdir <= 315; tdir += 45)
                {
                    if (tdir != turnaround && StepDirection(actor, tdir, dist))
                    {
                        return;
                    }
                }
            }
            else
            {
                for (tdir = 315; tdir >= 0; tdir -= 45)
                {
                    if (tdir != turnaround && StepDirection(actor, tdir, dist))
                    {
                        return;
                    }
                }
            }

            if (turnaround != DI_NODIR && StepDirection(actor, turnaround, dist))
            {
                return;
            }

            actor.v.ideal_yaw = olddir;		// can't move

            // if a bridge was pulled out from underneath a monster, it may not have
            // a valid standing position at all

            if (!CheckBottom(actor))
            {
                FixCheckBottom(actor);
            }
        }

        /// <summary>
        /// SV_FixCheckBottom
        /// </summary>
        private static void FixCheckBottom(MemoryEdict ent)
        {
            ent.v.flags = (int)ent.v.flags | EdictFlags.FL_PARTIALGROUND;
        }
    }
}
