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
using System.Collections.Generic;
using OpenTK.Audio;
using OpenTK.Audio.OpenAL;
using SharpQuake.Framework.IO.Sound;

namespace SharpQuake
{
    internal class OpenALController : ISoundController
    {
        private const int AL_BUFFER_COUNT = 24;
        private const int BUFFER_SIZE = 0x10000;
        private AudioContext _Context;
        private int _Source;
        private int[] _Buffers;
        private int[] _BufferBytes;
        private ALFormat _BufferFormat;
        private int _SamplesSent;
        private Queue<int> _FreeBuffers;

        private void FreeContext()
        {
            if( _Source != 0 )
            {
                AL.SourceStop( _Source );
                AL.DeleteSource( _Source );
                _Source = 0;
            }
            if( _Buffers != null )
            {
                AL.DeleteBuffers( _Buffers );
                _Buffers = null;
            }
            if( _Context != null )
            {
                _Context.Dispose();
                _Context = null;
            }
        }

        #region ISoundController Members

        public bool IsInitialised { get; private set; }

        public Host Host
        {
            get;
            private set;
        }

        public void Initialise(object host )
        {
            Host = ( Host ) host;

            FreeContext();

            _Context = new AudioContext();
            _Source = AL.GenSource();
            _Buffers = new int[AL_BUFFER_COUNT];
            _BufferBytes = new int[AL_BUFFER_COUNT];
            _FreeBuffers = new Queue<int>( AL_BUFFER_COUNT );

            for( var i = 0; i < _Buffers.Length; i++ )
            {
                _Buffers[i] = AL.GenBuffer();
                _FreeBuffers.Enqueue( _Buffers[i] );
            }

            AL.SourcePlay( _Source );
            AL.Source( _Source, ALSourceb.Looping, false );

            Host.Sound.shm.channels = 2;
            Host.Sound.shm.samplebits = 16;
            Host.Sound.shm.speed = 11025;
            Host.Sound.shm.buffer = new byte[BUFFER_SIZE];
            Host.Sound.shm.soundalive = true;
            Host.Sound.shm.splitbuffer = false;
            Host.Sound.shm.samples = Host.Sound.shm.buffer.Length / ( Host.Sound.shm.samplebits / 8 );
            Host.Sound.shm.samplepos = 0;
            Host.Sound.shm.submission_chunk = 1;

            if( Host.Sound.shm.samplebits == 8 )
            {
                if( Host.Sound.shm.channels == 2 )
                    _BufferFormat = ALFormat.Stereo8;
                else
                    _BufferFormat = ALFormat.Mono8;
            }
            else
            {
                if( Host.Sound.shm.channels == 2 )
                    _BufferFormat = ALFormat.Stereo16;
                else
                    _BufferFormat = ALFormat.Mono16;
            }

            IsInitialised = true;
        }

        public void Shutdown()
        {
            FreeContext();
            IsInitialised = false;
        }

        public void ClearBuffer()
        {
            AL.SourceStop( _Source );
        }

        public byte[] LockBuffer()
        {
            return Host.Sound.shm.buffer;
        }

        public void UnlockBuffer(int bytes )
        {
            int processed;
            AL.GetSource( _Source, ALGetSourcei.BuffersProcessed, out processed );
            if( processed > 0 )
            {
                var bufs = AL.SourceUnqueueBuffers( _Source, processed );
                foreach( var buffer in bufs )
                {
                    if( buffer == 0 )
                        continue;

                    var idx = Array.IndexOf( _Buffers, buffer );
                    if( idx != -1 )
                    {
                        _SamplesSent += _BufferBytes[idx] >> ( ( Host.Sound.shm.samplebits / 8 ) - 1 );
                        _SamplesSent &=  Host.Sound.shm.samples - 1 ;
                        _BufferBytes[idx] = 0;
                    }
                    if( !_FreeBuffers.Contains( buffer ) )
                        _FreeBuffers.Enqueue( buffer );
                }
            }

            if( _FreeBuffers.Count == 0 )
            {
                Host.Console.DPrint( "UnlockBuffer: No free buffers!\n" );
                return;
            }

            var buf = _FreeBuffers.Dequeue();
            if( buf != 0 )
            {
                AL.BufferData( buf, _BufferFormat, Host.Sound.shm.buffer, bytes, Host.Sound.shm.speed );
                AL.SourceQueueBuffer( _Source, buf );

                var idx = Array.IndexOf( _Buffers, buf );
                if( idx != -1 )
                {
                    _BufferBytes[idx] = bytes;
                }

                int state;
                AL.GetSource( _Source, ALGetSourcei.SourceState, out state );
                if( (ALSourceState)state != ALSourceState.Playing )
                {
                    AL.SourcePlay( _Source );
                    Host.Console.DPrint( "Sound resumed from {0}, free {1} of {2} buffers\n",
                        ( (ALSourceState)state ).ToString( "F" ), _FreeBuffers.Count, _Buffers.Length );
                }
            }
        }

        public int GetPosition()
        {
            int state, offset = 0;
            AL.GetSource( _Source, ALGetSourcei.SourceState, out state );
            if( (ALSourceState)state != ALSourceState.Playing )
            {
                for( var i = 0; i < _BufferBytes.Length; i++ )
                {
                    _SamplesSent += _BufferBytes[i] >> ( ( Host.Sound.shm.samplebits / 8 ) - 1 );
                    _BufferBytes[i] = 0;
                }
                _SamplesSent &=  Host.Sound.shm.samples - 1 ;
            }
            else
            {
                AL.GetSource( _Source, ALGetSourcei.SampleOffset, out offset );
            }
            return ( _SamplesSent + offset ) & ( Host.Sound.shm.samples - 1 );
        }

        #endregion ISoundController Members
    }
}
