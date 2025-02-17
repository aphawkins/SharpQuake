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
    using SharpQuake.Framework;

    internal class NetworkLoop : INetDriver
    {
        private bool _LocalConnectPending; // localconnectpending
        private QuakeSocket _Client; // loop_client
        private QuakeSocket _Server; // loop_server

        #region INetDriver Members

        public string Name => "Loopback";

        public bool IsInitialised { get; private set; }

        // CHANGE
        private Host Host
        {
            get;
            set;
        }

        public void Initialise(object host)
        {
            Host = (Host)host;

            if (Host.Client.Cls.state == ClientActive.ca_dedicated)
            {
                return;// -1;
            }

            IsInitialised = true;
        }

        public void Listen(bool state)
        {
            // nothig to do
        }

        public void SearchForHosts(bool xmit)
        {
            if (!Host.Server.NetServer.active)
            {
                return;
            }

            Host.Network.HostCacheCount = 1;
            Host.Network.HostCache[0].name = Host.Network.HostName == "UNNAMED" ? "local" : Host.Network.HostName;

            Host.Network.HostCache[0].map = Host.Server.NetServer.name;
            Host.Network.HostCache[0].users = Host.Network.ActiveConnections;
            Host.Network.HostCache[0].maxusers = Host.Server.ServerStatic.maxclients;
            Host.Network.HostCache[0].driver = Host.Network.DriverLevel;
            Host.Network.HostCache[0].cname = "local";
        }

        public QuakeSocket Connect(string host)
        {
            if (host != "local")
            {
                return null;
            }

            _LocalConnectPending = true;

            if (_Client == null)
            {
                _Client = Host.Network.NewSocket();
                if (_Client == null)
                {
                    Host.Console.Print("Loop_Connect: no qsocket available\n");
                    return null;
                }
                _Client.address = "localhost";
            }
            _Client.ClearBuffers();
            _Client.canSend = true;

            if (_Server == null)
            {
                _Server = Host.Network.NewSocket();
                if (_Server == null)
                {
                    Host.Console.Print("Loop_Connect: no qsocket available\n");
                    return null;
                }
                _Server.address = "LOCAL";
            }
            _Server.ClearBuffers();
            _Server.canSend = true;

            _Client.driverdata = _Server;
            _Server.driverdata = _Client;

            return _Client;
        }

        public QuakeSocket CheckNewConnections()
        {
            if (!_LocalConnectPending)
            {
                return null;
            }

            _LocalConnectPending = false;
            _Server.ClearBuffers();
            _Server.canSend = true;
            _Client.ClearBuffers();
            _Client.canSend = true;
            return _Server;
        }

        public int GetMessage(QuakeSocket sock)
        {
            if (sock.receiveMessageLength == 0)
            {
                return 0;
            }

            int ret = sock.receiveMessage[0];
            var length = sock.receiveMessage[1] + (sock.receiveMessage[2] << 8);

            // alignment byte skipped here
            Host.Network.Message.Clear();
            Host.Network.Message.FillFrom(sock.receiveMessage, 4, length);

            length = IntAlign(length + 4);
            sock.receiveMessageLength -= length;

            if (sock.receiveMessageLength > 0)
            {
                Array.Copy(sock.receiveMessage, length, sock.receiveMessage, 0, sock.receiveMessageLength);
            }

            if (sock.driverdata != null && ret == 1)
            {
                ((QuakeSocket)sock.driverdata).canSend = true;
            }

            return ret;
        }

        public int SendMessage(QuakeSocket sock, MessageWriter data)
        {
            if (sock.driverdata == null)
            {
                return -1;
            }

            var sock2 = (QuakeSocket)sock.driverdata;

            if ((sock2.receiveMessageLength + data.Length + 4) > NetworkDef.NET_MAXMESSAGE)
            {
                Utilities.Error("Loop_SendMessage: overflow\n");
            }

            // message type
            var offset = sock2.receiveMessageLength;
            sock2.receiveMessage[offset++] = 1;

            // length
            sock2.receiveMessage[offset++] = (byte)(data.Length & 0xff);
            sock2.receiveMessage[offset++] = (byte)(data.Length >> 8);

            // align
            offset++;

            // message
            Buffer.BlockCopy(data.Data, 0, sock2.receiveMessage, offset, data.Length);
            sock2.receiveMessageLength = IntAlign(sock2.receiveMessageLength + data.Length + 4);

            sock.canSend = false;
            return 1;
        }

        public int SendUnreliableMessage(QuakeSocket sock, MessageWriter data)
        {
            if (sock.driverdata == null)
            {
                return -1;
            }

            var sock2 = (QuakeSocket)sock.driverdata;

            if ((sock2.receiveMessageLength + data.Length + sizeof(byte) + sizeof(short)) > NetworkDef.NET_MAXMESSAGE)
            {
                return 0;
            }

            var offset = sock2.receiveMessageLength;

            // message type
            sock2.receiveMessage[offset++] = 2;

            // length
            sock2.receiveMessage[offset++] = (byte)(data.Length & 0xff);
            sock2.receiveMessage[offset++] = (byte)(data.Length >> 8);

            // align
            offset++;

            // message
            Buffer.BlockCopy(data.Data, 0, sock2.receiveMessage, offset, data.Length);
            sock2.receiveMessageLength = IntAlign(sock2.receiveMessageLength + data.Length + 4);

            return 1;
        }

        public bool CanSendMessage(QuakeSocket sock)
        {
            if (sock.driverdata == null)
            {
                return false;
            }

            return sock.canSend;
        }

        public bool CanSendUnreliableMessage(QuakeSocket sock)
        {
            return true;
        }

        public void Close(QuakeSocket sock)
        {
            if (sock.driverdata != null)
            {
                ((QuakeSocket)sock.driverdata).driverdata = null;
            }

            sock.ClearBuffers();
            sock.canSend = true;
            if (sock == _Client)
            {
                _Client = null;
            }
            else
            {
                _Server = null;
            }
        }

        public void Shutdown()
        {
            IsInitialised = false;
        }

        private static int IntAlign(int value)
        {
            return (value + (sizeof(int) - 1)) & (~(sizeof(int) - 1));
        }

        #endregion INetDriver Members
    }
}
