﻿/// <copyright>
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

using System;
using System.Net;
using System.Net.Sockets;

namespace SharpQuake.Framework
{
    // struct net_landriver_t
    public interface INetLanDriver
    {
        string Name
        {
            get;
        }

        bool IsInitialised
        {
            get;
        }

        Socket ControlSocket
        {
            get;
        }

        bool Initialise();

        void Dispose();

        void Listen(bool state);

        Socket OpenSocket(int port);

        int CloseSocket(Socket socket);

        int Connect(Socket socket, EndPoint addr);

        Socket CheckNewConnections();

        int Read(Socket socket, byte[] buf, int len, ref EndPoint ep);

        int Write(Socket socket, byte[] buf, int len, EndPoint ep);

        int Broadcast(Socket socket, byte[] buf, int len);

        string GetNameFromAddr(EndPoint addr);

        EndPoint GetAddrFromName(string name);

        int AddrCompare(EndPoint addr1, EndPoint addr2);

        int GetSocketPort(EndPoint addr);

        int SetSocketPort(EndPoint addr, int port);
    } //net_landriver_t;
}
