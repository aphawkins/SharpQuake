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
    using System.Net;
    using System.Net.Sockets;
    using System.Runtime.Versioning;
    using SharpQuake.Framework;

    public sealed class NetTcpIp : INetLanDriver, IDisposable
    {
        private bool _disposed;

        public static NetTcpIp Instance { get; } = new NetTcpIp();

        private const int WSAEWOULDBLOCK = 10035;
        private const int WSAECONNREFUSED = 10061;
        private IPAddress _MyAddress; // unsigned long myAddr
        private Socket _BroadcastSocket; // net_broadcastsocket
        private EndPoint _BroadcastAddress; // qsockaddr broadcastaddr
        private Socket _AcceptSocket; // net_acceptsocket

        private NetTcpIp()
        {
        }

        #region INetLanDriver Members

        public string Name => "TCP/IP";

        public bool IsInitialised { get; private set; }

        public Socket ControlSocket { get; private set; }

        public string MachineName
        {
            get;
            private set;
        }

        public string HostName
        {
            get;
            set;
        }

        public string HostAddress
        {
            get;
            private set;
        }

        public int HostPort
        {
            get;
            set;
        }

        /// <summary>
        /// UDP_Init
        /// </summary>
        public bool Initialise()
        {
            ThrowIfDisposed();

            IsInitialised = false;

            if (CommandLine.HasParam("-noudp"))
            {
                return false;
            }

            try
            {
                MachineName = Dns.GetHostName();
            }
            catch (SocketException se)
            {
                ConsoleWrapper.DPrint("Cannot get host name: {0}\n", se.Message);
                return false;
            }

            // if the quake hostname isn't set, set it to the machine name
            if (HostName == "UNNAMED")
            {
                if (!IPAddress.TryParse(MachineName, out IPAddress addr))
                {
                    var i = MachineName.IndexOf('.');
                    HostName = i != -1 ? MachineName[..i] : MachineName;
                }
                //CVar.Set( "hostname", MachineName );
            }

            var i2 = CommandLine.CheckParm("-ip");
            if (i2 > 0)
            {
                if (i2 < CommandLine.Argc - 1)
                {
                    var ipaddr = CommandLine.Argv(i2 + 1);
                    if (!IPAddress.TryParse(ipaddr, out _MyAddress))
                    {
                        Utilities.Error("{0} is not a valid IP address!", ipaddr);
                    }

                    HostAddress = ipaddr;
                }
                else
                {
                    Utilities.Error("Net.Init: you must specify an IP address after -ip");
                }
            }
            else
            {
                _MyAddress = IPAddress.Any;
                HostAddress = "INADDR_ANY";
                //Host.Network.MyTcpIpAddress = "INADDR_ANY";
            }

            ControlSocket = OpenSocket(0);

            if (ControlSocket == null)
            {
                ConsoleWrapper.Print("TCP/IP: Unable to open control socket\n");
                return false;
            }

            _BroadcastAddress = new IPEndPoint(IPAddress.Broadcast, HostPort);

            IsInitialised = true;
            ConsoleWrapper.Print("TCP/IP Initialized\n");
            return true;
        }

        /// <summary>
        /// UDP_Listen
        /// </summary>
        public void Listen(bool state)
        {
            ThrowIfDisposed();

            // enable listening
            if (state)
            {
                if (_AcceptSocket == null)
                {
                    _AcceptSocket = OpenSocket(HostPort);
                    if (_AcceptSocket == null)
                    {
                        Utilities.Error("UDP_Listen: Unable to open accept socket\n");
                    }
                }
            }
            else
            {
                // disable listening
                if (_AcceptSocket != null)
                {
                    CloseSocket(_AcceptSocket);
                    _AcceptSocket = null;
                }
            }
        }

        public Socket OpenSocket(int port)
        {
            ThrowIfDisposed();

            Socket result = null;
            try
            {
                result = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
                {
                    Blocking = false
                };

                if (OperatingSystem.IsWindows())
                {
                    result.SetIPProtectionLevel(IPProtectionLevel.Unrestricted);
                }

                EndPoint ep = new IPEndPoint(_MyAddress, port);
                result.Bind(ep);
            }
            catch (Exception ex)
            {
                if (result != null)
                {
                    result.Close();
                    result = null;
                }
                ConsoleWrapper.Print("Unable to create socket: " + ex.Message);
            }

            return result;
        }

        public int CloseSocket(Socket socket)
        {
            ThrowIfDisposed();

            if (socket == _BroadcastSocket)
            {
                _BroadcastSocket = null;
            }

            socket.Close();
            return 0;
        }

        public int Connect(Socket socket, EndPoint addr)
        {
            ThrowIfDisposed();

            return 0;
        }

        public string GetNameFromAddr(EndPoint addr)
        {
            ThrowIfDisposed();

            try
            {
                var entry = Dns.GetHostEntry(((IPEndPoint)addr).Address);
                return entry.HostName;
            }
            catch (SocketException)
            {
            }
            return string.Empty;
        }

        public EndPoint GetAddrFromName(string name)
        {
            ThrowIfDisposed();

            try
            {
                var i = name.IndexOf(':');
                string saddr;
                var port = HostPort;
                if (i != -1)
                {
                    saddr = name[..i];
                    if (int.TryParse(name[(i + 1)..], out int p))
                    {
                        port = p;
                    }
                }
                else
                {
                    saddr = name;
                }

                if (IPAddress.TryParse(saddr, out IPAddress addr))
                {
                    return new IPEndPoint(addr, port);
                }
                var entry = Dns.GetHostEntry(name);
                foreach (var addr2 in entry.AddressList)
                {
                    return new IPEndPoint(addr2, port);
                }
            }
            catch (SocketException)
            {
            }
            return null;
        }

        public int AddrCompare(EndPoint addr1, EndPoint addr2)
        {
            ThrowIfDisposed();

            if (addr1.AddressFamily != addr2.AddressFamily)
            {
                return -1;
            }

            if (addr1 is not IPEndPoint ep1 || addr2 is not IPEndPoint ep2)
            {
                return -1;
            }

            if (!ep1.Address.Equals(ep2.Address))
            {
                return -1;
            }

            if (ep1.Port != ep2.Port)
            {
                return 1;
            }

            return 0;
        }

        public int GetSocketPort(EndPoint addr)
        {
            return ((IPEndPoint)addr).Port;
        }

        public int SetSocketPort(EndPoint addr, int port)
        {
            ((IPEndPoint)addr).Port = port;
            return 0;
        }

        public Socket CheckNewConnections()
        {
            ThrowIfDisposed();

            if (_AcceptSocket == null)
            {
                return null;
            }

            if (_AcceptSocket.Available > 0)
            {
                return _AcceptSocket;
            }

            return null;
        }

        public int Read(Socket socket, byte[] buf, int len, ref EndPoint ep)
        {
            ThrowIfDisposed();

            var ret = 0;
            try
            {
                ret = socket.ReceiveFrom(buf, len, SocketFlags.None, ref ep);
            }
            catch (SocketException se)
            {
                ret = se.ErrorCode is WSAEWOULDBLOCK or WSAECONNREFUSED ? 0 : -1;
            }
            return ret;
        }

        public int Write(Socket socket, byte[] buf, int len, EndPoint ep)
        {
            ThrowIfDisposed();

            var ret = 0;
            try
            {
                ret = socket.SendTo(buf, len, SocketFlags.None, ep);
            }
            catch (SocketException se)
            {
                ret = se.ErrorCode == WSAEWOULDBLOCK ? 0 : -1;
            }
            return ret;
        }

        public int Broadcast(Socket socket, byte[] buf, int len)
        {
            ThrowIfDisposed();

            if (socket != _BroadcastSocket)
            {
                if (_BroadcastSocket != null)
                {
                    Utilities.Error("Attempted to use multiple broadcasts sockets\n");
                }

                try
                {
                    socket.EnableBroadcast = true;
                }
                catch (SocketException se)
                {
                    ConsoleWrapper.Print("Unable to make socket broadcast capable: {0}\n", se.Message);
                    return -1;
                }
            }

            return Write(socket, buf, len, _BroadcastAddress);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Listen(false);
            CloseSocket(ControlSocket);

            _disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }


        #endregion INetLanDriver Members
    }
}
