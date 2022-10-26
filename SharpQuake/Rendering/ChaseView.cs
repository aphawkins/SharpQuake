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

// chase.c -- chase camera code

namespace SharpQuake
{
    using System;
    using OpenTK;
    using SharpQuake.Framework;
    using SharpQuake.Framework.World;

    /// <summary>
    /// Chase_functions
    /// </summary>
    public class ChaseView
    {
        /// <summary>
        /// chase_active.value != 0
        /// </summary>
        public bool IsActive => Host.Cvars.Active.Get<bool>();

        private Vector3 _Dest;

        // Instances
        public Host Host
        {
            get;
            private set;
        }

        public ChaseView(Host host)
        {
            Host = host;
        }

        // Chase_Init
        public void Initialise()
        {
            if (Host.Cvars.Back == null)
            {
                Host.Cvars.Back = Host.CVars.Add("chase_back", 100f);
                Host.Cvars.Up = Host.CVars.Add("chase_up", 16f);
                Host.Cvars.Right = Host.CVars.Add("chase_right", 0f);
                Host.Cvars.Active = Host.CVars.Add("chase_active", false);
            }
        }

        // Chase_Reset
        public static void Reset()
        {
            // for respawning and teleporting
            //	start position 12 units behind head
        }

        // Chase_Update
        public void Update()
        {
            // if can't see player, reset
            MathLib.AngleVectors(ref Host.Client.Cl.viewangles, out Vector3 forward, out Vector3 right, out _);

            // calc exact destination
            _Dest = Host.RenderContext.RefDef.vieworg - (forward * Host.Cvars.Back.Get<float>()) - (right * Host.Cvars.Right.Get<float>());
            _Dest.Z = Host.RenderContext.RefDef.vieworg.Z + Host.Cvars.Up.Get<float>();

            // find the spot the player is looking at
            var dest = Host.RenderContext.RefDef.vieworg + (forward * 4096);

            TraceLine(ref Host.RenderContext.RefDef.vieworg, ref dest, out Vector3 stop);

            // calculate pitch to look at the same spot from camera
            stop -= Host.RenderContext.RefDef.vieworg;
            Vector3.Dot(ref stop, ref forward, out float dist);
            if (dist < 1)
            {
                dist = 1;
            }

            Host.RenderContext.RefDef.viewangles.X = (float)(-Math.Atan(stop.Z / dist) / Math.PI * 180.0);
            //r_refdef.viewangles[PITCH] = -atan(stop[2] / dist) / M_PI * 180;

            // move towards destination
            Host.RenderContext.RefDef.vieworg = _Dest; //VectorCopy(chase_dest, r_refdef.vieworg);
        }

        private void TraceLine(ref Vector3 start, ref Vector3 end, out Vector3 impact)
        {
            var trace = new Trace_t();

            Host.Server.RecursiveHullCheck(Host.Client.Cl.worldmodel.Hulls[0], 0, 0, 1, ref start, ref end, trace);

            impact = trace.endpos; // VectorCopy(trace.endpos, impact);
        }
    }
}
