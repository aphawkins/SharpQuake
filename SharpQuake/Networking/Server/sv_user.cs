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
    using System.Linq;
    using System.Numerics;
    using SharpQuake.Framework;
    using SharpQuake.Framework.IO;
    using SharpQuake.Framework.IO.Input;

    public partial class Server
    {
        public MemoryEdict Player { get; private set; }

        private const int MAX_FORWARD = 6;
        private bool _OnGround; // onground

        // world
        //static v3f angles - this must be a reference to _Player.v.angles
        //static v3f origin  - this must be a reference to _Player.v.origin
        //static Vector3 velocity - this must be a reference to _Player.v.velocity

        private UserCommand _Cmd; // cmd

        private Vector3 _Forward; // forward
        private Vector3 _Right; // right

        private Vector3 _WishDir; // wishdir
        private float _WishSpeed; // wishspeed

        private readonly string[] ClientMessageCommands = new string[]
        {
            "status",
            "god",
            "notarget",
            "fly",
            "name",
            "noclip",
            "say",
            "say_team",
            "tell",
            "color",
            "kill",
            "pause",
            "spawn",
            "begin",
            "prespawn",
            "kick",
            "ping",
            "give",
            "ban"
        };

        /// <summary>
        /// SV_RunClients
        /// </summary>
        public void RunClients()
        {
            for (var i = 0; i < ServerStatic.maxclients; i++)
            {
                Host.HostClient = ServerStatic.clients[i];
                if (!Host.HostClient.active)
                {
                    continue;
                }

                Player = Host.HostClient.edict;

                if (!ReadClientMessage())
                {
                    DropClient(false);	// client misbehaved...
                    continue;
                }

                if (!Host.HostClient.spawned)
                {
                    // clear client movement until a new packet is received
                    Host.HostClient.cmd.Clear();
                    continue;
                }

                // always pause in single player if in console or menus
                if (!NetServer.paused && (ServerStatic.maxclients > 1 || Host.Keyboard.Destination == KeyDestination.key_game))
                {
                    ClientThink();
                }
            }
        }

        /// <summary>
        /// SV_SetIdealPitch
        /// </summary>
        public void SetIdealPitch()
        {
            if (((int)Player.v.flags & EdictFlags.FL_ONGROUND) == 0)
            {
                return;
            }

            var angleval = Player.v.angles.y * Math.PI * 2 / 360;
            var sinval = Math.Sin(angleval);
            var cosval = Math.Cos(angleval);
            var z = new float[MAX_FORWARD];
            for (var i = 0; i < MAX_FORWARD; i++)
            {
                var top = Player.v.origin;
                top.x += (float)(cosval * (i + 3) * 12);
                top.y += (float)(sinval * (i + 3) * 12);
                top.z += Player.v.view_ofs.z;

                var bottom = top;
                bottom.z -= 160;

                var tr = Move(ref top, ref Utilities.ZeroVector3f, ref Utilities.ZeroVector3f, ref bottom, 1, Player);
                if (tr.allsolid)
                {
                    return; // looking at a wall, leave ideal the way is was
                }

                if (tr.fraction == 1)
                {
                    return; // near a dropoff
                }

                z[i] = top.z + (tr.fraction * (bottom.z - top.z));
            }

            float dir = 0; // Uze: int in original code???
            var steps = 0;
            for (var j = 1; j < MAX_FORWARD; j++)
            {
                var step = z[j] - z[j - 1]; // Uze: int in original code???
                if (step is > (-QDef.ON_EPSILON) and < QDef.ON_EPSILON) // Uze: comparing int with ON_EPSILON (0.1)???
                {
                    continue;
                }

                if (dir != 0 && (step - dir > QDef.ON_EPSILON || step - dir < -QDef.ON_EPSILON))
                {
                    return;     // mixed changes
                }

                steps++;
                dir = step;
            }

            if (dir == 0)
            {
                Player.v.idealpitch = 0;
                return;
            }

            if (steps < 2)
            {
                return;
            }

            Player.v.idealpitch = -dir * Host.Cvars.IdealPitchScale.Get<float>();
        }

        private int GetClientMessageCommand(string s)
        {
            int ret = Host.HostClient.privileged ? 2 : 0;
            var cmdName = s.Split(' ')[0];

            if (ClientMessageCommands.Contains(cmdName))
            {
                ret = 1;
            }

            return ret;
        }

        /// <summary>
        /// SV_ReadClientMessage
        /// Returns false if the client should be killed
        /// </summary>
        private bool ReadClientMessage()
        {
            while (true)
            {
                var ret = Host.Network.GetMessage(Host.HostClient.netconnection);
                if (ret == -1)
                {
                    Host.Console.DPrint("SV_ReadClientMessage: NET_GetMessage failed\n");
                    return false;
                }
                if (ret == 0)
                {
                    return true;
                }

                Host.Network.Reader.Reset();

                var flag = true;
                while (flag)
                {
                    if (!Host.HostClient.active)
                    {
                        return false;  // a command caused an error
                    }

                    if (Host.Network.Reader.IsBadRead)
                    {
                        Host.Console.DPrint("SV_ReadClientMessage: badread\n");
                        return false;
                    }

                    var cmd = Host.Network.Reader.ReadChar();
                    switch (cmd)
                    {
                        case -1:
                            flag = false; // end of message
                            ret = 1;
                            break;

                        case ProtocolDef.clc_nop:
                            break;

                        case ProtocolDef.clc_stringcmd:
                            var s = Host.Network.Reader.ReadString();
                            ret = GetClientMessageCommand(s);
                            if (ret == 2)
                            {
                                Host.Commands.Buffer.Insert(s);
                            }
                            else if (ret == 1)
                            {
                                Host.Commands.ExecuteString(s, CommandSource.Client);
                            }
                            else
                            {
                                Host.Console.DPrint("{0} tried to {1}\n", Host.HostClient.name, s);
                            }

                            break;

                        case ProtocolDef.clc_disconnect:
                            return false;

                        case ProtocolDef.clc_move:
                            ReadClientMove(ref Host.HostClient.cmd);
                            break;

                        default:
                            Host.Console.DPrint("SV_ReadClientMessage: unknown command char\n");
                            return false;
                    }
                }

                if (ret != 1)
                {
                    break;
                }
            }

            return true;
        }

        /// <summary>
        /// SV_ReadClientMove
        /// </summary>
        private void ReadClientMove(ref UserCommand move)
        {
            var client = Host.HostClient;

            // read ping time
            client.ping_times[client.num_pings % ServerDef.NUM_PING_TIMES] = (float)(NetServer.time - Host.Network.Reader.ReadFloat());
            client.num_pings++;

            // read current angles
            var angles = Host.Network.Reader.ReadAngles();
            MathLib.Copy(ref angles, out client.edict.v.v_angle);

            // read movement
            move.forwardmove = Host.Network.Reader.ReadShort();
            move.sidemove = Host.Network.Reader.ReadShort();
            move.upmove = Host.Network.Reader.ReadShort();

            // read buttons
            var bits = Host.Network.Reader.ReadByte();
            client.edict.v.button0 = bits & 1;
            client.edict.v.button2 = (bits & 2) >> 1;

            var i = Host.Network.Reader.ReadByte();
            if (i != 0)
            {
                client.edict.v.impulse = i;
            }
        }

        /// <summary>
        /// SV_ClientThink
        /// the move fields specify an intended velocity in pix/sec
        /// the angle fields specify an exact angular motion in degrees
        /// </summary>
        private void ClientThink()
        {
            if (Player.v.movetype == Movetypes.MOVETYPE_NONE)
            {
                return;
            }

            _OnGround = ((int)Player.v.flags & EdictFlags.FL_ONGROUND) != 0;

            DropPunchAngle();

            //
            // if dead, behave differently
            //
            if (Player.v.health <= 0)
            {
                return;
            }

            //
            // angles
            // show 1/3 the pitch angle and all the roll angle
            _Cmd = Host.HostClient.cmd;

            MathLib.VectorAdd(ref Player.v.v_angle, ref Player.v.punchangle, out Vector3f v_angle);
            var pang = Utilities.ToVector(ref Player.v.angles);
            var pvel = Utilities.ToVector(ref Player.v.velocity);
            Player.v.angles.z = Host.View.CalcRoll(ref pang, ref pvel) * 4;
            if (Player.v.fixangle == 0)
            {
                Player.v.angles.x = -v_angle.x / 3;
                Player.v.angles.y = v_angle.y;
            }

            if (((int)Player.v.flags & EdictFlags.FL_WATERJUMP) != 0)
            {
                WaterJump();
                return;
            }
            //
            // walk
            //
            if ((Player.v.waterlevel >= 2) && (Player.v.movetype != Movetypes.MOVETYPE_NOCLIP))
            {
                WaterMove();
                return;
            }

            AirMove();
        }

        private void DropPunchAngle()
        {
            var v = Utilities.ToVector(ref Player.v.punchangle);
            var len = MathLib.Normalize(ref v) - (10 * Host.FrameTime);
            if (len < 0)
            {
                len = 0;
            }

            v *= (float)len;
            MathLib.Copy(ref v, out Player.v.punchangle);
        }

        /// <summary>
        /// SV_WaterJump
        /// </summary>
        private void WaterJump()
        {
            if (NetServer.time > Player.v.teleport_time || Player.v.waterlevel == 0)
            {
                Player.v.flags = (int)Player.v.flags & ~EdictFlags.FL_WATERJUMP;
                Player.v.teleport_time = 0;
            }
            Player.v.velocity.x = Player.v.movedir.x;
            Player.v.velocity.y = Player.v.movedir.y;
        }

        /// <summary>
        /// SV_WaterMove
        /// </summary>
        private void WaterMove()
        {
            //
            // user intentions
            //
            var pangle = Utilities.ToVector(ref Player.v.v_angle);
            MathLib.AngleVectors(ref pangle, out _Forward, out _Right, out _);
            var wishvel = (_Forward * _Cmd.forwardmove) + (_Right * _Cmd.sidemove);

            if (_Cmd.forwardmove == 0 && _Cmd.sidemove == 0 && _Cmd.upmove == 0)
            {
                wishvel.Z -= 60;       // drift towards bottom
            }
            else
            {
                wishvel.Z += _Cmd.upmove;
            }

            var wishspeed = wishvel.Length();
            var maxSpeed = Host.Cvars.MaxSpeed.Get<float>();
            if (wishspeed > maxSpeed)
            {
                wishvel *= maxSpeed / wishspeed;
                wishspeed = maxSpeed;
            }
            wishspeed *= 0.7f;

            //
            // water friction
            //
            float newspeed, speed = MathLib.Length(ref Player.v.velocity);
            if (speed != 0)
            {
                newspeed = (float)(speed - (Host.FrameTime * speed * Host.Cvars.Friction.Get<float>()));
                if (newspeed < 0)
                {
                    newspeed = 0;
                }

                MathLib.VectorScale(ref Player.v.velocity, newspeed / speed, out Player.v.velocity);
            }
            else
            {
                newspeed = 0;
            }

            //
            // water acceleration
            //
            if (wishspeed == 0)
            {
                return;
            }

            var addspeed = wishspeed - newspeed;
            if (addspeed <= 0)
            {
                return;
            }

            MathLib.Normalize(ref wishvel);
            var accelspeed = (float)(Host.Cvars.Accelerate.Get<float>() * wishspeed * Host.FrameTime);
            if (accelspeed > addspeed)
            {
                accelspeed = addspeed;
            }

            wishvel *= accelspeed;
            Player.v.velocity.x += wishvel.X;
            Player.v.velocity.y += wishvel.Y;
            Player.v.velocity.z += wishvel.Z;
        }

        /// <summary>
        /// SV_AirMove
        /// </summary>
        private void AirMove()
        {
            var pangles = Utilities.ToVector(ref Player.v.angles);
            MathLib.AngleVectors(ref pangles, out _Forward, out _Right, out _);

            var fmove = _Cmd.forwardmove;
            var smove = _Cmd.sidemove;

            // hack to not let you back into teleporter
            if (NetServer.time < Player.v.teleport_time && fmove < 0)
            {
                fmove = 0;
            }

            var wishvel = (_Forward * fmove) + (_Right * smove);

            wishvel.Z = (int)Player.v.movetype != Movetypes.MOVETYPE_WALK ? _Cmd.upmove : 0;

            _WishDir = wishvel;
            _WishSpeed = MathLib.Normalize(ref _WishDir);
            var maxSpeed = Host.Cvars.MaxSpeed.Get<float>();
            if (_WishSpeed > maxSpeed)
            {
                wishvel *= maxSpeed / _WishSpeed;
                _WishSpeed = maxSpeed;
            }

            if (Player.v.movetype == Movetypes.MOVETYPE_NOCLIP)
            {
                // noclip
                MathLib.Copy(ref wishvel, out Player.v.velocity);
            }
            else if (_OnGround)
            {
                UserFriction();
                Accelerate();
            }
            else
            {	// not on ground, so little effect on velocity
                AirAccelerate(wishvel);
            }
        }

        /// <summary>
        /// SV_UserFriction
        /// </summary>
        private void UserFriction()
        {
            var speed = MathLib.LengthXY(ref Player.v.velocity);
            if (speed == 0)
            {
                return;
            }

            // if the leading edge is over a dropoff, increase friction
            Vector3 start, stop;
            start.X = stop.X = Player.v.origin.x + (Player.v.velocity.x / speed * 16);
            start.Y = stop.Y = Player.v.origin.y + (Player.v.velocity.y / speed * 16);
            start.Z = Player.v.origin.z + Player.v.mins.z;
            stop.Z = start.Z - 34;

            var trace = Move(ref start, ref Utilities.ZeroVector, ref Utilities.ZeroVector, ref stop, 1, Player);
            var friction = Host.Cvars.Friction.Get<float>();
            if (trace.fraction == 1.0)
            {
                friction *= Host.Cvars.EdgeFriction.Get<float>();
            }

            // apply friction
            var control = speed < Host.Cvars.StopSpeed.Get<float>() ? Host.Cvars.StopSpeed.Get<float>() : speed;
            var newspeed = (float)(speed - (Host.FrameTime * control * friction));

            if (newspeed < 0)
            {
                newspeed = 0;
            }

            newspeed /= speed;

            MathLib.VectorScale(ref Player.v.velocity, newspeed, out Player.v.velocity);
        }

        /// <summary>
        /// SV_Accelerate
        /// </summary>
        private void Accelerate()
        {
            var currentspeed = Vector3.Dot(Utilities.ToVector(ref Player.v.velocity), _WishDir);
            var addspeed = _WishSpeed - currentspeed;
            if (addspeed <= 0)
            {
                return;
            }

            var accelspeed = (float)(Host.Cvars.Accelerate.Get<float>() * Host.FrameTime * _WishSpeed);
            if (accelspeed > addspeed)
            {
                accelspeed = addspeed;
            }

            Player.v.velocity.x += _WishDir.X * accelspeed;
            Player.v.velocity.y += _WishDir.Y * accelspeed;
            Player.v.velocity.z += _WishDir.Z * accelspeed;
        }

        /// <summary>
        /// SV_AirAccelerate
        /// </summary>
        private void AirAccelerate(Vector3 wishveloc)
        {
            var wishspd = MathLib.Normalize(ref wishveloc);
            if (wishspd > 30)
            {
                wishspd = 30;
            }

            var currentspeed = Vector3.Dot(Utilities.ToVector(ref Player.v.velocity), wishveloc);
            var addspeed = wishspd - currentspeed;
            if (addspeed <= 0)
            {
                return;
            }

            var accelspeed = (float)(Host.Cvars.Accelerate.Get<float>() * _WishSpeed * Host.FrameTime);
            if (accelspeed > addspeed)
            {
                accelspeed = addspeed;
            }

            wishveloc *= accelspeed;
            Player.v.velocity.x += wishveloc.X;
            Player.v.velocity.y += wishveloc.Y;
            Player.v.velocity.z += wishveloc.Z;
        }
    }
}
