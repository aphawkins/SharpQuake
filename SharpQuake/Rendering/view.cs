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

// view.h
// view.c -- player eye positioning

// The view is allowed to move slightly from it's true position for bobbing,
// but if it exceeds 8 pixels linear distance (spherical, not box), the list of
// entities sent from the server may not include everything in the pvs, especially
// when crossing a water boudnary.

namespace SharpQuake
{
    using System;
    using SharpQuake.Framework;
    using SharpQuake.Framework.IO.BSP;
    using SharpQuake.Framework.IO;
    using System.Numerics;
    using System.Drawing;

    /// <summary>
    /// V_functions
    /// </summary>
    public class View
    {
        public float Crosshair => Host.Cvars.Crosshair.Get<float>();

        public float Gamma => Host.Cvars.Gamma.Get<float>();

        public Color Blend;
        private static readonly Vector3 SmallOffset = Vector3.One / 32f;

        private readonly byte[] _GammaTable; // [256];	// palette is sent through this
        private readonly ClientShift _CShift_empty;// = { { 130, 80, 50 }, 0 };
        private readonly ClientShift _CShift_water;// = { { 130, 80, 50 }, 128 };
        private readonly ClientShift _CShift_slime;// = { { 0, 25, 5 }, 150 };
        private readonly ClientShift _CShift_lava;// = { { 255, 80, 0 }, 150 };

        // v_blend[4]		// rgba 0.0 - 1.0
        private readonly byte[,] _Ramps = new byte[3, 256]; // ramps[3][256]

        private Vector3 _right; // vec3_t right

        private float _DmgTime; // v_dmg_time
        private float _DmgRoll; // v_dmg_roll
        private float _DmgPitch; // v_dmg_pitch

        private float _OldZ = 0; // static oldz  from CalcRefdef()
        private float _OldYaw = 0; // static oldyaw from CalcGunAngle
        private float _OldPitch = 0; // static oldpitch from CalcGunAngle
        private float _OldGammaValue; // static float oldgammavalue from CheckGamma

        // Instances
        private Host Host
        {
            get;
            set;
        }

        // V_Init
        public void Initialise()
        {
            Host.Commands.Add("v_cshift", CShift_f);
            Host.Commands.Add("bf", BonusFlash_f);
            Host.Commands.Add("centerview", StartPitchDrift);

            if (Host.Cvars.LcdX == null)
            {
                Host.Cvars.LcdX = Host.CVars.Add("lcd_x", 0f);
                Host.Cvars.LcdYaw = Host.CVars.Add("lcd_yaw", 0f);

                Host.Cvars.ScrOfsX = Host.CVars.Add("scr_ofsx", 0f);
                Host.Cvars.ScrOfsY = Host.CVars.Add("scr_ofsy", 0f);
                Host.Cvars.ScrOfsZ = Host.CVars.Add("scr_ofsz", 0f);

                Host.Cvars.ClRollSpeed = Host.CVars.Add("cl_rollspeed", 200f);
                Host.Cvars.ClRollAngle = Host.CVars.Add("cl_rollangle", 2.0f);

                Host.Cvars.ClBob = Host.CVars.Add("cl_bob", 0.02f);
                Host.Cvars.ClBobCycle = Host.CVars.Add("cl_bobcycle", 0.6f);
                Host.Cvars.ClBobUp = Host.CVars.Add("cl_bobup", 0.5f);

                Host.Cvars.KickTime = Host.CVars.Add("v_kicktime", 0.5f);
                Host.Cvars.KickRoll = Host.CVars.Add("v_kickroll", 0.6f);
                Host.Cvars.KickPitch = Host.CVars.Add("v_kickpitch", 0.6f);

                Host.Cvars.IYawCycle = Host.CVars.Add("v_iyaw_cycle", 2f);
                Host.Cvars.IRollCycle = Host.CVars.Add("v_iroll_cycle", 0.5f);
                Host.Cvars.IPitchCycle = Host.CVars.Add("v_ipitch_cycle", 1f);
                Host.Cvars.IYawLevel = Host.CVars.Add("v_iyaw_level", 0.3f);
                Host.Cvars.IRollLevel = Host.CVars.Add("v_iroll_level", 0.1f);
                Host.Cvars.IPitchLevel = Host.CVars.Add("v_ipitch_level", 0.3f);

                Host.Cvars.IdleScale = Host.CVars.Add("v_idlescale", 0f);

                Host.Cvars.Crosshair = Host.CVars.Add("crosshair", 0f, ClientVariableFlags.Archive);
                Host.Cvars.ClCrossX = Host.CVars.Add("cl_crossx", 0f);
                Host.Cvars.ClCrossY = Host.CVars.Add("cl_crossy", 0f);

                Host.Cvars.glCShiftPercent = Host.CVars.Add("gl_cshiftpercent", 100f);

                Host.Cvars.CenterMove = Host.CVars.Add("v_centermove", 0.15f);
                Host.Cvars.CenterSpeed = Host.CVars.Add("v_centerspeed", 500f);

                BuildGammaTable(1.0f);    // no gamma yet
                Host.Cvars.Gamma = Host.CVars.Add("gamma", 1f, ClientVariableFlags.Archive);
            }
        }

        /// <summary>
        /// V_RenderView
        /// The player's clipping box goes from (-16 -16 -24) to (16 16 32) from
        /// the entity origin, so any view position inside that will be valid
        /// </summary>
        public void RenderView()
        {
            if (Host.Console.ForcedUp)
            {
                return;
            }

            // don't allow cheats in multiplayer
            if (Host.Client.Cl.maxclients > 1)
            {
                Host.CVars.Set("scr_ofsx", 0f);
                Host.CVars.Set("scr_ofsy", 0f);
                Host.CVars.Set("scr_ofsz", 0f);
            }

            if (Host.Client.Cl.intermission > 0)
            {
                // intermission / finale rendering
                CalcIntermissionRefDef();
            }
            else if (!Host.Client.Cl.paused)
            {
                CalcRefDef();
            }

            Host.RenderContext.PushDlights();

            if (Host.Cvars.LcdX.Get<float>() != 0)
            {
                //
                // render two interleaved views
                //
                var vid = Host.Screen.VidDef;
                var rdef = Host.RenderContext.RefDef;

                vid.rowbytes <<= 1;
                vid.aspect *= 0.5f;

                rdef.viewangles.Y -= Host.Cvars.LcdYaw.Get<float>();
                rdef.vieworg -= _right * Host.Cvars.LcdX.Get<float>();

                Host.RenderContext.RenderView();

                // ???????? vid.buffer += vid.rowbytes>>1;

                Host.RenderContext.PushDlights();

                rdef.viewangles.Y += Host.Cvars.LcdYaw.Get<float>() * 2;
                rdef.vieworg += _right * Host.Cvars.LcdX.Get<float>() * 2;

                Host.RenderContext.RenderView();

                // ????????? vid.buffer -= vid.rowbytes>>1;

                rdef.vrect.height <<= 1;

                vid.rowbytes >>= 1;
                vid.aspect *= 2;
            }
            else
            {
                Host.RenderContext.RenderView();
            }
        }

        /// <summary>
        /// V_CalcRoll
        /// Used by view and sv_user
        /// </summary>
        public float CalcRoll(ref Vector3 angles, ref Vector3 velocity)
        {
            MathLib.AngleVectors(ref angles, out _, out _right, out _);
            var side = Vector3.Dot(velocity, _right);
            float sign = side < 0 ? -1 : 1;
            side = Math.Abs(side);

            var value = Host.Cvars.ClRollAngle.Get<float>();
            side = side < Host.Cvars.ClRollSpeed.Get<float>() ? side * value / Host.Cvars.ClRollSpeed.Get<float>() : value;

            return side * sign;
        }

        // V_UpdatePalette
        public void UpdatePalette()
        {
            CalcPowerupCshift();

            var isnew = false;

            var cl = Host.Client.Cl;
            for (var i = 0; i < ColorShift.NUM_CSHIFTS; i++)
            {
                if (cl.cshifts[i].percent != cl.prev_cshifts[i].percent)
                {
                    isnew = true;
                    cl.prev_cshifts[i].percent = cl.cshifts[i].percent;
                }
                for (var j = 0; j < 3; j++)
                {
                    if (cl.cshifts[i].destcolor[j] != cl.prev_cshifts[i].destcolor[j])
                    {
                        isnew = true;
                        cl.prev_cshifts[i].destcolor[j] = cl.cshifts[i].destcolor[j];
                    }
                }
            }

            // drop the damage value
            cl.cshifts[ColorShift.CSHIFT_DAMAGE].percent -= (int)(Host.FrameTime * 150);
            if (cl.cshifts[ColorShift.CSHIFT_DAMAGE].percent < 0)
            {
                cl.cshifts[ColorShift.CSHIFT_DAMAGE].percent = 0;
            }

            // drop the bonus value
            cl.cshifts[ColorShift.CSHIFT_BONUS].percent -= (int)(Host.FrameTime * 100);
            if (cl.cshifts[ColorShift.CSHIFT_BONUS].percent < 0)
            {
                cl.cshifts[ColorShift.CSHIFT_BONUS].percent = 0;
            }

            var force = CheckGamma();
            if (!isnew && !force)
            {
                return;
            }

            CalcBlend();

            var a = Blend.A;
            var r = 255 * Blend.R * a;
            var g = 255 * Blend.G * a;
            var b = 255 * Blend.B * a;

            a = (byte)(1 - a);
            for (var i = 0; i < 256; i++)
            {
                var ir = (int)((i * a) + r);
                var ig = (int)((i * a) + g);
                var ib = (int)((i * a) + b);
                if (ir > 255)
                {
                    ir = 255;
                }

                if (ig > 255)
                {
                    ig = 255;
                }

                if (ib > 255)
                {
                    ib = 255;
                }

                _Ramps[0, i] = _GammaTable[ir];
                _Ramps[1, i] = _GammaTable[ig];
                _Ramps[2, i] = _GammaTable[ib];
            }

            var basepal = Host.BasePal;
            var offset = 0;
            var newpal = new byte[768];

            for (var i = 0; i < 256; i++)
            {
                int ir = basepal[offset + 0];
                int ig = basepal[offset + 1];
                int ib = basepal[offset + 2];

                newpal[offset + 0] = _Ramps[0, ir];
                newpal[offset + 1] = _Ramps[1, ig];
                newpal[offset + 2] = _Ramps[2, ib];

                offset += 3;
            }

            ShiftPalette();
        }

        // V_StartPitchDrift
        public void StartPitchDrift(CommandMessage msg)
        {
            var cl = Host.Client.Cl;
            if (cl.laststop == cl.time)
            {
                return; // something else is keeping it from drifting
            }
            if (cl.nodrift || cl.pitchvel == 0)
            {
                cl.pitchvel = Host.Cvars.CenterSpeed.Get<float>();
                cl.nodrift = false;
                cl.driftmove = 0;
            }
        }

        // V_StopPitchDrift
        public void StopPitchDrift()
        {
            var cl = Host.Client.Cl;
            cl.laststop = cl.time;
            cl.nodrift = true;
            cl.pitchvel = 0;
        }

        /// <summary>
        /// V_CalcBlend
        /// </summary>
        public void CalcBlend()
        {
            float r = 0;
            float g = 0;
            float b = 0;
            float a = 0;

            var cshifts = Host.Client.Cl.cshifts;

            if (Host.Cvars.glCShiftPercent.Get<float>() != 0)
            {
                for (var j = 0; j < ColorShift.NUM_CSHIFTS; j++)
                {
                    var a2 = cshifts[j].percent * Host.Cvars.glCShiftPercent.Get<float>() / 100.0f / 255.0f;

                    if (a2 == 0)
                    {
                        continue;
                    }

                    a += a2 * (1 - a);

                    a2 /= a;
                    r = (r * (1 - a2)) + (cshifts[j].destcolor[0] * a2);
                    g = (g * (1 - a2)) + (cshifts[j].destcolor[1] * a2);
                    b = (b * (1 - a2)) + (cshifts[j].destcolor[2] * a2);
                }
            }

            float fR = r / 255.0f;
            float fG = g / 255.0f;
            float fB = b / 255.0f;
            float fA = Math.Clamp(a, 0.0f, 1.0f);

            Blend = Color.FromArgb((int)fA, (int)fR, (int)fG, (int)fB);
        }

        // V_ParseDamage
        public void ParseDamage()
        {
            var armor = Host.Network.Reader.ReadByte();
            var blood = Host.Network.Reader.ReadByte();
            var from = Host.Network.Reader.ReadCoords();

            var count = (blood * 0.5f) + (armor * 0.5f);
            if (count < 10)
            {
                count = 10;
            }

            var cl = Host.Client.Cl;
            cl.faceanimtime = (float)cl.time + 0.2f; // put sbar face into pain frame

            cl.cshifts[ColorShift.CSHIFT_DAMAGE].percent += (int)(3 * count);
            if (cl.cshifts[ColorShift.CSHIFT_DAMAGE].percent < 0)
            {
                cl.cshifts[ColorShift.CSHIFT_DAMAGE].percent = 0;
            }

            if (cl.cshifts[ColorShift.CSHIFT_DAMAGE].percent > 150)
            {
                cl.cshifts[ColorShift.CSHIFT_DAMAGE].percent = 150;
            }

            if (armor > blood)
            {
                cl.cshifts[ColorShift.CSHIFT_DAMAGE].destcolor[0] = 200;
                cl.cshifts[ColorShift.CSHIFT_DAMAGE].destcolor[1] = 100;
                cl.cshifts[ColorShift.CSHIFT_DAMAGE].destcolor[2] = 100;
            }
            else if (armor != 0)
            {
                cl.cshifts[ColorShift.CSHIFT_DAMAGE].destcolor[0] = 220;
                cl.cshifts[ColorShift.CSHIFT_DAMAGE].destcolor[1] = 50;
                cl.cshifts[ColorShift.CSHIFT_DAMAGE].destcolor[2] = 50;
            }
            else
            {
                cl.cshifts[ColorShift.CSHIFT_DAMAGE].destcolor[0] = 255;
                cl.cshifts[ColorShift.CSHIFT_DAMAGE].destcolor[1] = 0;
                cl.cshifts[ColorShift.CSHIFT_DAMAGE].destcolor[2] = 0;
            }

            //
            // calculate view angle kicks
            //
            var ent = Host.Client.Entities[cl.viewentity];

            from -= ent.origin; //  VectorSubtract (from, ent->origin, from);
            MathLib.Normalize(ref from);

            MathLib.AngleVectors(ref ent.angles, out Vector3 forward, out Vector3 right, out _);

            var side = Vector3.Dot(from, right);

            _DmgRoll = count * side * Host.Cvars.KickRoll.Get<float>();

            side = Vector3.Dot(from, forward);
            _DmgPitch = count * side * Host.Cvars.KickPitch.Get<float>();

            _DmgTime = Host.Cvars.KickTime.Get<float>();
        }

        /// <summary>
        /// V_SetContentsColor
        /// Underwater, lava, etc each has a color shift
        /// </summary>
        public void SetContentsColor(int contents)
        {
            Host.Client.Cl.cshifts[ColorShift.CSHIFT_CONTENTS] = (Q1Contents)contents switch
            {
                Q1Contents.Empty or Q1Contents.Solid => _CShift_empty,
                Q1Contents.Lava => _CShift_lava,
                Q1Contents.Slime => _CShift_slime,
                _ => _CShift_water,
            };
        }

        // BuildGammaTable
        private void BuildGammaTable(float g)
        {
            if (g == 1.0f)
            {
                for (var i = 0; i < 256; i++)
                {
                    _GammaTable[i] = (byte)i;
                }
            }
            else
            {
                for (var i = 0; i < 256; i++)
                {
                    var inf = (int)((255 * Math.Pow((i + 0.5) / 255.5, g)) + 0.5);
                    if (inf < 0)
                    {
                        inf = 0;
                    }

                    if (inf > 255)
                    {
                        inf = 255;
                    }

                    _GammaTable[i] = (byte)inf;
                }
            }
        }

        // V_cshift_f
        private void CShift_f(CommandMessage msg)
        {
            int.TryParse(msg.Parameters[0], out _CShift_empty.destcolor[0]);
            int.TryParse(msg.Parameters[1], out _CShift_empty.destcolor[1]);
            int.TryParse(msg.Parameters[2], out _CShift_empty.destcolor[2]);
            int.TryParse(msg.Parameters[3], out _CShift_empty.percent);
        }

        // V_BonusFlash_f
        //
        // When you run over an item, the server sends this command
        private void BonusFlash_f(CommandMessage msg)
        {
            var cl = Host.Client.Cl;
            cl.cshifts[ColorShift.CSHIFT_BONUS].destcolor[0] = 215;
            cl.cshifts[ColorShift.CSHIFT_BONUS].destcolor[1] = 186;
            cl.cshifts[ColorShift.CSHIFT_BONUS].destcolor[2] = 69;
            cl.cshifts[ColorShift.CSHIFT_BONUS].percent = 50;
        }

        // V_CalcIntermissionRefdef
        private void CalcIntermissionRefDef()
        {
            // ent is the player model (visible when out of body)
            var ent = Host.Client.ViewEntity;

            // view is the weapon model (only visible from inside body)
            var view = Host.Client.ViewEnt;

            var rdef = Host.RenderContext.RefDef;
            rdef.vieworg = ent.origin;
            rdef.viewangles = ent.angles;
            view.model = null;

            // allways idle in intermission
            AddIdle(1);
        }

        // V_CalcRefdef
        private void CalcRefDef()
        {
            DriftPitch();

            // ent is the player model (visible when out of body)
            var ent = Host.Client.ViewEntity;
            // view is the weapon model (only visible from inside body)
            var view = Host.Client.ViewEnt;

            // transform the view offset by the model's matrix to get the offset from
            // model origin for the view
            ent.angles.Y = Host.Client.Cl.viewangles.Y; // the model should face the view dir
            ent.angles.X = -Host.Client.Cl.viewangles.X;    // the model should face the view dir

            var bob = CalcBob();

            var rdef = Host.RenderContext.RefDef;
            var cl = Host.Client.Cl;

            // refresh position
            rdef.vieworg = ent.origin;
            rdef.vieworg.Z += cl.viewheight + bob;

            // never let it sit exactly on a node line, because a water plane can
            // dissapear when viewed with the eye exactly on it.
            // the server protocol only specifies to 1/16 pixel, so add 1/32 in each axis
            rdef.vieworg += SmallOffset;
            rdef.viewangles = cl.viewangles;

            CalcViewRoll();
            AddIdle(Host.Cvars.IdleScale.Get<float>());

            // offsets
            var angles = ent.angles;
            angles.X = -angles.X; // because entity pitches are actually backward

            MathLib.AngleVectors(ref angles, out Vector3 forward, out Vector3 right, out Vector3 up);

            rdef.vieworg += (forward * Host.Cvars.ScrOfsX.Get<float>()) + (right * Host.Cvars.ScrOfsY.Get<float>()) + (up * Host.Cvars.ScrOfsZ.Get<float>());

            BoundOffsets();

            // set up gun position
            view.angles = cl.viewangles;

            CalcGunAngle();

            view.origin = ent.origin;
            view.origin.Z += cl.viewheight;
            view.origin += forward * bob * 0.4f;
            view.origin.Z += bob;

            // fudge position around to keep amount of weapon visible
            // roughly equal with different FOV
            var viewSize = Host.Screen.ViewSize.Get<float>(); // scr_viewsize

            if (viewSize == 110)
            {
                view.origin.Z += 1;
            }
            else if (viewSize == 100)
            {
                view.origin.Z += 2;
            }
            else if (viewSize == 90)
            {
                view.origin.Z += 1;
            }
            else if (viewSize == 80)
            {
                view.origin.Z += 0.5f;
            }

            view.model = cl.model_precache[cl.stats[QStatsDef.STAT_WEAPON]];
            view.frame = cl.stats[QStatsDef.STAT_WEAPONFRAME];
            view.colormap = Host.Screen.VidDef.colormap;

            // set up the refresh position
            rdef.viewangles += cl.punchangle;

            // smooth out stair step ups
            if (cl.onground && ent.origin.Z - _OldZ > 0)
            {
                var steptime = (float)(cl.time - cl.oldtime);
                if (steptime < 0)
                {
                    steptime = 0;
                }

                _OldZ += steptime * 80;
                if (_OldZ > ent.origin.Z)
                {
                    _OldZ = ent.origin.Z;
                }

                if (ent.origin.Z - _OldZ > 12)
                {
                    _OldZ = ent.origin.Z - 12;
                }

                rdef.vieworg.Z += _OldZ - ent.origin.Z;
                view.origin.Z += _OldZ - ent.origin.Z;
            }
            else
            {
                _OldZ = ent.origin.Z;
            }

            if (Host.ChaseView.IsActive)
            {
                Host.ChaseView.Update();
            }
        }

        // V_AddIdle
        //
        // Idle swaying
        private void AddIdle(float idleScale)
        {
            var time = Host.Client.Cl.time;
            var v = new Vector3(
                (float)(Math.Sin(time * Host.Cvars.IPitchCycle.Get<float>()) * Host.Cvars.IPitchLevel.Get<float>()),
                (float)(Math.Sin(time * Host.Cvars.IYawCycle.Get<float>()) * Host.Cvars.IYawLevel.Get<float>()),
                (float)(Math.Sin(time * Host.Cvars.IRollCycle.Get<float>()) * Host.Cvars.IRollLevel.Get<float>()));
            Host.RenderContext.RefDef.viewangles += v * idleScale;
        }

        // V_DriftPitch
        //
        // Moves the client pitch angle towards cl.idealpitch sent by the server.
        //
        // If the user is adjusting pitch manually, either with lookup/lookdown,
        // mlook and mouse, or klook and keyboard, pitch drifting is constantly stopped.
        //
        // Drifting is enabled when the center view key is hit, mlook is released and
        // lookspring is non 0, or when
        private void DriftPitch()
        {
            var cl = Host.Client.Cl;
            if (Host.NoClipAngleHack || !cl.onground || Host.Client.Cls.demoplayback)
            {
                cl.driftmove = 0;
                cl.pitchvel = 0;
                return;
            }

            // don't count small mouse motion
            if (cl.nodrift)
            {
                if (Math.Abs(cl.cmd.forwardmove) < Host.Client.ForwardSpeed)
                {
                    cl.driftmove = 0;
                }
                else
                {
                    cl.driftmove += (float)Host.FrameTime;
                }

                if (cl.driftmove > Host.Cvars.CenterMove.Get<float>())
                {
                    StartPitchDrift(null);
                }
                return;
            }

            var delta = cl.idealpitch - cl.viewangles.X;
            if (delta == 0)
            {
                cl.pitchvel = 0;
                return;
            }

            var move = (float)Host.FrameTime * cl.pitchvel;
            cl.pitchvel += (float)Host.FrameTime * Host.Cvars.CenterSpeed.Get<float>();

            if (delta > 0)
            {
                if (move > delta)
                {
                    cl.pitchvel = 0;
                    move = delta;
                }
                cl.viewangles.X += move;
            }
            else if (delta < 0)
            {
                if (move > -delta)
                {
                    cl.pitchvel = 0;
                    move = -delta;
                }
                cl.viewangles.X -= move;
            }
        }

        // V_CalcBob
        private float CalcBob()
        {
            var cl = Host.Client.Cl;
            var bobCycle = Host.Cvars.ClBobCycle.Get<float>();
            var bobUp = Host.Cvars.ClBobUp.Get<float>();
            var cycle = (float)(cl.time - ((int)(cl.time / bobCycle) * bobCycle));
            cycle /= bobCycle;
            cycle = cycle < bobUp ? (float)Math.PI * cycle / bobUp : (float)(Math.PI + (Math.PI * (cycle - bobUp) / (1.0 - bobUp)));

            // bob is proportional to velocity in the xy plane
            // (don't count Z, or jumping messes it up)
            var tmp = new Vector2(cl.velocity.X, cl.velocity.Y);
            double bob = tmp.Length() * Host.Cvars.ClBob.Get<float>();
            bob = (bob * 0.3) + (bob * 0.7 * Math.Sin(cycle));
            if (bob > 4)
            {
                bob = 4;
            }
            else if (bob < -7)
            {
                bob = -7;
            }

            return (float)bob;
        }

        // V_CalcViewRoll
        //
        // Roll is induced by movement and damage
        private void CalcViewRoll()
        {
            var cl = Host.Client.Cl;
            var rdef = Host.RenderContext.RefDef;
            var side = CalcRoll(ref Host.Client.ViewEntity.angles, ref cl.velocity);
            rdef.viewangles.Z += side;

            if (_DmgTime > 0)
            {
                rdef.viewangles.Z += _DmgTime / Host.Cvars.KickTime.Get<float>() * _DmgRoll;
                rdef.viewangles.X += _DmgTime / Host.Cvars.KickTime.Get<float>() * _DmgPitch;
                _DmgTime -= (float)Host.FrameTime;
            }

            if (cl.stats[QStatsDef.STAT_HEALTH] <= 0)
            {
                rdef.viewangles.Z = 80; // dead view angle
                return;
            }
        }

        // V_BoundOffsets
        private void BoundOffsets()
        {
            var ent = Host.Client.ViewEntity;

            // absolutely bound refresh reletive to entity clipping hull
            // so the view can never be inside a solid wall
            var rdef = Host.RenderContext.RefDef;
            if (rdef.vieworg.X < ent.origin.X - 14)
            {
                rdef.vieworg.X = ent.origin.X - 14;
            }
            else if (rdef.vieworg.X > ent.origin.X + 14)
            {
                rdef.vieworg.X = ent.origin.X + 14;
            }

            if (rdef.vieworg.Y < ent.origin.Y - 14)
            {
                rdef.vieworg.Y = ent.origin.Y - 14;
            }
            else if (rdef.vieworg.Y > ent.origin.Y + 14)
            {
                rdef.vieworg.Y = ent.origin.Y + 14;
            }

            if (rdef.vieworg.Z < ent.origin.Z - 22)
            {
                rdef.vieworg.Z = ent.origin.Z - 22;
            }
            else if (rdef.vieworg.Z > ent.origin.Z + 30)
            {
                rdef.vieworg.Z = ent.origin.Z + 30;
            }
        }

        /// <summary>
        /// CalcGunAngle
        /// </summary>
        private void CalcGunAngle()
        {
            var rdef = Host.RenderContext.RefDef;
            var yaw = rdef.viewangles.Y;
            var pitch = -rdef.viewangles.X;

            yaw = AngleDelta(yaw - rdef.viewangles.Y) * 0.4f;
            if (yaw > 10)
            {
                yaw = 10;
            }

            if (yaw < -10)
            {
                yaw = -10;
            }

            pitch = AngleDelta(-pitch - rdef.viewangles.X) * 0.4f;
            if (pitch > 10)
            {
                pitch = 10;
            }

            if (pitch < -10)
            {
                pitch = -10;
            }

            var move = (float)Host.FrameTime * 20;
            if (yaw > _OldYaw)
            {
                if (_OldYaw + move < yaw)
                {
                    yaw = _OldYaw + move;
                }
            }
            else
            {
                if (_OldYaw - move > yaw)
                {
                    yaw = _OldYaw - move;
                }
            }

            if (pitch > _OldPitch)
            {
                if (_OldPitch + move < pitch)
                {
                    pitch = _OldPitch + move;
                }
            }
            else
            {
                if (_OldPitch - move > pitch)
                {
                    pitch = _OldPitch - move;
                }
            }

            _OldYaw = yaw;
            _OldPitch = pitch;

            var cl = Host.Client.Cl;
            cl.viewent.angles.Y = rdef.viewangles.Y + yaw;
            cl.viewent.angles.X = -(rdef.viewangles.X + pitch);

            var idleScale = Host.Cvars.IdleScale.Get<float>();
            cl.viewent.angles.Z -= (float)(idleScale * Math.Sin(cl.time * Host.Cvars.IRollCycle.Get<float>()) * Host.Cvars.IRollLevel.Get<float>());
            cl.viewent.angles.X -= (float)(idleScale * Math.Sin(cl.time * Host.Cvars.IPitchCycle.Get<float>()) * Host.Cvars.IPitchLevel.Get<float>());
            cl.viewent.angles.Y -= (float)(idleScale * Math.Sin(cl.time * Host.Cvars.IYawCycle.Get<float>()) * Host.Cvars.IYawLevel.Get<float>());
        }

        // angledelta()
        private static float AngleDelta(float a)
        {
            a = MathLib.AngleMod(a);
            if (a > 180)
            {
                a -= 360;
            }

            return a;
        }

        // V_CalcPowerupCshift
        private void CalcPowerupCshift()
        {
            var cl = Host.Client.Cl;
            if (cl.HasItems(QItemsDef.IT_QUAD))
            {
                cl.cshifts[ColorShift.CSHIFT_POWERUP].destcolor[0] = 0;
                cl.cshifts[ColorShift.CSHIFT_POWERUP].destcolor[1] = 0;
                cl.cshifts[ColorShift.CSHIFT_POWERUP].destcolor[2] = 255;
                cl.cshifts[ColorShift.CSHIFT_POWERUP].percent = 30;
            }
            else if (cl.HasItems(QItemsDef.IT_SUIT))
            {
                cl.cshifts[ColorShift.CSHIFT_POWERUP].destcolor[0] = 0;
                cl.cshifts[ColorShift.CSHIFT_POWERUP].destcolor[1] = 255;
                cl.cshifts[ColorShift.CSHIFT_POWERUP].destcolor[2] = 0;
                cl.cshifts[ColorShift.CSHIFT_POWERUP].percent = 20;
            }
            else if (cl.HasItems(QItemsDef.IT_INVISIBILITY))
            {
                cl.cshifts[ColorShift.CSHIFT_POWERUP].destcolor[0] = 100;
                cl.cshifts[ColorShift.CSHIFT_POWERUP].destcolor[1] = 100;
                cl.cshifts[ColorShift.CSHIFT_POWERUP].destcolor[2] = 100;
                cl.cshifts[ColorShift.CSHIFT_POWERUP].percent = 100;
            }
            else if (cl.HasItems(QItemsDef.IT_INVULNERABILITY))
            {
                cl.cshifts[ColorShift.CSHIFT_POWERUP].destcolor[0] = 255;
                cl.cshifts[ColorShift.CSHIFT_POWERUP].destcolor[1] = 255;
                cl.cshifts[ColorShift.CSHIFT_POWERUP].destcolor[2] = 0;
                cl.cshifts[ColorShift.CSHIFT_POWERUP].percent = 30;
            }
            else
            {
                cl.cshifts[ColorShift.CSHIFT_POWERUP].percent = 0;
            }
        }

        // V_CheckGamma
        private bool CheckGamma()
        {
            if (Host.Cvars.Gamma.Get<float>() == _OldGammaValue)
            {
                return false;
            }

            _OldGammaValue = Host.Cvars.Gamma.Get<float>();

            BuildGammaTable(Host.Cvars.Gamma.Get<float>());
            Host.Screen.VidDef.recalc_refdef = true;   // force a surface cache flush

            return true;
        }

        // VID_ShiftPalette from gl_vidnt.c
        private static void ShiftPalette()
        {
            //	VID_SetPalette (palette);
            //	gammaworks = SetDeviceGammaRamp (maindc, ramps);
        }

        public View(Host host)
        {
            Host = host;

            _GammaTable = new byte[256];

            _CShift_empty = new ClientShift(new[] { 130, 80, 50 }, 0);
            _CShift_water = new ClientShift(new[] { 130, 80, 50 }, 128);
            _CShift_slime = new ClientShift(new[] { 0, 25, 5 }, 150);
            _CShift_lava = new ClientShift(new[] { 255, 80, 0 }, 150);
        }
    }
}
