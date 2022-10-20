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

namespace SharpQuake.Framework
{
    public static class Tokeniser
    {
        public static string Token { get; private set; }

        /// <summary>
        /// COM_Parse
        /// Parse a token out of a string
        /// </summary>
        public static string Parse(string data )
        {
            Token = string.Empty;

            if (string.IsNullOrEmpty( data ) )
                return null;

            // skip whitespace
            var i = 0;

            while ( i < data.Length )
            {
                while ( i < data.Length )
                {
                    if ( data[i] > ' ' )
                        break;

                    i++;
                }

                if ( i >= data.Length )
                    return null;

                // skip // comments
                if ( ( data[i] == '/' ) && ( i + 1 < data.Length ) && ( data[i + 1] == '/' ) )
                {
                    while ( i < data.Length && data[i] != '\n' )
                        i++;
                }
                else
                    break;
            }

            if ( i >= data.Length )
                return null;

            var i0 = i;

            // handle quoted strings specially
            if ( data[i] == '\"' )
            {
                i++;
                i0 = i;

                while ( i < data.Length && data[i] != '\"' )
                    i++;

                if ( i == data.Length )
                {
                    Token = data.Substring( i0, i - i0 );
                    return null;
                }
                else
                {
                    Token = data.Substring( i0, i - i0 );
                    return ( i + 1 < data.Length ? data.Substring( i + 1 ) : null );
                }
            }

            // parse single characters
            var c = data[i];

            if ( c == '{' || c == '}' || c == ')' || c == '(' || c == '\'' || c == ':' )
            {
                Token = data.Substring( i, 1 );
                return ( i + 1 < data.Length ? data.Substring( i + 1 ) : null );
            }

            // parse a regular word
            while ( i < data.Length )
            {
                c = data[i];

                if ( c <= 32 || c == '{' || c == '}' || c == ')' || c == '(' || c == '\'' || c == ':' )
                {
                    i--;
                    break;
                }

                i++;
            }

            if ( i == data.Length )
            {
                Token = data.Substring( i0, i - i0 );
                return null;
            }

            Token = data.Substring( i0, i - i0 + 1 );
            return ( i + 1 < data.Length ? data.Substring( i + 1 ) : null );
        }

    }
}
