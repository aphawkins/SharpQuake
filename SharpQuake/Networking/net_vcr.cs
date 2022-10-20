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
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Text;
    using SharpQuake.Framework;

    internal static class VcrOp
    {
        public const int VCR_OP_CONNECT = 1;
        public const int VCR_OP_GETMESSAGE = 2;
        public const int VCR_OP_SENDMESSAGE = 3;
        public const int VCR_OP_CANSENDMESSAGE = 4;

        public const int VCR_MAX_MESSAGE = 4;
    }

    internal class net_vcr : INetDriver
    {
        private VcrRecord _Next;

        #region INetDriver Members

        public string Name => "VCR";

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

            _Next = Utilities.ReadStructure<VcrRecord>(Host.VcrReader.BaseStream);
            IsInitialised = true;
        }

        public void Listen(bool state)
        {
            // nothing to do
        }

        public void SearchForHosts(bool xmit)
        {
            // nothing to do
        }

        public qsocket_t Connect(string host)
        {
            return null;
        }

        public qsocket_t CheckNewConnections()
        {
            if (Host.Time != _Next.time || _Next.op != VcrOp.VCR_OP_CONNECT)
            {
                Utilities.Error("VCR missmatch");
            }

            if (_Next.session == 0)
            {
                ReadNext();
                return null;
            }

            var sock = Host.Network.NewSocket();
            sock.driverdata = _Next.session;

            var buf = new byte[NetworkDef.NET_NAMELEN];
            Host.VcrReader.Read(buf, 0, buf.Length);
            sock.address = Encoding.ASCII.GetString(buf);

            ReadNext();

            return sock;
        }

        public int GetMessage(qsocket_t sock)
        {
            if (Host.Time != _Next.time || _Next.op != VcrOp.VCR_OP_GETMESSAGE || _Next.session != SocketToSession(sock))
            {
                Utilities.Error("VCR missmatch");
            }

            var ret = Host.VcrReader.ReadInt32();
            if (ret != 1)
            {
                ReadNext();
                return ret;
            }

            var length = Host.VcrReader.ReadInt32();
            Host.Network.Message.FillFrom(Host.VcrReader.BaseStream, length);

            ReadNext();

            return 1;
        }

        public int SendMessage(qsocket_t sock, MessageWriter data)
        {
            if (Host.Time != _Next.time || _Next.op != VcrOp.VCR_OP_SENDMESSAGE || _Next.session != SocketToSession(sock))
            {
                Utilities.Error("VCR missmatch");
            }

            var ret = Host.VcrReader.ReadInt32();

            ReadNext();

            return ret;
        }

        public int SendUnreliableMessage(qsocket_t sock, MessageWriter data)
        {
            throw new NotImplementedException();
        }

        public bool CanSendMessage(qsocket_t sock)
        {
            if (Host.Time != _Next.time || _Next.op != VcrOp.VCR_OP_CANSENDMESSAGE || _Next.session != SocketToSession(sock))
            {
                Utilities.Error("VCR missmatch");
            }

            var ret = Host.VcrReader.ReadInt32();

            ReadNext();

            return ret != 0;
        }

        public bool CanSendUnreliableMessage(qsocket_t sock)
        {
            return true;
        }

        public void Close(qsocket_t sock)
        {
            // nothing to do
        }

        public void Shutdown()
        {
            // nothing to do
        }

        /// <summary>
        /// VCR_ReadNext
        /// </summary>
        private void ReadNext()
        {
            try
            {
                _Next = Utilities.ReadStructure<VcrRecord>(Host.VcrReader.BaseStream);
            }
            catch (IOException)
            {
                _Next = new VcrRecord
                {
                    op = 255
                };
                Utilities.Error("=== END OF PLAYBACK===\n");
            }
            if (_Next.op is < 1 or > VcrOp.VCR_MAX_MESSAGE)
            {
                Utilities.Error("VCR_ReadNext: bad op");
            }
        }

        #endregion INetDriver Members

        public static long SocketToSession(qsocket_t sock)
        {
            return (long)sock.driverdata;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal class VcrRecord
    {
        public double time;
        public int op;
        public long session;

        public static int SizeInBytes = Marshal.SizeOf(typeof(VcrRecord));
    }
}
