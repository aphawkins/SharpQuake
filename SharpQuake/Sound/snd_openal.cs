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
    using OpenTK.Audio;
    using OpenTK.Audio.OpenAL;
    using SharpQuake.Framework.IO.Sound;

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
            if (_Source != 0)
            {
                AL.SourceStop(_Source);
                AL.DeleteSource(_Source);
                _Source = 0;
            }
            if (_Buffers != null)
            {
                AL.DeleteBuffers(_Buffers);
                _Buffers = null;
            }
            if (_Context != null)
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

        public void Initialise(object host)
        {
            Host = (Host)host;

            FreeContext();

            _Context = new AudioContext();
            _Source = AL.GenSource();
            _Buffers = new int[AL_BUFFER_COUNT];
            _BufferBytes = new int[AL_BUFFER_COUNT];
            _FreeBuffers = new Queue<int>(AL_BUFFER_COUNT);

            for (var i = 0; i < _Buffers.Length; i++)
            {
                _Buffers[i] = AL.GenBuffer();
                _FreeBuffers.Enqueue(_Buffers[i]);
            }

            AL.SourcePlay(_Source);
            AL.Source(_Source, ALSourceb.Looping, false);

            Host.Sound.Shm.channels = 2;
            Host.Sound.Shm.samplebits = 16;
            Host.Sound.Shm.speed = 11025;
            Host.Sound.Shm.buffer = new byte[BUFFER_SIZE];
            Host.Sound.Shm.soundalive = true;
            Host.Sound.Shm.splitbuffer = false;
            Host.Sound.Shm.samples = Host.Sound.Shm.buffer.Length / (Host.Sound.Shm.samplebits / 8);
            Host.Sound.Shm.samplepos = 0;
            Host.Sound.Shm.submission_chunk = 1;

            _BufferFormat = Host.Sound.Shm.samplebits == 8
                ? Host.Sound.Shm.channels == 2 ? ALFormat.Stereo8 : ALFormat.Mono8
                : Host.Sound.Shm.channels == 2 ? ALFormat.Stereo16 : ALFormat.Mono16;

            IsInitialised = true;
        }

        public void Shutdown()
        {
            FreeContext();
            IsInitialised = false;
        }

        public void ClearBuffer()
        {
            AL.SourceStop(_Source);
        }

        public byte[] LockBuffer()
        {
            return Host.Sound.Shm.buffer;
        }

        public void UnlockBuffer(int bytes)
        {
            AL.GetSource(_Source, ALGetSourcei.BuffersProcessed, out int processed);
            if (processed > 0)
            {
                var bufs = AL.SourceUnqueueBuffers(_Source, processed);
                foreach (var buffer in bufs)
                {
                    if (buffer == 0)
                    {
                        continue;
                    }

                    var idx = Array.IndexOf(_Buffers, buffer);
                    if (idx != -1)
                    {
                        _SamplesSent += _BufferBytes[idx] >> ((Host.Sound.Shm.samplebits / 8) - 1);
                        _SamplesSent &= Host.Sound.Shm.samples - 1;
                        _BufferBytes[idx] = 0;
                    }
                    if (!_FreeBuffers.Contains(buffer))
                    {
                        _FreeBuffers.Enqueue(buffer);
                    }
                }
            }

            if (_FreeBuffers.Count == 0)
            {
                Host.Console.DPrint("UnlockBuffer: No free buffers!\n");
                return;
            }

            var buf = _FreeBuffers.Dequeue();
            if (buf != 0)
            {
                AL.BufferData(buf, _BufferFormat, Host.Sound.Shm.buffer, bytes, Host.Sound.Shm.speed);
                AL.SourceQueueBuffer(_Source, buf);

                var idx = Array.IndexOf(_Buffers, buf);
                if (idx != -1)
                {
                    _BufferBytes[idx] = bytes;
                }

                AL.GetSource(_Source, ALGetSourcei.SourceState, out int state);
                if ((ALSourceState)state != ALSourceState.Playing)
                {
                    AL.SourcePlay(_Source);
                    Host.Console.DPrint("Sound resumed from {0}, free {1} of {2} buffers\n",
                        ((ALSourceState)state).ToString("F"), _FreeBuffers.Count, _Buffers.Length);
                }
            }
        }

        public int GetPosition()
        {
            int offset = 0;
            AL.GetSource(_Source, ALGetSourcei.SourceState, out int state);
            if ((ALSourceState)state != ALSourceState.Playing)
            {
                for (var i = 0; i < _BufferBytes.Length; i++)
                {
                    _SamplesSent += _BufferBytes[i] >> ((Host.Sound.Shm.samplebits / 8) - 1);
                    _BufferBytes[i] = 0;
                }
                _SamplesSent &= Host.Sound.Shm.samples - 1;
            }
            else
            {
                AL.GetSource(_Source, ALGetSourcei.SampleOffset, out offset);
            }
            return (_SamplesSent + offset) & (Host.Sound.Shm.samples - 1);
        }

        #endregion ISoundController Members
    }
}
