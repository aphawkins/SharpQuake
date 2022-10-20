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

using System;
using OpenTK;

namespace SharpQuake.Framework
{
    public static class EndianHelper
    {
        public static IByteOrderConverter Converter { get; private set; }

        public static bool IsBigEndian
        {
            get
            {
                return !BitConverter.IsLittleEndian;
            }
        }

        static EndianHelper( )
        {
            // set the byte swapping variables in a portable manner
            if ( BitConverter.IsLittleEndian )
            {
                Converter = new LittleEndianConverter( );
            }
            else
            {
                Converter = new BigEndianConverter( );
            }
        }

        public static short BigShort(short l )
        {
            return Converter.BigShort( l );
        }

        public static short LittleShort(short l )
        {
            return Converter.LittleShort( l );
        }

        public static int BigLong(int l )
        {
            return Converter.BigLong( l );
        }

        public static int LittleLong(int l )
        {
            return Converter.LittleLong( l );
        }

        public static float BigFloat(float l )
        {
            return Converter.BigFloat( l );
        }

        public static float LittleFloat(float l )
        {
            return Converter.LittleFloat( l );
        }

        public static Vector3 LittleVector( Vector3 src )
        {
            return new Vector3( Converter.LittleFloat( src.X ),
                Converter.LittleFloat( src.Y ), Converter.LittleFloat( src.Z ) );
        }

        public static Vector3 LittleVector3(float[] src )
        {
            return new Vector3( Converter.LittleFloat( src[0] ),
                Converter.LittleFloat( src[1] ), Converter.LittleFloat( src[2] ) );
        }

        public static Vector4 LittleVector4(float[] src, int offset )
        {
            return new Vector4( Converter.LittleFloat( src[offset + 0] ),
                Converter.LittleFloat( src[offset + 1] ),
                Converter.LittleFloat( src[offset + 2] ),
                Converter.LittleFloat( src[offset + 3] ) );
        }

        // SwapPic (qpic_t *pic)
        public static void SwapPic( WadPicHeader pic )
        {
            pic.width = LittleLong( pic.width );
            pic.height = LittleLong( pic.height );
        }

    }
}
