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
	using System.Collections.Generic;
	using System.Net;
	using System.Net.Sockets;
	using System.Runtime.InteropServices;
	using System.Text;
	using SharpQuake.Framework;
	using SharpQuake.Framework.IO;

	internal delegate void PollHandler(object arg);

	public class Network
	{
		public INetDriver[] Drivers { get; private set; }

		public INetLanDriver[] LanDrivers { get; private set; }

        public IEnumerable<QuakeSocket> ActiveSockets => _ActiveSockets;

        public IEnumerable<QuakeSocket> FreeSockets => _ActiveSockets;

        public int MessagesSent { get; private set; } = 0;

		public int MessagesReceived { get; private set; } = 0;

		public int UnreliableMessagesSent { get; private set; } = 0;

		public int UnreliableMessagesReceived { get; private set; } = 0;

        public string HostName => Host.Cvars.HostName.Get<string>();

        public string MyTcpIpAddress { get; set; }

		public int DefaultHostPort { get; private set; } = 26000;

        public static bool TcpIpAvailable => NetTcpIp.Instance.IsInitialised;

        public HostCache[] HostCache { get; } = new HostCache[NetworkDef.HOSTCACHESIZE];

		public int DriverLevel { get; private set; }

        public INetLanDriver LanDriver => LanDrivers[LanDriverLevel];

        public INetDriver Driver => Drivers[DriverLevel];

        public bool SlistInProgress { get; private set; }

		public double Time { get; private set; }


		public int HostPort;

		public int ActiveConnections;

		public MessageWriter Message;

		// sizebuf_t net_message
		public MessageReader Reader;

		public int HostCacheCount;

		public bool SlistSilent;

		// slistSilent
		public bool SlistLocal = true;

		public int LanDriverLevel;

		private readonly PollProcedure _SlistSendProcedure;
		private readonly PollProcedure _SlistPollProcedure;

		// net_landriver_t	net_landrivers[MAX_NET_DRIVERS]
		private bool _IsRecording;

		// int	DEFAULTnet_hostport = 26000;
		// net_hostport;
		private bool _IsListening;

		// qboolean	listening = false;
		private List<QuakeSocket> _FreeSockets;

		// net_freeSockets
		private List<QuakeSocket> _ActiveSockets;
		private PollProcedure _PollProcedureList;

		// slistInProgress
		// slistLocal
		private int _SlistLastShown;

		// slistLastShown
		private double _SlistStartTime;
		private VcrRecord _VcrConnect = new();

		// vcrConnect
		private VcrRecord2 _VcrGetMessage = new();

		// vcrGetMessage
		private VcrRecord2 _VcrSendMessage = new();

		public Network(Host host)
		{
			Host = host;

			_SlistSendProcedure = new PollProcedure(null, 0.0, SlistSend, null);
			_SlistPollProcedure = new PollProcedure(null, 0.0, SlistPoll, null);

			// Temporary workaround will sort out soon
			NetworkWrapper.OnGetLanDriver += (index) => LanDrivers[index];
		}

		// CHANGE
		private Host Host
		{
			get;
			set;
		}

		// vcrSendMessage
		// NET_Init (void)
		public void Initialise()
		{
			for (var i2 = 0; i2 < HostCache.Length; i2++)
            {
                HostCache[i2] = new HostCache();
            }

            Drivers ??= CommandLine.HasParam("-playback")
                    ? (new INetDriver[]
                    {
                        new NetworkVcr()
                    })
                    : (new INetDriver[]
                    {
                        new NetworkLoop(),
                        NetworkDatagram.Instance
                    });

			LanDrivers ??= new INetLanDriver[]
				{
					NetTcpIp.Instance
				};

			if (CommandLine.HasParam("-record"))
            {
                _IsRecording = true;
            }

            var i = CommandLine.CheckParm("-port");
			if (i == 0)
            {
                i = CommandLine.CheckParm("-udpport");
            }

            if (i == 0)
            {
                i = CommandLine.CheckParm("-ipxport");
            }

            if (i > 0)
			{
				if (i < CommandLine.Argc - 1)
                {
                    DefaultHostPort = MathLib.AToI(CommandLine.Argv(i + 1));
                }
                else
                {
                    Utilities.Error("Net.Init: you must specify a number after -port!");
                }
            }
			HostPort = DefaultHostPort;

			if (CommandLine.HasParam("-listen") || Host.Client.Cls.state == ClientActive.ca_dedicated)
            {
                _IsListening = true;
            }

            var numsockets = Host.Server.ServerStatic.maxclientslimit;
			if (Host.Client.Cls.state != ClientActive.ca_dedicated)
            {
                numsockets++;
            }

            _FreeSockets = new List<QuakeSocket>(numsockets);
			_ActiveSockets = new List<QuakeSocket>(numsockets);

			for (i = 0; i < numsockets; i++)
            {
                _FreeSockets.Add(new QuakeSocket());
            }

            SetNetTime();

			// allocate space for network message buffer
			Message = new MessageWriter(NetworkDef.NET_MAXMESSAGE); // SZ_Alloc (&net_message, NET_MAXMESSAGE);
			Reader = new MessageReader(Message);

			if (Host.Cvars.MessageTimeout == null)
			{
				Host.Cvars.MessageTimeout = Host.CVars.Add("net_messagetimeout", 300);
				Host.Cvars.HostName = Host.CVars.Add("hostname", "UNNAMED");
			}

			Host.Commands.Add("slist", Slist_f);
			Host.Commands.Add("listen", Listen_f);
			Host.Commands.Add("maxplayers", MaxPlayers_f);
			Host.Commands.Add("port", Port_f);

			// initialize all the drivers
			DriverLevel = 0;
			foreach (var driver in Drivers)
			{
				driver.Initialise(Host);
				if (driver.IsInitialised && _IsListening)
				{
					driver.Listen(true);
				}
				DriverLevel++;
			}

			//if (*my_ipx_address)
			//    Con_DPrintf("IPX address %s\n", my_ipx_address);
			if (!string.IsNullOrEmpty(MyTcpIpAddress))
            {
                Host.Console.DPrint("TCP/IP address {0}\n", MyTcpIpAddress);
            }
        }

		// net_driverlevel
		// net_landriverlevel
		/// <summary>
		/// NET_Shutdown
		/// </summary>
		public void Shutdown()
		{
			SetNetTime();

			if (_ActiveSockets != null)
			{
				var tmp = _ActiveSockets.ToArray();
				foreach (var sock in tmp)
                {
                    Close(sock);
                }
            }

			//
			// shutdown the drivers
			//
			if (Drivers != null)
			{
				for (DriverLevel = 0; DriverLevel < Drivers.Length; DriverLevel++)
				{
					if (Drivers[DriverLevel].IsInitialised)
                    {
                        Drivers[DriverLevel].Shutdown();
                    }
                }
			}
		}

		// slistStartTime
		/// <summary>
		/// NET_CheckNewConnections
		/// </summary>
		/// <returns></returns>
		public QuakeSocket CheckNewConnections()
		{
			SetNetTime();

			for (DriverLevel = 0; DriverLevel < Drivers.Length; DriverLevel++)
			{
				if (!Drivers[DriverLevel].IsInitialised)
                {
                    continue;
                }

                if (DriverLevel > 0 && !_IsListening)
                {
                    continue;
                }

                var ret = Driver.CheckNewConnections();
				if (ret != null)
				{
					if (_IsRecording)
					{
						_VcrConnect.time = Host.Time;
						_VcrConnect.op = VcrOp.VCR_OP_CONNECT;
						_VcrConnect.session = 1; // (long)ret; // Uze: todo: make it work on 64bit systems
						var buf = Utilities.StructureToBytes(ref _VcrConnect);
						Host.VcrWriter.Write(buf, 0, buf.Length);
						buf = Encoding.ASCII.GetBytes(ret.address);
						var count = Math.Min(buf.Length, NetworkDef.NET_NAMELEN);
						var extra = NetworkDef.NET_NAMELEN - count;
						Host.VcrWriter.Write(buf, 0, count);
						for (var i = 0; i < extra; i++)
                        {
                            Host.VcrWriter.Write((byte)0);
                        }
                    }
					return ret;
				}
			}

			if (_IsRecording)
			{
				_VcrConnect.time = Host.Time;
				_VcrConnect.op = VcrOp.VCR_OP_CONNECT;
				_VcrConnect.session = 0;
				var buf = Utilities.StructureToBytes(ref _VcrConnect);
				Host.VcrWriter.Write(buf, 0, buf.Length);
			}

			return null;
		}

		// hostcache
		// hostCacheCount
		/// <summary>
		/// NET_Connect
		/// called by client to connect to a host.  Returns -1 if not able to connect
		/// </summary>
		public QuakeSocket Connect(string host)
		{
			SetNetTime();

			if (string.IsNullOrEmpty(host))
            {
                host = null;
            }

            if (host != null)
			{
				if (Utilities.SameText(host, "local"))
				{
					goto JustDoIt;
				}

				if (HostCacheCount > 0)
				{
					foreach (var hc in HostCache)
					{
						if (Utilities.SameText(hc.name, host))
						{
							host = hc.cname;
							goto JustDoIt;
						}
					}
				}
			}

			SlistSilent = host != null;
			Slist_f(null);

			while (SlistInProgress)
            {
                Poll();
            }

            if (host == null)
			{
				if (HostCacheCount != 1)
                {
                    return null;
                }

                host = HostCache[0].cname;
				Host.Console.Print("Connecting to...\n{0} @ {1}\n\n", HostCache[0].name, host);
			}

			DriverLevel = 0;
			foreach (var hc in HostCache)
			{
				if (Utilities.SameText(host, hc.name))
				{
					host = hc.cname;
					break;
				}
				DriverLevel++;
			}

		JustDoIt:
			DriverLevel = 0;
			foreach (var drv in Drivers)
			{
				if (!drv.IsInitialised)
                {
                    continue;
                }

                var ret = drv.Connect(host);
				if (ret != null)
                {
                    return ret;
                }

                DriverLevel++;
			}

			if (host != null)
			{
				Host.Console.Print("\n");
				PrintSlistHeader();
				PrintSlist();
				PrintSlistTrailer();
			}

			return null;
		}

		/// <summary>
		/// NET_CanSendMessage
		/// Returns true or false if the given qsocket can currently accept a
		/// message to be transmitted.
		/// </summary>
		public bool CanSendMessage(QuakeSocket sock)
		{
			if (sock == null)
            {
                return false;
            }

            if (sock.disconnected)
            {
                return false;
            }

            SetNetTime();

			var r = Drivers[sock.driver].CanSendMessage(sock);

			if (_IsRecording)
			{
				_VcrSendMessage.time = Host.Time;
				_VcrSendMessage.op = VcrOp.VCR_OP_CANSENDMESSAGE;
				_VcrSendMessage.session = 1; // (long)sock; Uze: todo: do something?
				_VcrSendMessage.ret = r ? 1 : 0;
				var buf = Utilities.StructureToBytes(ref _VcrSendMessage);
				Host.VcrWriter.Write(buf, 0, buf.Length);
			}

			return r;
		}

		/// <summary>
		/// NET_GetMessage
		/// returns data in net_message sizebuf
		/// returns 0 if no data is waiting
		/// returns 1 if a message was received
		/// returns 2 if an unreliable message was received
		/// returns -1 if the connection died
		/// </summary>
		public int GetMessage(QuakeSocket sock)
		{
			//int ret;

			if (sock == null)
            {
                return -1;
            }

            if (sock.disconnected)
			{
				Host.Console.Print("NET_GetMessage: disconnected socket\n");
				return -1;
			}

			SetNetTime();

			var ret = Drivers[sock.driver].GetMessage(sock);

			// see if this connection has timed out
			if (ret == 0 && sock.driver != 0)
			{
				if (Time - sock.lastMessageTime > Host.Cvars.MessageTimeout.Get<int>())
				{
					Close(sock);
					return -1;
				}
			}

			if (ret > 0)
			{
				if (sock.driver != 0)
				{
					sock.lastMessageTime = Time;
					if (ret == 1)
                    {
                        MessagesReceived++;
                    }
                    else if (ret == 2)
                    {
                        UnreliableMessagesReceived++;
                    }
                }

				if (_IsRecording)
				{
					_VcrGetMessage.time = Host.Time;
					_VcrGetMessage.op = VcrOp.VCR_OP_GETMESSAGE;
					_VcrGetMessage.session = 1;// (long)sock; Uze todo: write somethisng meaningful
					_VcrGetMessage.ret = ret;
					var buf = Utilities.StructureToBytes(ref _VcrGetMessage);
					Host.VcrWriter.Write(buf, 0, buf.Length);
					Host.VcrWriter.Write(Message.Length);
					Host.VcrWriter.Write(Message.Data, 0, Message.Length);
				}
			}
			else
			{
				if (_IsRecording)
				{
					_VcrGetMessage.time = Host.Time;
					_VcrGetMessage.op = VcrOp.VCR_OP_GETMESSAGE;
					_VcrGetMessage.session = 1; // (long)sock; Uze todo: fix this
					_VcrGetMessage.ret = ret;
					var buf = Utilities.StructureToBytes(ref _VcrGetMessage);
					Host.VcrWriter.Write(buf, 0, buf.Length);
				}
			}

			return ret;
		}

		/// <summary>
		/// NET_SendMessage
		/// Try to send a complete length+message unit over the reliable stream.
		/// returns 0 if the message cannot be delivered reliably, but the connection
		/// is still considered valid
		/// returns 1 if the message was sent properly
		/// returns -1 if the connection died
		/// </summary>
		public int SendMessage(QuakeSocket sock, MessageWriter data)
		{
			if (sock == null)
            {
                return -1;
            }

            if (sock.disconnected)
			{
				Host.Console.Print("NET_SendMessage: disconnected socket\n");
				return -1;
			}

			SetNetTime();

			var r = Drivers[sock.driver].SendMessage(sock, data);
			if (r == 1 && sock.driver != 0)
            {
                MessagesSent++;
            }

            if (_IsRecording)
			{
				_VcrSendMessage.time = Host.Time;
				_VcrSendMessage.op = VcrOp.VCR_OP_SENDMESSAGE;
				_VcrSendMessage.session = 1; // (long)sock; Uze: todo: do something?
				_VcrSendMessage.ret = r;
				var buf = Utilities.StructureToBytes(ref _VcrSendMessage);
				Host.VcrWriter.Write(buf, 0, buf.Length);
			}

			return r;
		}

		/// <summary>
		/// NET_SendUnreliableMessage
		/// returns 0 if the message connot be delivered reliably, but the connection
		///		is still considered valid
		/// returns 1 if the message was sent properly
		/// returns -1 if the connection died
		/// </summary>
		public int SendUnreliableMessage(QuakeSocket sock, MessageWriter data)
		{
			if (sock == null)
            {
                return -1;
            }

            if (sock.disconnected)
			{
				Host.Console.Print("NET_SendMessage: disconnected socket\n");
				return -1;
			}

			SetNetTime();

			var r = Drivers[sock.driver].SendUnreliableMessage(sock, data);
			if (r == 1 && sock.driver != 0)
            {
                UnreliableMessagesSent++;
            }

            if (_IsRecording)
			{
				_VcrSendMessage.time = Host.Time;
				_VcrSendMessage.op = VcrOp.VCR_OP_SENDMESSAGE;
				_VcrSendMessage.session = 1;// (long)sock; Uze todo: ???????
				_VcrSendMessage.ret = r;
				var buf = Utilities.StructureToBytes(ref _VcrSendMessage);
				Host.VcrWriter.Write(buf);
			}

			return r;
		}

		/// <summary>
		/// NET_SendToAll
		/// This is a reliable *blocking* send to all attached clients.
		/// </summary>
		public int SendToAll(MessageWriter data, int blocktime)
		{
			var state1 = new bool[QDef.MAX_SCOREBOARD];
			var state2 = new bool[QDef.MAX_SCOREBOARD];

			var count = 0;
			for (var i = 0; i < Host.Server.ServerStatic.maxclients; i++)
			{
				Host.HostClient = Host.Server.ServerStatic.clients[i];
				if (Host.HostClient.netconnection == null)
                {
                    continue;
                }

                if (Host.HostClient.active)
				{
					if (Host.HostClient.netconnection.driver == 0)
					{
						SendMessage(Host.HostClient.netconnection, data);
						state1[i] = true;
						state2[i] = true;
						continue;
					}
					count++;
					state1[i] = false;
					state2[i] = false;
				}
				else
				{
					state1[i] = true;
					state2[i] = true;
				}
			}

			var start = Timer.GetFloatTime();
			while (count > 0)
			{
				count = 0;
				for (var i = 0; i < Host.Server.ServerStatic.maxclients; i++)
				{
					Host.HostClient = Host.Server.ServerStatic.clients[i];
					if (!state1[i])
					{
						if (CanSendMessage(Host.HostClient.netconnection))
						{
							state1[i] = true;
							SendMessage(Host.HostClient.netconnection, data);
						}
						else
						{
							GetMessage(Host.HostClient.netconnection);
						}
						count++;
						continue;
					}

					if (!state2[i])
					{
						if (CanSendMessage(Host.HostClient.netconnection))
						{
							state2[i] = true;
						}
						else
						{
							GetMessage(Host.HostClient.netconnection);
						}
						count++;
						continue;
					}
				}
				if ((Timer.GetFloatTime() - start) > blocktime)
                {
                    break;
                }
            }
			return count;
		}

		/// <summary>
		/// NET_Close
		/// </summary>
		public void Close(QuakeSocket sock)
		{
			if (sock == null)
            {
                return;
            }

            if (sock.disconnected)
            {
                return;
            }

            SetNetTime();

			// call the driver_Close function
			Drivers[sock.driver].Close(sock);

			FreeSocket(sock);
		}

		/// <summary>
		/// NET_FreeQSocket
		/// </summary>
		public void FreeSocket(QuakeSocket sock)
		{
			// remove it from active list
			if (!_ActiveSockets.Remove(sock))
            {
                Utilities.Error("NET_FreeQSocket: not active\n");
            }

            // add it to free list
            _FreeSockets.Add(sock);
			sock.disconnected = true;
		}

		/// <summary>
		/// NET_Poll
		/// </summary>
		public void Poll()
		{
			SetNetTime();

			for (var pp = _PollProcedureList; pp != null; pp = pp.next)
			{
				if (pp.nextTime > Time)
                {
                    break;
                }

                _PollProcedureList = pp.next;
				pp.procedure(pp.arg);
			}
		}

		// double SetNetTime
		public double SetNetTime()
		{
			Time = Timer.GetFloatTime();
			return Time;
		}

		/// <summary>
		/// NET_Slist_f
		/// </summary>
		public void Slist_f(CommandMessage msg)
		{
			if (SlistInProgress)
            {
                return;
            }

            if (!SlistSilent)
			{
				Host.Console.Print("Looking for Quake servers...\n");
				PrintSlistHeader();
			}

			SlistInProgress = true;
			_SlistStartTime = Timer.GetFloatTime();

			SchedulePollProcedure(_SlistSendProcedure, 0.0);
			SchedulePollProcedure(_SlistPollProcedure, 0.1);

			HostCacheCount = 0;
		}

		/// <summary>
		/// NET_NewQSocket
		/// Called by drivers when a new communications endpoint is required
		/// The sequence and buffer fields will be filled in properly
		/// </summary>
		public QuakeSocket NewSocket()
		{
			if (_FreeSockets.Count == 0)
            {
                return null;
            }

            if (ActiveConnections >= Host.Server.ServerStatic.maxclients)
            {
                return null;
            }

            // get one from free list
            var i = _FreeSockets.Count - 1;
			var sock = _FreeSockets[i];
			_FreeSockets.RemoveAt(i);

			// add it to active list
			_ActiveSockets.Add(sock);

			sock.disconnected = false;
			sock.connecttime = Time;
			sock.address = "UNSET ADDRESS";
			sock.driver = DriverLevel;
			sock.socket = null;
			sock.driverdata = null;
			sock.canSend = true;
			sock.sendNext = false;
			sock.lastMessageTime = Time;
			sock.ackSequence = 0;
			sock.sendSequence = 0;
			sock.unreliableSendSequence = 0;
			sock.sendMessageLength = 0;
			sock.receiveSequence = 0;
			sock.unreliableReceiveSequence = 0;
			sock.receiveMessageLength = 0;

			return sock;
		}

		// pollProcedureList
		private void PrintSlistHeader()
		{
			Host.Console.Print("Server          Map             Users\n");
			Host.Console.Print("--------------- --------------- -----\n");
			_SlistLastShown = 0;
		}

		// = { "hostname", "UNNAMED" };
		private void PrintSlist()
		{
			int i;
			for (i = _SlistLastShown; i < HostCacheCount; i++)
			{
				var hc = HostCache[i];
				if (hc.maxusers != 0)
                {
                    Host.Console.Print("{0,-15} {1,-15}\n {2,2}/{3,2}\n", Utilities.Copy(hc.name, 15), Utilities.Copy(hc.map, 15), hc.users, hc.maxusers);
                }
                else
                {
                    Host.Console.Print("{0,-15} {1,-15}\n", Utilities.Copy(hc.name, 15), Utilities.Copy(hc.map, 15));
                }
            }
			_SlistLastShown = i;
		}

		private void PrintSlistTrailer()
		{
			if (HostCacheCount != 0)
            {
                Host.Console.Print("== end list ==\n\n");
            }
            else
            {
                Host.Console.Print("No Quake servers found.\n\n");
            }
        }

		/// <summary>
		/// SchedulePollProcedure
		/// </summary>
		private void SchedulePollProcedure(PollProcedure proc, double timeOffset)
		{
			proc.nextTime = Timer.GetFloatTime() + timeOffset;
			PollProcedure pp, prev;
			for (pp = _PollProcedureList, prev = null; pp != null; pp = pp.next)
			{
				if (pp.nextTime >= proc.nextTime)
                {
                    break;
                }

                prev = pp;
			}

			if (prev == null)
			{
				proc.next = _PollProcedureList;
				_PollProcedureList = proc;
				return;
			}

			proc.next = pp;
			prev.next = proc;
		}

		// NET_Listen_f
		private void Listen_f(CommandMessage msg)
		{
			if (msg.Parameters == null || msg.Parameters.Length != 1)
			{
				Host.Console.Print("\"listen\" is \"{0}\"\n", _IsListening ? 1 : 0);
				return;
			}

			_IsListening = MathLib.AToI(msg.Parameters[0]) != 0;

			foreach (var driver in Drivers)
			{
				if (driver.IsInitialised)
				{
					driver.Listen(_IsListening);
				}
			}
		}

		// MaxPlayers_f
		private void MaxPlayers_f(CommandMessage msg)
		{
			if (msg.Parameters == null || msg.Parameters.Length != 1)
			{
				Host.Console.Print($"\"maxplayers\" is \"{Host.Server.ServerStatic.maxclients}\"\n");
				return;
			}

			if (Host.Server.NetServer.active)
			{
				Host.Console.Print("maxplayers can not be changed while a server is running.\n");
				return;
			}

			var n = MathLib.AToI(msg.Parameters[0]);
			if (n < 1)
            {
                n = 1;
            }

            if (n > Host.Server.ServerStatic.maxclientslimit)
			{
				n = Host.Server.ServerStatic.maxclientslimit;
				Host.Console.Print("\"maxplayers\" set to \"{0}\"\n", n);
			}

			if (n == 1 && _IsListening)
            {
                Host.Commands.Buffer.Append("listen 0\n");
            }

            if (n > 1 && !_IsListening)
            {
                Host.Commands.Buffer.Append("listen 1\n");
            }

            Host.Server.ServerStatic.maxclients = n;
			if (n == 1)
            {
                Host.CVars.Set("deathmatch", 0);
            }
            else
            {
                Host.CVars.Set("deathmatch", 1);
            }
        }

		// NET_Port_f
		private void Port_f(CommandMessage msg)
		{
			if (msg.Parameters == null || msg.Parameters.Length != 1)
			{
				Host.Console.Print($"\"port\" is \"{HostPort}\"\n");
				return;
			}

			var n = MathLib.AToI(msg.Parameters[0]);
			if (n is < 1 or > 65534)
			{
				Host.Console.Print("Bad value, must be between 1 and 65534\n");
				return;
			}

			DefaultHostPort = n;
			HostPort = n;

			if (_IsListening)
			{
				// force a change to the new port
				Host.Commands.Buffer.Append("listen 0\n");
				Host.Commands.Buffer.Append("listen 1\n");
			}
		}

		/// <summary>
		/// Slist_Send
		/// </summary>
		private void SlistSend(object arg)
		{
			for (DriverLevel = 0; DriverLevel < Drivers.Length; DriverLevel++)
			{
				if (!SlistLocal && DriverLevel == 0)
                {
                    continue;
                }

                if (!Drivers[DriverLevel].IsInitialised)
                {
                    continue;
                }

                Drivers[DriverLevel].SearchForHosts(true);
			}

			if ((Timer.GetFloatTime() - _SlistStartTime) < 0.5)
            {
                SchedulePollProcedure(_SlistSendProcedure, 0.75);
            }
        }

		/// <summary>
		/// Slist_Poll
		/// </summary>
		private void SlistPoll(object arg)
		{
			for (DriverLevel = 0; DriverLevel < Drivers.Length; DriverLevel++)
			{
				if (!SlistLocal && DriverLevel == 0)
                {
                    continue;
                }

                if (!Drivers[DriverLevel].IsInitialised)
                {
                    continue;
                }

                Drivers[DriverLevel].SearchForHosts(false);
			}

			if (!SlistSilent)
            {
                PrintSlist();
            }

            if ((Timer.GetFloatTime() - _SlistStartTime) < 1.5)
			{
				SchedulePollProcedure(_SlistPollProcedure, 0.1);
				return;
			}

			if (!SlistSilent)
            {
                PrintSlistTrailer();
            }

            SlistInProgress = false;
			SlistSilent = false;
			SlistLocal = true;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		private class VcrRecord2 : VcrRecord
		{
			public int ret;
			// Uze: int len - removed
		} //vcrGetMessage;

		// Temporary fix to support pulling messagereader/writer from main code


	}

	public static class MessageWriterExtensions
	{
		public static int FillFrom(this MessageWriter writer, Network network, Socket socket, ref EndPoint ep)
		{
			writer.Clear();
			var result = network.LanDriver.Read(socket, writer._Buffer, writer._Buffer.Length, ref ep);
			if (result >= 0)
            {
                writer._Count = result;
            }

            return result;
		}
	}
	/// <summary>
	/// NetHeader flags
	/// </summary>
	internal static class NetFlags
	{
		public const int NETFLAG_LENGTH_MASK = 0x0000ffff;
		public const int NETFLAG_DATA = 0x00010000;
		public const int NETFLAG_ACK = 0x00020000;
		public const int NETFLAG_NAK = 0x00040000;
		public const int NETFLAG_EOM = 0x00080000;
		public const int NETFLAG_UNRELIABLE = 0x00100000;
		public const int NETFLAG_CTL = -2147483648;// 0x80000000;
	}

	internal static class CCReq
	{
		public const int CCREQ_CONNECT = 0x01;
		public const int CCREQ_SERVER_INFO = 0x02;
		public const int CCREQ_PLAYER_INFO = 0x03;
		public const int CCREQ_RULE_INFO = 0x04;
	}

	//	note:
	//		There are two address forms used above.  The short form is just a
	//		port number.  The address that goes along with the port is defined as
	//		"whatever address you receive this reponse from".  This lets us use
	//		the host OS to solve the problem of multiple host addresses (possibly
	//		with no routing between them); the host will use the right address
	//		when we reply to the inbound connection request.  The long from is
	//		a full address and port in a string.  It is used for returning the
	//		address of a server that is not running locally.
	internal static class CCRep
	{
		public const int CCREP_ACCEPT = 0x81;
		public const int CCREP_REJECT = 0x82;
		public const int CCREP_SERVER_INFO = 0x83;
		public const int CCREP_PLAYER_INFO = 0x84;
		public const int CCREP_RULE_INFO = 0x85;
	}



	internal class PollProcedure
	{
		public PollProcedure next;
		public double nextTime;
		public PollHandler procedure; // void (*procedure)();
		public object arg; // void *arg

		public PollProcedure(PollProcedure next, double nextTime, PollHandler handler, object arg)
		{
			this.next = next;
			this.nextTime = nextTime;
			procedure = handler;
			this.arg = arg;
		}
	}






	// PollProcedure;

	//hostcache_t;
	// This is the network info/connection protocol.  It is used to find Quake
	// servers, get info about them, and connect to them.  Once connected, the
	// Quake game protocol (documented elsewhere) is used.
	//
	//
	// General notes:
	//	game_name is currently always "QUAKE", but is there so this same protocol
	//		can be used for future games as well; can you say Quake2?
	//
	// CCREQ_CONNECT
	//		string	game_name				"QUAKE"
	//		byte	net_protocol_version	NET_PROTOCOL_VERSION
	//
	// CCREQ_SERVER_INFO
	//		string	game_name				"QUAKE"
	//		byte	net_protocol_version	NET_PROTOCOL_VERSION
	//
	// CCREQ_PLAYER_INFO
	//		byte	player_number
	//
	// CCREQ_RULE_INFO
	//		string	rule
	//
	//
	//
	// CCREP_ACCEPT
	//		long	port
	//
	// CCREP_REJECT
	//		string	reason
	//
	// CCREP_SERVER_INFO
	//		string	server_address
	//		string	host_name
	//		string	level_name
	//		byte	current_players
	//		byte	max_players
	//		byte	protocol_version	NET_PROTOCOL_VERSION
	//
	// CCREP_PLAYER_INFO
	//		byte	player_number
	//		string	name
	//		long	colors
	//		long	frags
	//		long	connect_time
	//		string	address
	//
	// CCREP_RULE_INFO
	//		string	rule
	//		string	value
}
