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
    using System.Numerics;
    using SharpQuake.Framework;
    using SharpQuake.Framework.IO;
    using SharpQuake.Framework.IO.BSP;
    using SharpQuake.Game.Data.Models;
    using SharpQuake.Game.Networking.Server;
    using SharpQuake.Game.Rendering;
    using SharpQuake.Game.Rendering.Memory;

    public partial class Server
    {
        private int _FatBytes; // fatbytes
        private readonly byte[] _FatPvs = new byte[BspDef.MAX_MAP_LEAFS / 8]; // fatpvs

        // Instances
        private Host Host
        {
            get;
            set;
        }

        // SV_Init
        public void Initialise()
        {
            for (var i = 0; i < _BoxClipNodes.Length; i++)
            {
                _BoxClipNodes[i].children = new short[2];
            }
            for (var i = 0; i < _BoxPlanes.Length; i++)
            {
                _BoxPlanes[i] = new QuakePlane();
            }
            for (var i = 0; i < _AreaNodes.Length; i++)
            {
                _AreaNodes[i] = new AreaNode();
            }

            if (Host.Cvars.Friction == null)
            {
                Host.Cvars.Friction = Host.CVars.Add("sv_friction", 4f, ClientVariableFlags.Server);
                Host.Cvars.EdgeFriction = Host.CVars.Add("edgefriction", 2f);
                Host.Cvars.StopSpeed = Host.CVars.Add("sv_stopspeed", 100f);
                Host.Cvars.Gravity = Host.CVars.Add("sv_gravity", 800f, ClientVariableFlags.Server);
                Host.Cvars.MaxVelocity = Host.CVars.Add("sv_maxvelocity", 2000f);
                Host.Cvars.NoStep = Host.CVars.Add("sv_nostep", false);
                Host.Cvars.MaxSpeed = Host.CVars.Add("sv_maxspeed", 320f, ClientVariableFlags.Server);
                Host.Cvars.Accelerate = Host.CVars.Add("sv_accelerate", 10f);
                Host.Cvars.Aim = Host.CVars.Add("sv_aim", 0.93f);
                Host.Cvars.IdealPitchScale = Host.CVars.Add("sv_idealpitchscale", 0.8f);
            }

            for (var i = 0; i < QDef.MAX_MODELS; i++)
            {
                _LocalModels[i] = "*" + i.ToString();
            }
        }

        /// <summary>
        /// SV_StartParticle
        /// Make sure the event gets sent to all clients
        /// </summary>
        public void StartParticle(ref Vector3 org, ref Vector3 dir, int color, int count)
        {
            if (NetServer.datagram.Length > QDef.MAX_DATAGRAM - 16)
            {
                return;
            }

            NetServer.datagram.WriteByte(ProtocolDef.svc_particle);
            NetServer.datagram.WriteCoord(org.X);
            NetServer.datagram.WriteCoord(org.Y);
            NetServer.datagram.WriteCoord(org.Z);

            var max = Vector3.One * 127;
            var min = Vector3.One * -128;
            var v = Vector3.Clamp(dir * 16, min, max);
            NetServer.datagram.WriteChar((int)v.X);
            NetServer.datagram.WriteChar((int)v.Y);
            NetServer.datagram.WriteChar((int)v.Z);
            NetServer.datagram.WriteByte(count);
            NetServer.datagram.WriteByte(color);
        }

        /// <summary>
        /// SV_StartSound
        /// Each entity can have eight independant sound sources, like voice,
        /// weapon, feet, etc.
        ///
        /// Channel 0 is an auto-allocate channel, the others override anything
        /// allready running on that entity/channel pair.
        ///
        /// An attenuation of 0 will play full volume everywhere in the level.
        /// Larger attenuations will drop off.  (max 4 attenuation)
        /// </summary>
        public void StartSound(MemoryEdict entity, int channel, string sample, int volume, float attenuation)
        {
            if (volume is < 0 or > 255)
            {
                Utilities.Error("SV_StartSound: volume = {0}", volume);
            }

            if (attenuation is < 0 or > 4)
            {
                Utilities.Error("SV_StartSound: attenuation = {0}", attenuation);
            }

            if (channel is < 0 or > 7)
            {
                Utilities.Error("SV_StartSound: channel = {0}", channel);
            }

            if (NetServer.datagram.Length > QDef.MAX_DATAGRAM - 16)
            {
                return;
            }

            // find precache number for sound
            int sound_num;
            for (sound_num = 1; sound_num < QDef.MAX_SOUNDS && NetServer.sound_precache[sound_num] != null; sound_num++)
            {
                if (sample == NetServer.sound_precache[sound_num])
                {
                    break;
                }
            }

            if (sound_num == QDef.MAX_SOUNDS || string.IsNullOrEmpty(NetServer.sound_precache[sound_num]))
            {
                Host.Console.Print("SV_StartSound: {0} not precacheed\n", sample);
                return;
            }

            var ent = NumForEdict(entity);

            channel = (ent << 3) | channel;

            var field_mask = 0;
            if (volume != Sound.DEFAULT_SOUND_PACKET_VOLUME)
            {
                field_mask |= ProtocolDef.SND_VOLUME;
            }

            if (attenuation != Sound.DEFAULT_SOUND_PACKET_ATTENUATION)
            {
                field_mask |= ProtocolDef.SND_ATTENUATION;
            }

            // directed messages go only to the entity the are targeted on
            NetServer.datagram.WriteByte(ProtocolDef.svc_sound);
            NetServer.datagram.WriteByte(field_mask);
            if ((field_mask & ProtocolDef.SND_VOLUME) != 0)
            {
                NetServer.datagram.WriteByte(volume);
            }

            if ((field_mask & ProtocolDef.SND_ATTENUATION) != 0)
            {
                NetServer.datagram.WriteByte((int)(attenuation * 64));
            }

            NetServer.datagram.WriteShort(channel);
            NetServer.datagram.WriteByte(sound_num);
            MathLib.VectorAdd(ref entity.v.mins, ref entity.v.maxs, out Vector3f v);
            MathLib.VectorMA(ref entity.v.origin, 0.5f, ref v, out v);
            NetServer.datagram.WriteCoord(v.x);
            NetServer.datagram.WriteCoord(v.y);
            NetServer.datagram.WriteCoord(v.z);
        }

        /// <summary>
        /// SV_DropClient
        /// Called when the player is getting totally kicked off the host
        /// if (crash = true), don't bother sending signofs
        /// </summary>
        public void DropClient(bool crash)
        {
            var client = Host.HostClient;

            if (!crash)
            {
                // send any final messages (don't check for errors)
                if (Host.Network.CanSendMessage(client.netconnection))
                {
                    var msg = client.message;
                    msg.WriteByte(ProtocolDef.svc_disconnect);
                    Host.Network.SendMessage(client.netconnection, msg);
                }

                if (client.edict != null && client.spawned)
                {
                    // call the prog function for removing a client
                    // this will set the body to a dead frame, among other things
                    var saveSelf = Host.Programs.GlobalStruct.self;
                    Host.Programs.GlobalStruct.self = EdictToProg(client.edict);
                    Host.Programs.Execute(Host.Programs.GlobalStruct.ClientDisconnect);
                    Host.Programs.GlobalStruct.self = saveSelf;
                }

                Host.Console.DPrint("Client {0} removed\n", client.name);
            }

            // break the net connection
            Host.Network.Close(client.netconnection);
            client.netconnection = null;

            // free the client (the body stays around)
            client.active = false;
            client.name = null;
            client.old_frags = -999999;
            Host.Network.ActiveConnections--;

            // send notification to all clients
            for (var i = 0; i < ServerStatic.maxclients; i++)
            {
                var cl = ServerStatic.clients[i];
                if (!cl.active)
                {
                    continue;
                }

                cl.message.WriteByte(ProtocolDef.svc_updatename);
                cl.message.WriteByte(Host.ClientNum);
                cl.message.WriteString("");
                cl.message.WriteByte(ProtocolDef.svc_updatefrags);
                cl.message.WriteByte(Host.ClientNum);
                cl.message.WriteShort(0);
                cl.message.WriteByte(ProtocolDef.svc_updatecolors);
                cl.message.WriteByte(Host.ClientNum);
                cl.message.WriteByte(0);
            }
        }

        /// <summary>
        /// SV_SendClientMessages
        /// </summary>
        public void SendClientMessages()
        {
            // update frags, names, etc
            UpdateToReliableMessages();

            // build individual updates
            for (var i = 0; i < ServerStatic.maxclients; i++)
            {
                Host.HostClient = ServerStatic.clients[i];

                if (!Host.HostClient.active)
                {
                    continue;
                }

                if (Host.HostClient.spawned)
                {
                    if (!SendClientDatagram(Host.HostClient))
                    {
                        continue;
                    }
                }
                else
                {
                    // the player isn't totally in the game yet
                    // send small keepalive messages if too much time has passed
                    // send a full message when the next signon stage has been requested
                    // some other message data (name changes, etc) may accumulate
                    // between signon stages
                    if (!Host.HostClient.sendsignon)
                    {
                        if (Host.RealTime - Host.HostClient.last_message > 5)
                        {
                            SendNop(Host.HostClient);
                        }

                        continue;   // don't send out non-signon messages
                    }
                }

                // check for an overflowed message.  Should only happen
                // on a very fucked up connection that backs up a lot, then
                // changes level
                if (Host.HostClient.message.IsOveflowed)
                {
                    DropClient(true);
                    Host.HostClient.message.IsOveflowed = false;
                    continue;
                }

                if (Host.HostClient.message.Length > 0 || Host.HostClient.dropasap)
                {
                    if (!Host.Network.CanSendMessage(Host.HostClient.netconnection))
                    {
                        continue;
                    }

                    if (Host.HostClient.dropasap)
                    {
                        DropClient(false);    // went to another level
                    }
                    else
                    {
                        if (Host.Network.SendMessage(Host.HostClient.netconnection, Host.HostClient.message) == -1)
                        {
                            DropClient(true); // if the message couldn't send, kick off
                        }

                        Host.HostClient.message.Clear();
                        Host.HostClient.last_message = Host.RealTime;
                        Host.HostClient.sendsignon = false;
                    }
                }
            }

            // clear muzzle flashes
            CleanupEnts();
        }

        /// <summary>
        /// SV_ClearDatagram
        /// </summary>
        public void ClearDatagram()
        {
            NetServer.datagram.Clear();
        }

        /// <summary>
        /// SV_ModelIndex
        /// </summary>
        public int ModelIndex(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return 0;
            }

            int i;
            for (i = 0; i < QDef.MAX_MODELS && NetServer.model_precache[i] != null; i++)
            {
                if (NetServer.model_precache[i] == name)
                {
                    return i;
                }
            }

            if (i == QDef.MAX_MODELS || string.IsNullOrEmpty(NetServer.model_precache[i]))
            {
                Utilities.Error("SV_ModelIndex: model {0} not precached", name);
            }

            return i;
        }

        /// <summary>
        /// SV_ClientPrintf
        /// Sends text across to be displayed
        /// FIXME: make this just a stuffed echo?
        /// </summary>
        public void ClientPrint(string fmt, params object[] args)
        {
            var tmp = string.Format(fmt, args);
            Host.HostClient.message.WriteByte(ProtocolDef.svc_print);
            Host.HostClient.message.WriteString(tmp);
        }

        /// <summary>
        /// SV_BroadcastPrint
        /// </summary>
        public void BroadcastPrint(string fmt, params object[] args)
        {
            var tmp = args.Length > 0 ? string.Format(fmt, args) : fmt;
            for (var i = 0; i < ServerStatic.maxclients; i++)
            {
                if (ServerStatic.clients[i].active && ServerStatic.clients[i].spawned)
                {
                    var msg = ServerStatic.clients[i].message;
                    msg.WriteByte(ProtocolDef.svc_print);
                    msg.WriteString(tmp);
                }
            }
        }

        private void WriteClientDamageMessage(MemoryEdict ent, MessageWriter msg)
        {
            if (ent.v.dmg_take != 0 || ent.v.dmg_save != 0)
            {
                var other = ProgToEdict(ent.v.dmg_inflictor);
                msg.WriteByte(ProtocolDef.svc_damage);
                msg.WriteByte((int)ent.v.dmg_save);
                msg.WriteByte((int)ent.v.dmg_take);
                msg.WriteCoord(other.v.origin.x + (0.5f * (other.v.mins.x + other.v.maxs.x)));
                msg.WriteCoord(other.v.origin.y + (0.5f * (other.v.mins.y + other.v.maxs.y)));
                msg.WriteCoord(other.v.origin.z + (0.5f * (other.v.mins.z + other.v.maxs.z)));

                ent.v.dmg_take = 0;
                ent.v.dmg_save = 0;
            }
        }

        private static void WriteClientWeapons(MemoryEdict ent, MessageWriter msg)
        {
            if (MainWindow.Common.GameKind == GameKind.StandardQuake)
            {
                msg.WriteByte((int)ent.v.weapon);
            }
            else
            {
                for (var i = 0; i < 32; i++)
                {
                    if ((((int)ent.v.weapon) & (1 << i)) != 0)
                    {
                        msg.WriteByte(i);
                        break;
                    }
                }
            }
        }

        private static void WriteClientHeader(MessageWriter msg, int bits)
        {
            msg.WriteByte(ProtocolDef.svc_clientdata);
            msg.WriteShort(bits);
        }

        private static void WriteClientAmmo(MemoryEdict ent, MessageWriter msg)
        {
            msg.WriteByte((int)ent.v.currentammo);
            msg.WriteByte((int)ent.v.ammo_shells);
            msg.WriteByte((int)ent.v.ammo_nails);
            msg.WriteByte((int)ent.v.ammo_rockets);
            msg.WriteByte((int)ent.v.ammo_cells);
        }

        private static void WriteClientFixAngle(MemoryEdict ent, MessageWriter msg)
        {
            if (ent.v.fixangle != 0)
            {
                msg.WriteByte(ProtocolDef.svc_setangle);
                msg.WriteAngle(ent.v.angles.x);
                msg.WriteAngle(ent.v.angles.y);
                msg.WriteAngle(ent.v.angles.z);
                ent.v.fixangle = 0;
            }
        }

        private static void WriteClientView(MemoryEdict ent, MessageWriter msg, int bits)
        {
            if ((bits & ProtocolDef.SU_VIEWHEIGHT) != 0)
            {
                msg.WriteChar((int)ent.v.view_ofs.z);
            }

            if ((bits & ProtocolDef.SU_IDEALPITCH) != 0)
            {
                msg.WriteChar((int)ent.v.idealpitch);
            }
        }

        private static void WriteClientPunches(MemoryEdict ent, MessageWriter msg, int bits)
        {
            if ((bits & ProtocolDef.SU_PUNCH1) != 0)
            {
                msg.WriteChar((int)ent.v.punchangle.x);
            }

            if ((bits & ProtocolDef.SU_VELOCITY1) != 0)
            {
                msg.WriteChar((int)(ent.v.velocity.x / 16));
            }

            if ((bits & ProtocolDef.SU_PUNCH2) != 0)
            {
                msg.WriteChar((int)ent.v.punchangle.y);
            }

            if ((bits & ProtocolDef.SU_VELOCITY2) != 0)
            {
                msg.WriteChar((int)(ent.v.velocity.y / 16));
            }

            if ((bits & ProtocolDef.SU_PUNCH3) != 0)
            {
                msg.WriteChar((int)ent.v.punchangle.z);
            }

            if ((bits & ProtocolDef.SU_VELOCITY3) != 0)
            {
                msg.WriteChar((int)(ent.v.velocity.z / 16));
            }
        }

        private void WriteClientItems(MemoryEdict ent, MessageWriter msg, int items, int bits)
        {
            msg.WriteLong(items);

            if ((bits & ProtocolDef.SU_WEAPONFRAME) != 0)
            {
                msg.WriteByte((int)ent.v.weaponframe);
            }

            if ((bits & ProtocolDef.SU_ARMOR) != 0)
            {
                msg.WriteByte((int)ent.v.armorvalue);
            }

            if ((bits & ProtocolDef.SU_WEAPON) != 0)
            {
                msg.WriteByte(ModelIndex(Host.Programs.GetString(ent.v.weaponmodel)));
            }
        }

        private static void WriteClientHealth(MemoryEdict ent, MessageWriter msg)
        {
            msg.WriteShort((int)ent.v.health);
        }

        private int GenerateClientBits(MemoryEdict ent, out int items)
        {
            var bits = 0;

            if (ent.v.view_ofs.z != ProtocolDef.DEFAULT_VIEWHEIGHT)
            {
                bits |= ProtocolDef.SU_VIEWHEIGHT;
            }

            if (ent.v.idealpitch != 0)
            {
                bits |= ProtocolDef.SU_IDEALPITCH;
            }

            // stuff the sigil bits into the high bits of items for sbar, or else
            // mix in items2
            var val = Host.Programs.GetEdictFieldFloat(ent, "items2", 0);

            items = val != 0 ? (int)ent.v.items | ((int)val << 23) : (int)ent.v.items | ((int)Host.Programs.GlobalStruct.serverflags << 28);

            bits |= ProtocolDef.SU_ITEMS;

            if (((int)ent.v.flags & EdictFlags.FL_ONGROUND) != 0)
            {
                bits |= ProtocolDef.SU_ONGROUND;
            }

            if (ent.v.waterlevel >= 2)
            {
                bits |= ProtocolDef.SU_INWATER;
            }

            if (ent.v.punchangle.x != 0)
            {
                bits |= ProtocolDef.SU_PUNCH1;
            }

            if (ent.v.punchangle.y != 0)
            {
                bits |= ProtocolDef.SU_PUNCH2;
            }

            if (ent.v.punchangle.z != 0)
            {
                bits |= ProtocolDef.SU_PUNCH3;
            }

            if (ent.v.velocity.x != 0)
            {
                bits |= ProtocolDef.SU_VELOCITY1;
            }

            if (ent.v.velocity.y != 0)
            {
                bits |= ProtocolDef.SU_VELOCITY2;
            }

            if (ent.v.velocity.z != 0)
            {
                bits |= ProtocolDef.SU_VELOCITY3;
            }

            if (ent.v.weaponframe != 0)
            {
                bits |= ProtocolDef.SU_WEAPONFRAME;
            }

            if (ent.v.armorvalue != 0)
            {
                bits |= ProtocolDef.SU_ARMOR;
            }

            //	if (ent.v.weapon)
            bits |= ProtocolDef.SU_WEAPON;

            return bits;
        }

        /// <summary>
        /// SV_WriteClientdataToMessage
        /// </summary>
        public void WriteClientDataToMessage(MemoryEdict ent, MessageWriter msg)
        {
            //
            // send a damage message
            //
            WriteClientDamageMessage(ent, msg);

            //
            // send the current viewpos offset from the view entity
            //
            SetIdealPitch();        // how much to look up / down ideally

            // a fixangle might get lost in a dropped packet.  Oh well.
            WriteClientFixAngle(ent, msg);

            var bits = GenerateClientBits(ent, out var items);

            // send the data
            WriteClientHeader(msg, bits);
            WriteClientView(ent, msg, bits);
            WriteClientPunches(ent, msg, bits);

            // always sent
            WriteClientItems(ent, msg, items, bits);
            WriteClientHealth(ent, msg);
            WriteClientAmmo(ent, msg);
            WriteClientWeapons(ent, msg);
        }

        /// <summary>
        /// SV_CheckForNewClients
        /// </summary>
        public void CheckForNewClients()
        {
            //
            // check for new connections
            //
            while (true)
            {
                var ret = Host.Network.CheckNewConnections();
                if (ret == null)
                {
                    break;
                }

                //
                // init a new client structure
                //
                int i;
                for (i = 0; i < ServerStatic.maxclients; i++)
                {
                    if (!ServerStatic.clients[i].active)
                    {
                        break;
                    }
                }

                if (i == ServerStatic.maxclients)
                {
                    Utilities.Error("Host_CheckForNewClients: no free clients");
                }

                ServerStatic.clients[i].netconnection = ret;
                ConnectClient(i);

                Host.Network.ActiveConnections++;
            }
        }

        /// <summary>
        /// SV_SaveSpawnparms
        /// Grabs the current state of each client for saving across the
        /// transition to another level
        /// </summary>
        public void SaveSpawnparms()
        {
            ServerStatic.serverflags = (int)Host.Programs.GlobalStruct.serverflags;

            for (var i = 0; i < ServerStatic.maxclients; i++)
            {
                Host.HostClient = ServerStatic.clients[i];
                if (!Host.HostClient.active)
                {
                    continue;
                }

                // call the progs to get default spawn parms for the new client
                Host.Programs.GlobalStruct.self = EdictToProg(Host.HostClient.edict);
                Host.Programs.Execute(Host.Programs.GlobalStruct.SetChangeParms);
                AssignGlobalSpawnparams(Host.HostClient);
            }
        }

        /// <summary>
        /// SV_SpawnServer
        /// </summary>
        public void SpawnServer(string server)
        {
            // let's not have any servers with no name
            if (string.IsNullOrEmpty(Host.Network.HostName))
            {
                Host.CVars.Set("hostname", "UNNAMED");
            }

            Host.Screen.CenterTimeOff = 0;

            Host.Console.DPrint("SpawnServer: {0}\n", server);
            ServerStatic.changelevel_issued = false;     // now safe to issue another

            //
            // tell all connected clients that we are going to a new level
            //
            if (NetServer.active)
            {
                SendReconnect();
            }

            //
            // make cvars consistant
            //
            if (Host.Cvars.Coop.Get<bool>())
            {
                Host.CVars.Set("deathmatch", 0);
            }

            Host.CurrentSkill = (int)(Host.Cvars.Skill.Get<int>() + 0.5);
            if (Host.CurrentSkill < 0)
            {
                Host.CurrentSkill = 0;
            }

            if (Host.CurrentSkill > 3)
            {
                Host.CurrentSkill = 3;
            }

            Host.CVars.Set("skill", Host.CurrentSkill);

            //
            // set up the new server
            //
            Host.ClearMemory();

            NetServer.Clear();

            NetServer.name = server;

            // load progs to get entity field count
            Host.Programs.LoadProgs();

            // allocate server memory
            NetServer.max_edicts = QDef.MAX_EDICTS;

            NetServer.edicts = new MemoryEdict[NetServer.max_edicts];
            for (var i = 0; i < NetServer.edicts.Length; i++)
            {
                NetServer.edicts[i] = new MemoryEdict();
            }

            // leave slots at start for clients only
            NetServer.num_edicts = ServerStatic.maxclients + 1;
            MemoryEdict ent;
            for (var i = 0; i < ServerStatic.maxclients; i++)
            {
                ent = EdictNum(i + 1);
                ServerStatic.clients[i].edict = ent;
            }

            NetServer.state = ServerState.Loading;
            NetServer.paused = false;
            NetServer.time = 1.0;
            NetServer.modelname = string.Format("maps/{0}.bsp", server);
            NetServer.worldmodel = (BrushModelData)Host.Model.ForName(NetServer.modelname, false, ModelType.mod_brush);
            if (NetServer.worldmodel == null)
            {
                Host.Console.Print("Couldn't spawn server {0}\n", NetServer.modelname);
                NetServer.active = false;
                return;
            }
            NetServer.models[1] = NetServer.worldmodel;

            //
            // clear world interaction links
            //
            ClearWorld();

            NetServer.sound_precache[0] = string.Empty;
            NetServer.model_precache[0] = string.Empty;

            NetServer.model_precache[1] = NetServer.modelname;
            for (var i = 1; i < NetServer.worldmodel.NumSubModels; i++)
            {
                NetServer.model_precache[1 + i] = _LocalModels[i];
                NetServer.models[i + 1] = Host.Model.ForName(_LocalModels[i], false, ModelType.mod_brush);
            }

            //
            // load the rest of the entities
            //
            ent = EdictNum(0);
            ent.Clear();
            ent.v.model = Host.Programs.StringOffset(NetServer.worldmodel.Name);
            if (ent.v.model == -1)
            {
                ent.v.model = Host.Programs.NewString(NetServer.worldmodel.Name);
            }
            ent.v.modelindex = 1;       // world model
            ent.v.solid = Solids.SOLID_BSP;
            ent.v.movetype = Movetypes.MOVETYPE_PUSH;

            if (Host.Cvars.Coop.Get<bool>())
            {
                Host.Programs.GlobalStruct.coop = 1; //coop.value;
            }
            else
            {
                Host.Programs.GlobalStruct.deathmatch = Host.Cvars.Deathmatch.Get<int>();
            }

            var offset = Host.Programs.NewString(NetServer.name);
            Host.Programs.GlobalStruct.mapname = offset;

            // serverflags are for cross level information (sigils)
            Host.Programs.GlobalStruct.serverflags = ServerStatic.serverflags;

            Host.Programs.LoadFromFile(NetServer.worldmodel.Entities);

            NetServer.active = true;

            // all setup is completed, any further precache statements are errors
            NetServer.state = ServerState.Active;

            // run two frames to allow everything to settle
            Host.FrameTime = 0.1;
            Physics();
            Physics();

            // create a baseline for more efficient communications
            CreateBaseline();

            // send serverinfo to all connected clients
            for (var i = 0; i < ServerStatic.maxclients; i++)
            {
                Host.HostClient = ServerStatic.clients[i];
                if (Host.HostClient.active)
                {
                    SendServerInfo(Host.HostClient);
                }
            }

            GC.Collect();
            Host.Console.DPrint("Server spawned.\n");
        }

        /// <summary>
        /// SV_CleanupEnts
        /// </summary>
        private void CleanupEnts()
        {
            for (var i = 1; i < NetServer.num_edicts; i++)
            {
                var ent = NetServer.edicts[i];
                ent.v.effects = (int)ent.v.effects & ~EntityEffects.EF_MUZZLEFLASH;
            }
        }

        /// <summary>
        /// SV_SendNop
        /// Send a nop message without trashing or sending the accumulated client
        /// message buffer
        /// </summary>
        private void SendNop(FrameworkClient client)
        {
            var msg = new MessageWriter(4);
            msg.WriteChar(ProtocolDef.svc_nop);

            if (Host.Network.SendUnreliableMessage(client.netconnection, msg) == -1)
            {
                DropClient(true); // if the message couldn't send, kick off
            }

            client.last_message = Host.RealTime;
        }

        /// <summary>
        /// SV_SendClientDatagram
        /// </summary>
        private bool SendClientDatagram(FrameworkClient client)
        {
            var msg = new MessageWriter(QDef.MAX_DATAGRAM); // Uze todo: make static?

            msg.WriteByte(ProtocolDef.svc_time);
            msg.WriteFloat((float)NetServer.time);

            // add the client specific data to the datagram
            WriteClientDataToMessage(client.edict, msg);

            WriteEntitiesToClient(client.edict, msg);

            // copy the server datagram if there is space
            if (msg.Length + NetServer.datagram.Length < msg.Capacity)
            {
                msg.Write(NetServer.datagram.Data, 0, NetServer.datagram.Length);
            }

            // send the datagram
            if (Host.Network.SendUnreliableMessage(client.netconnection, msg) == -1)
            {
                DropClient(true);// if the message couldn't send, kick off
                return false;
            }

            return true;
        }

        /// <summary>
        /// SV_WriteEntitiesToClient
        /// </summary>
        private void WriteEntitiesToClient(MemoryEdict clent, MessageWriter msg)
        {
            // find the client's PVS
            var org = Utilities.ToVector(ref clent.v.origin) + Utilities.ToVector(ref clent.v.view_ofs);
            var pvs = FatPVS(ref org);

            // send over all entities (except the client) that touch the pvs
            for (var e = 1; e < NetServer.num_edicts; e++)
            {
                var ent = NetServer.edicts[e];
                // ignore if not touching a PV leaf
                if (ent != clent) // clent is ALLWAYS sent
                {
                    // ignore ents without visible models
                    var mname = Host.Programs.GetString(ent.v.model);
                    if (string.IsNullOrEmpty(mname))
                    {
                        continue;
                    }

                    int i;
                    for (i = 0; i < ent.num_leafs; i++)
                    {
                        if ((pvs[ent.leafnums[i] >> 3] & (1 << (ent.leafnums[i] & 7))) != 0)
                        {
                            break;
                        }
                    }

                    if (i == ent.num_leafs)
                    {
                        continue;       // not visible
                    }
                }

                if (msg.Capacity - msg.Length < 16)
                {
                    Host.Console.Print("packet overflow\n");
                    return;
                }

                // send an update
                var bits = 0;
                MathLib.VectorSubtract(ref ent.v.origin, ref ent.baseline.origin, out Vector3f miss);
                if (miss.x is < (-0.1f) or > 0.1f)
                {
                    bits |= ProtocolDef.U_ORIGIN1;
                }

                if (miss.y is < (-0.1f) or > 0.1f)
                {
                    bits |= ProtocolDef.U_ORIGIN2;
                }

                if (miss.z is < (-0.1f) or > 0.1f)
                {
                    bits |= ProtocolDef.U_ORIGIN3;
                }

                if (ent.v.angles.x != ent.baseline.angles.x)
                {
                    bits |= ProtocolDef.U_ANGLE1;
                }

                if (ent.v.angles.y != ent.baseline.angles.y)
                {
                    bits |= ProtocolDef.U_ANGLE2;
                }

                if (ent.v.angles.z != ent.baseline.angles.z)
                {
                    bits |= ProtocolDef.U_ANGLE3;
                }

                if (ent.v.movetype == Movetypes.MOVETYPE_STEP)
                {
                    bits |= ProtocolDef.U_NOLERP;   // don't mess up the step animation
                }

                if (ent.baseline.colormap != ent.v.colormap)
                {
                    bits |= ProtocolDef.U_COLORMAP;
                }

                if (ent.baseline.skin != ent.v.skin)
                {
                    bits |= ProtocolDef.U_SKIN;
                }

                if (ent.baseline.frame != ent.v.frame)
                {
                    bits |= ProtocolDef.U_FRAME;
                }

                if (ent.baseline.effects != ent.v.effects)
                {
                    bits |= ProtocolDef.U_EFFECTS;
                }

                if (ent.baseline.modelindex != ent.v.modelindex)
                {
                    bits |= ProtocolDef.U_MODEL;
                }

                if (e >= 256)
                {
                    bits |= ProtocolDef.U_LONGENTITY;
                }

                if (bits >= 256)
                {
                    bits |= ProtocolDef.U_MOREBITS;
                }

                //
                // write the message
                //
                msg.WriteByte(bits | ProtocolDef.U_SIGNAL);

                if ((bits & ProtocolDef.U_MOREBITS) != 0)
                {
                    msg.WriteByte(bits >> 8);
                }

                if ((bits & ProtocolDef.U_LONGENTITY) != 0)
                {
                    msg.WriteShort(e);
                }
                else
                {
                    msg.WriteByte(e);
                }

                if ((bits & ProtocolDef.U_MODEL) != 0)
                {
                    msg.WriteByte((int)ent.v.modelindex);
                }

                if ((bits & ProtocolDef.U_FRAME) != 0)
                {
                    msg.WriteByte((int)ent.v.frame);
                }

                if ((bits & ProtocolDef.U_COLORMAP) != 0)
                {
                    msg.WriteByte((int)ent.v.colormap);
                }

                if ((bits & ProtocolDef.U_SKIN) != 0)
                {
                    msg.WriteByte((int)ent.v.skin);
                }

                if ((bits & ProtocolDef.U_EFFECTS) != 0)
                {
                    msg.WriteByte((int)ent.v.effects);
                }

                if ((bits & ProtocolDef.U_ORIGIN1) != 0)
                {
                    msg.WriteCoord(ent.v.origin.x);
                }

                if ((bits & ProtocolDef.U_ANGLE1) != 0)
                {
                    msg.WriteAngle(ent.v.angles.x);
                }

                if ((bits & ProtocolDef.U_ORIGIN2) != 0)
                {
                    msg.WriteCoord(ent.v.origin.y);
                }

                if ((bits & ProtocolDef.U_ANGLE2) != 0)
                {
                    msg.WriteAngle(ent.v.angles.y);
                }

                if ((bits & ProtocolDef.U_ORIGIN3) != 0)
                {
                    msg.WriteCoord(ent.v.origin.z);
                }

                if ((bits & ProtocolDef.U_ANGLE3) != 0)
                {
                    msg.WriteAngle(ent.v.angles.z);
                }
            }
        }

        /// <summary>
        /// SV_FatPVS
        /// Calculates a PVS that is the inclusive or of all leafs within 8 pixels of the
        /// given point.
        /// </summary>
        private byte[] FatPVS(ref Vector3 org)
        {
            _FatBytes = (NetServer.worldmodel.NumLeafs + 31) >> 3;
            Array.Clear(_FatPvs, 0, _FatPvs.Length);
            AddToFatPVS(ref org, NetServer.worldmodel.Nodes[0]);
            return _FatPvs;
        }

        /// <summary>
        /// SV_AddToFatPVS
        /// The PVS must include a small area around the client to allow head bobbing
        /// or other small motion on the client side.  Otherwise, a bob might cause an
        /// entity that should be visible to not show up, especially when the bob
        /// crosses a waterline.
        /// </summary>
        private void AddToFatPVS(ref Vector3 org, MemoryNodeBase node)
        {
            while (true)
            {
                // if this is a leaf, accumulate the pvs bits
                if (node.contents < 0)
                {
                    if (node.contents != (int)Q1Contents.Solid)
                    {
                        var pvs = NetServer.worldmodel.LeafPVS((MemoryLeaf)node);
                        for (var i = 0; i < _FatBytes; i++)
                        {
                            _FatPvs[i] |= pvs[i];
                        }
                    }
                    return;
                }

                var n = (MemoryNode)node;
                var plane = n.plane;
                var d = Vector3.Dot(org, plane.normal) - plane.dist;
                if (d > 8)
                {
                    node = n.children[0];
                }
                else if (d < -8)
                {
                    node = n.children[1];
                }
                else
                {   // go down both
                    AddToFatPVS(ref org, n.children[0]);
                    node = n.children[1];
                }
            }
        }

        /// <summary>
        /// SV_UpdateToReliableMessages
        /// </summary>
        private void UpdateToReliableMessages()
        {
            // check for changes to be sent over the reliable streams
            for (var i = 0; i < ServerStatic.maxclients; i++)
            {
                Host.HostClient = ServerStatic.clients[i];
                if (Host.HostClient.old_frags != Host.HostClient.edict.v.frags)
                {
                    for (var j = 0; j < ServerStatic.maxclients; j++)
                    {
                        var client = ServerStatic.clients[j];
                        if (!client.active)
                        {
                            continue;
                        }

                        client.message.WriteByte(ProtocolDef.svc_updatefrags);
                        client.message.WriteByte(i);
                        client.message.WriteShort((int)Host.HostClient.edict.v.frags);
                    }

                    Host.HostClient.old_frags = (int)Host.HostClient.edict.v.frags;
                }
            }

            for (var j = 0; j < ServerStatic.maxclients; j++)
            {
                var client = ServerStatic.clients[j];
                if (!client.active)
                {
                    continue;
                }

                client.message.Write(NetServer.reliable_datagram.Data, 0, NetServer.reliable_datagram.Length);
            }

            NetServer.reliable_datagram.Clear();
        }

        /// <summary>
        /// SV_ConnectClient
        /// Initializes a client_t for a new net connection.  This will only be called
        /// once for a player each game, not once for each level change.
        /// </summary>
        private void ConnectClient(int clientnum)
        {
            var client = ServerStatic.clients[clientnum];

            Host.Console.DPrint("Client {0} connected\n", client.netconnection.address);

            var edictnum = clientnum + 1;
            var ent = EdictNum(edictnum);

            // set up the client_t
            var netconnection = client.netconnection;

            var spawn_parms = new float[ServerDef.NUM_SPAWN_PARMS];
            if (NetServer.loadgame)
            {
                Array.Copy(client.spawn_parms, spawn_parms, spawn_parms.Length);
            }

            client.Clear();
            client.netconnection = netconnection;
            client.name = "unconnected";
            client.active = true;
            client.spawned = false;
            client.edict = ent;
            client.message.AllowOverflow = true; // we can catch it
            client.privileged = false;

            if (NetServer.loadgame)
            {
                Array.Copy(spawn_parms, client.spawn_parms, spawn_parms.Length);
            }
            else
            {
                // call the progs to get default spawn parms for the new client
                Host.Programs.Execute(Host.Programs.GlobalStruct.SetNewParms);

                AssignGlobalSpawnparams(client);
            }

            SendServerInfo(client);
        }

        private void AssignGlobalSpawnparams(FrameworkClient client)
        {
            client.spawn_parms[0] = Host.Programs.GlobalStruct.parm1;
            client.spawn_parms[1] = Host.Programs.GlobalStruct.parm2;
            client.spawn_parms[2] = Host.Programs.GlobalStruct.parm3;
            client.spawn_parms[3] = Host.Programs.GlobalStruct.parm4;

            client.spawn_parms[4] = Host.Programs.GlobalStruct.parm5;
            client.spawn_parms[5] = Host.Programs.GlobalStruct.parm6;
            client.spawn_parms[6] = Host.Programs.GlobalStruct.parm7;
            client.spawn_parms[7] = Host.Programs.GlobalStruct.parm8;

            client.spawn_parms[8] = Host.Programs.GlobalStruct.parm9;
            client.spawn_parms[9] = Host.Programs.GlobalStruct.parm10;
            client.spawn_parms[10] = Host.Programs.GlobalStruct.parm11;
            client.spawn_parms[11] = Host.Programs.GlobalStruct.parm12;

            client.spawn_parms[12] = Host.Programs.GlobalStruct.parm13;
            client.spawn_parms[13] = Host.Programs.GlobalStruct.parm14;
            client.spawn_parms[14] = Host.Programs.GlobalStruct.parm15;
            client.spawn_parms[15] = Host.Programs.GlobalStruct.parm16;
        }

        /// <summary>
        /// SV_SendServerinfo
        /// Sends the first message from the server to a connected client.
        /// This will be sent on the initial connection and upon each server load.
        /// </summary>
        private void SendServerInfo(FrameworkClient client)
        {
            var writer = client.message;

            writer.WriteByte(ProtocolDef.svc_print);
            writer.WriteString(string.Format("{0}\nVERSION {1,4:F2} SERVER ({2} CRC)", (char)2, QDef.VERSION, Host.Programs.Crc));

            writer.WriteByte(ProtocolDef.svc_serverinfo);
            writer.WriteLong(ProtocolDef.PROTOCOL_VERSION);
            writer.WriteByte(ServerStatic.maxclients);

            if (!Host.Cvars.Coop.Get<bool>() && Host.Cvars.Deathmatch.Get<int>() != 0)
            {
                writer.WriteByte(ProtocolDef.GAME_DEATHMATCH);
            }
            else
            {
                writer.WriteByte(ProtocolDef.GAME_COOP);
            }

            var message = Host.Programs.GetString(NetServer.edicts[0].v.message);

            writer.WriteString(message);

            for (var i = 1; i < NetServer.model_precache.Length; i++)
            {
                var tmp = NetServer.model_precache[i];
                if (string.IsNullOrEmpty(tmp))
                {
                    break;
                }

                writer.WriteString(tmp);
            }
            writer.WriteByte(0);

            for (var i = 1; i < NetServer.sound_precache.Length; i++)
            {
                var tmp = NetServer.sound_precache[i];
                if (tmp == null)
                {
                    break;
                }

                writer.WriteString(tmp);
            }
            writer.WriteByte(0);

            // send music
            writer.WriteByte(ProtocolDef.svc_cdtrack);
            writer.WriteByte((int)NetServer.edicts[0].v.sounds);
            writer.WriteByte((int)NetServer.edicts[0].v.sounds);

            // set view
            writer.WriteByte(ProtocolDef.svc_setview);
            writer.WriteShort(NumForEdict(client.edict));

            writer.WriteByte(ProtocolDef.svc_signonnum);
            writer.WriteByte(1);

            client.sendsignon = true;
            client.spawned = false;     // need prespawn, spawn, etc
        }

        /// <summary>
        /// SV_SendReconnect
        /// Tell all the clients that the server is changing levels
        /// </summary>
        private void SendReconnect()
        {
            var msg = new MessageWriter(128);

            msg.WriteChar(ProtocolDef.svc_stufftext);
            msg.WriteString("reconnect\n");
            Host.Network.SendToAll(msg, 5);

            if (Host.Client.Cls.state != ClientActive.ca_dedicated)
            {
                Host.Commands.ExecuteString("reconnect\n", CommandSource.Command);
            }
        }

        /// <summary>
        /// SV_CreateBaseline
        /// </summary>
        private void CreateBaseline()
        {
            for (var entnum = 0; entnum < NetServer.num_edicts; entnum++)
            {
                // get the current server version
                var svent = EdictNum(entnum);
                if (svent.free)
                {
                    continue;
                }

                if (entnum > ServerStatic.maxclients && svent.v.modelindex == 0)
                {
                    continue;
                }

                //
                // create entity baseline
                //
                svent.baseline.origin = svent.v.origin;
                svent.baseline.angles = svent.v.angles;
                svent.baseline.frame = (int)svent.v.frame;
                svent.baseline.skin = (int)svent.v.skin;
                if (entnum > 0 && entnum <= ServerStatic.maxclients)
                {
                    svent.baseline.colormap = entnum;
                    svent.baseline.modelindex = ModelIndex("progs/player.mdl");
                }
                else
                {
                    svent.baseline.colormap = 0;
                    svent.baseline.modelindex = ModelIndex(Host.Programs.GetString(svent.v.model));
                }

                //
                // add to the message
                //
                NetServer.signon.WriteByte(ProtocolDef.svc_spawnbaseline);
                NetServer.signon.WriteShort(entnum);

                NetServer.signon.WriteByte(svent.baseline.modelindex);
                NetServer.signon.WriteByte(svent.baseline.frame);
                NetServer.signon.WriteByte(svent.baseline.colormap);
                NetServer.signon.WriteByte(svent.baseline.skin);

                NetServer.signon.WriteCoord(svent.baseline.origin.x);
                NetServer.signon.WriteAngle(svent.baseline.angles.x);
                NetServer.signon.WriteCoord(svent.baseline.origin.y);
                NetServer.signon.WriteAngle(svent.baseline.angles.y);
                NetServer.signon.WriteCoord(svent.baseline.origin.z);
                NetServer.signon.WriteAngle(svent.baseline.angles.z);
            }
        }
    }
}
