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

namespace SharpQuake.Rendering.UI
{
    using System;
    using System.IO;
    using System.Text;
    using SharpQuake.Framework;
    using SharpQuake.Framework.IO;
    using SharpQuake.Framework.IO.Input;

    /// <summary>
    /// Con_functions
    /// </summary>
    public class Con
    {
        public bool IsInitialized { get; private set; }

        public bool ForcedUp { get; set; }

        public int NotifyLines { get; set; }

        public int TotalLines { get; private set; }

        public int BackScroll;
        private const string LOG_FILE_NAME = "qconsole.log";

        private const int CON_TEXTSIZE = 16384;
        private const int NUM_CON_TIMES = 4;

        private char[] _Text = new char[CON_TEXTSIZE]; // char		*con_text=0;
        private int _VisLines; // con_vislines

        // con_backscroll		// lines up from bottom to display
        private int _Current; // con_current		// where next message will be printed

        private int _X; // con_x		// offset in current line for next print
        private int _CR; // from Print()
        private readonly double[] _Times = new double[NUM_CON_TIMES]; // con_times	// realtime time the line was generated

        // for transparent notify lines
        private int _LineWidth; // con_linewidth

        private bool _DebugLog; // qboolean	con_debuglog;
        private readonly float _CursorSpeed = 4; // con_cursorspeed
        private FileStream _Log;

        public Con(Host host)
        {
            Host = host;
        }

        // Con_CheckResize (void)
        public void CheckResize()
        {
            var width = (Host.Screen.VidDef.width >> 3) - 2;
            if (width == _LineWidth)
            {
                return;
            }

            if (width < 1) // video hasn't been initialized yet
            {
                width = 38;
                _LineWidth = width; // con_linewidth = width;
                TotalLines = CON_TEXTSIZE / _LineWidth;
                Utilities.FillArray(_Text, ' '); // Q_memset (con_text, ' ', CON_TEXTSIZE);
            }
            else
            {
                var oldwidth = _LineWidth;
                _LineWidth = width;
                var oldtotallines = TotalLines;
                TotalLines = CON_TEXTSIZE / _LineWidth;
                var numlines = oldtotallines;

                if (TotalLines < numlines)
                {
                    numlines = TotalLines;
                }

                var numchars = oldwidth;

                if (_LineWidth < numchars)
                {
                    numchars = _LineWidth;
                }

                var tmp = _Text;
                _Text = new char[CON_TEXTSIZE];
                Utilities.FillArray(_Text, ' ');

                for (var i = 0; i < numlines; i++)
                {
                    for (var j = 0; j < numchars; j++)
                    {
                        _Text[((TotalLines - 1 - i) * _LineWidth) + j] = tmp[((_Current - i + oldtotallines) %
                                      oldtotallines * oldwidth) + j];
                    }
                }

                ClearNotify();
            }

            BackScroll = 0;
            _Current = TotalLines - 1;
        }

        // Instances
        private Host Host
        {
            get;
            set;
        }

        // Con_Init (void)
        public void Initialise()
        {
            _DebugLog = CommandLine.CheckParm("-condebug") > 0;

            if (_DebugLog)
            {
                var path = Path.Combine(FileSystem.GameDir, LOG_FILE_NAME);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                _Log = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            }

            _LineWidth = -1;
            CheckResize();

            Print("Console initialized.\n");

            //
            // register our commands
            //
            if (Host.Cvars.NotifyTime == null)
            {
                Host.Cvars.NotifyTime = Host.CVars.Add("con_notifytime", 3);
            }

            Host.Commands.Add("toggleconsole", ToggleConsole_f);
            Host.Commands.Add("messagemode", MessageMode_f);
            Host.Commands.Add("messagemode2", MessageMode2_f);
            Host.Commands.Add("clear", Clear_f);

            ConsoleWrapper.OnPrint += (txt) => Print(txt);

            ConsoleWrapper.OnPrint2 += (fmt, args) => Print(fmt, args);

            ConsoleWrapper.OnDPrint += (fmt, args) => DPrint(fmt, args);

            IsInitialized = true;
        }

        // Con_DrawConsole
        //
        // Draws the console with the solid background
        // The typing input line at the bottom should only be drawn if typing is allowed
        public void Draw(int lines, bool drawinput)
        {
            if (lines <= 0)
            {
                return;
            }

            // draw the background
            Host.DrawingContext.DrawConsoleBackground(lines);

            // draw the text
            _VisLines = lines;

            var rows = (lines - 16) >> 3;     // rows of text to draw
            var y = lines - 16 - (rows << 3); // may start slightly negative

            for (var i = _Current - rows + 1; i <= _Current; i++, y += 8)
            {
                var j = i - BackScroll;
                if (j < 0)
                {
                    j = 0;
                }

                var offset = j % TotalLines * _LineWidth;

                for (var x = 0; x < _LineWidth; x++)
                {
                    Host.DrawingContext.DrawCharacter((x + 1) << 3, y, _Text[offset + x]);
                }
            }

            // draw the input prompt, user text, and cursor if desired
            if (drawinput)
            {
                DrawInput();
            }
        }

        /// <summary>
        /// Con_Printf
        /// </summary>
        public void Print(string fmt, params object[] args)
        {
            var msg = args.Length > 0 ? string.Format(fmt, args) : fmt;

            Console.WriteLine(msg); // Debug stuff

            // log all messages to file
            if (_DebugLog)
            {
                DebugLog(msg);
            }

            if (!IsInitialized)
            {
                return;
            }

            if (Host.Client.Cls.state == ClientActive.ca_dedicated)
            {
                return;     // no graphics mode
            }

            // write it to the scrollable buffer
            Print(msg);

            // update the screen if the console is displayed
            if (Host.Client.Cls.signon != ClientDef.SIGNONS && !Host.Screen.IsDisabledForLoading)
            {
                Host.Screen.UpdateScreen();
            }
        }

        public void Shutdown()
        {
            if (_Log != null)
            {
                _Log.Flush();
                _Log.Dispose();
                _Log = null;
            }
        }

        //
        // Con_DPrintf
        //
        // A Con_Printf that only shows up if the "developer" cvar is set
        public void DPrint(string fmt, params object[] args)
        {
            // don't confuse non-developers with techie stuff...
            if (Host != null && Host.IsDeveloper)
            {
                Print(fmt, args);
            }
        }

        // Con_SafePrintf (char *fmt, ...)
        //
        // Okay to call even when the screen can't be updated
        public void SafePrint(string fmt, params object[] args)
        {
            var temp = Host.Screen.IsDisabledForLoading;
            Host.Screen.IsDisabledForLoading = true;
            Print(fmt, args);
            Host.Screen.IsDisabledForLoading = temp;
        }

        /// <summary>
        /// Con_DrawNotify
        /// </summary>
        public void DrawNotify()
        {
            var v = 0;
            for (var i = _Current - NUM_CON_TIMES + 1; i <= _Current; i++)
            {
                if (i < 0)
                {
                    continue;
                }

                var time = _Times[i % NUM_CON_TIMES];
                if (time == 0)
                {
                    continue;
                }

                time = Host.RealTime - time;
                if (time > Host.Cvars.NotifyTime.Get<int>())
                {
                    continue;
                }

                var textOffset = i % TotalLines * _LineWidth;

                Host.Screen.ClearNotify = 0;
                Host.Screen.CopyTop = true;

                for (var x = 0; x < _LineWidth; x++)
                {
                    Host.DrawingContext.DrawCharacter((x + 1) << 3, v, _Text[textOffset + x]);
                }

                v += 8;
            }

            if (Host.Keyboard.Destination == KeyDestination.key_message)
            {
                Host.Screen.ClearNotify = 0;
                Host.Screen.CopyTop = true;

                var x = 0;

                Host.DrawingContext.DrawString(8, v, "say:");
                var chat = Host.Keyboard.ChatBuffer;
                for (; x < chat.Length; x++)
                {
                    Host.DrawingContext.DrawCharacter((x + 5) << 3, v, chat[x]);
                }
                Host.DrawingContext.DrawCharacter((x + 5) << 3, v, 10 + ((int)(Host.RealTime * _CursorSpeed) & 1));
                v += 8;
            }

            if (v > NotifyLines)
            {
                NotifyLines = v;
            }
        }

        // Con_ClearNotify (void)
        public void ClearNotify()
        {
            for (var i = 0; i < NUM_CON_TIMES; i++)
            {
                _Times[i] = 0;
            }
        }

        /// <summary>
        /// Con_ToggleConsole_f
        /// </summary>
        public void ToggleConsole_f(CommandMessage msg)
        {
            if (Host.Keyboard.Destination == KeyDestination.key_console)
            {
                if (Host.Client.Cls.state == ClientActive.ca_connected)
                {
                    Host.Keyboard.Destination = KeyDestination.key_game;
                    Host.Keyboard.Lines[Host.Keyboard.EditLine][1] = '\0';  // clear any typing
                    Host.Keyboard.LinePos = 1;
                }
                else
                {
                    MenuBase.MainMenu.Show(Host);
                }
            }
            else
            {
                Host.Keyboard.Destination = KeyDestination.key_console;
            }

            Host.Screen.EndLoadingPlaque();
            Array.Clear(_Times, 0, _Times.Length);
        }

        /// <summary>
        /// Con_DebugLog
        /// </summary>
        private void DebugLog(string msg)
        {
            if (_Log != null)
            {
                var tmp = Encoding.UTF8.GetBytes(msg);
                _Log.Write(tmp, 0, tmp.Length);
            }
        }

        // Con_Print (char *txt)
        //
        // Handles cursor positioning, line wrapping, etc
        // All console printing must go through this in order to be logged to disk
        // If no console is visible, the notify window will pop up.
        private void Print(string txt)
        {
            if (string.IsNullOrEmpty(txt))
            {
                return;
            }

            int mask, offset = 0;

            BackScroll = 0;

            if (txt.StartsWith(((char)1).ToString()))// [0] == 1)
            {
                mask = 128; // go to colored text
                Host.Sound.LocalSound("misc/talk.wav"); // play talk wav
                offset++;
            }
            else if (txt.StartsWith(((char)2).ToString())) //txt[0] == 2)
            {
                mask = 128; // go to colored text
                offset++;
            }
            else
            {
                mask = 0;
            }

            while (offset < txt.Length)
            {
                var c = txt[offset];

                int l;
                // count word length
                for (l = 0; l < _LineWidth && offset + l < txt.Length; l++)
                {
                    if (txt[offset + l] <= ' ')
                    {
                        break;
                    }
                }

                // word wrap
                if (l != _LineWidth && (_X + l > _LineWidth))
                {
                    _X = 0;
                }

                offset++;

                if (_CR != 0)
                {
                    _Current--;
                    _CR = 0;
                }

                if (_X == 0)
                {
                    LineFeed();
                    // mark time for transparent overlay
                    if (_Current >= 0)
                    {
                        _Times[_Current % NUM_CON_TIMES] = Host.RealTime; // realtime
                    }
                }

                switch (c)
                {
                    case '\n':
                        _X = 0;
                        break;

                    case '\r':
                        _X = 0;
                        _CR = 1;
                        break;

                    default:    // display character and advance
                        var y = _Current % TotalLines;
                        _Text[(y * _LineWidth) + _X] = (char)(c | mask);
                        _X++;
                        if (_X >= _LineWidth)
                        {
                            _X = 0;
                        }

                        break;
                }
            }
        }

        /// <summary>
        /// Con_Clear_f
        /// </summary>
        private void Clear_f(CommandMessage msg)
        {
            Utilities.FillArray(_Text, ' ');
        }

        // Con_MessageMode_f
        private void MessageMode_f(CommandMessage msg)
        {
            Host.Keyboard.Destination = KeyDestination.key_message;
            Host.Keyboard.TeamMessage = false;
        }

        //Con_MessageMode2_f
        private void MessageMode2_f(CommandMessage msg)
        {
            Host.Keyboard.Destination = KeyDestination.key_message;
            Host.Keyboard.TeamMessage = true;
        }

        // Con_Linefeed
        private void LineFeed()
        {
            _X = 0;
            _Current++;

            for (var i = 0; i < _LineWidth; i++)
            {
                _Text[(_Current % TotalLines * _LineWidth) + i] = ' ';
            }
        }

        // Con_DrawInput
        //
        // The input line scrolls horizontally if typing goes beyond the right edge
        private void DrawInput()
        {
            if (Host.Keyboard.Destination != KeyDestination.key_console && !ForcedUp)
            {
                return;     // don't draw anything
            }

            // add the cursor frame
            Host.Keyboard.Lines[Host.Keyboard.EditLine][Host.Keyboard.LinePos] = (char)(10 + ((int)(Host.RealTime * _CursorSpeed) & 1));

            // fill out remainder with spaces
            for (var i = Host.Keyboard.LinePos + 1; i < _LineWidth; i++)
            {
                Host.Keyboard.Lines[Host.Keyboard.EditLine][i] = ' ';
            }

            //	prestep if horizontally scrolling
            var offset = 0;
            if (Host.Keyboard.LinePos >= _LineWidth)
            {
                offset = 1 + Host.Keyboard.LinePos - _LineWidth;
            }
            //text += 1 + key_linepos - con_linewidth;

            // draw it
            for (var i = 0; i < _LineWidth; i++)
            {
                Host.DrawingContext.DrawCharacter((i + 1) << 3, _VisLines - 16, Host.Keyboard.Lines[Host.Keyboard.EditLine][offset + i]);
            }

            // remove cursor
            Host.Keyboard.Lines[Host.Keyboard.EditLine][Host.Keyboard.LinePos] = '\0';
        }
    }
}
