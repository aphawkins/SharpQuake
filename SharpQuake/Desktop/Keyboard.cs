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

// keys.h
// keys.c

// key up events are sent even if in console mode

namespace SharpQuake
{
    using System;
    using System.IO;
    using System.Text;
    using SharpQuake.Framework;
    using SharpQuake.Framework.IO;
    using SharpQuake.Framework.IO.Input;

    /// <summary>
    /// Key_functions
    /// </summary>
    public class Keyboard
    {
        public KeyDestination Destination { get; set; }

        public bool TeamMessage { get; set; }

        public char[][] Lines { get; } = new char[32][];

        public int EditLine { get; private set; }

        public string ChatBuffer => _ChatBuffer.ToString();

        public int LastPress { get; private set; }

        public string[] Bindings { get; } = new string[256];

        // Instances
        public Host Host
        {
            get;
            private set;
        }

        public int LinePos;

        public int KeyCount;

        // key_linepos
        private bool _ShiftDown; // = false;
        private int _HistoryLine; // history_line=0;
        private readonly bool[] _ConsoleKeys = new bool[256]; // consolekeys[256]	// if true, can't be rebound while in console
        private readonly bool[] _MenuBound = new bool[256]; // menubound[256]	// if true, can't be rebound while in menu
        private readonly int[] _KeyShift = new int[256]; // keyshift[256]		// key to map to if shift held down in console
        private readonly int[] _Repeats = new int[256]; // key_repeats[256]	// if > 1, it is autorepeating
        private readonly bool[] _KeyDown = new bool[256];

        private readonly StringBuilder _ChatBuffer = new(32); // chat_buffer

        public Keyboard(Host host)
        {
            Host = host;
        }

        // Key_Event (int key, qboolean down)
        //
        // Called by the system between frames for both key up and key down events
        // Should NOT be called during an interrupt!
        public void Event(int key, bool down)
        {
            _KeyDown[key] = down;

            if (!down)
            {
                _Repeats[key] = 0;
            }

            LastPress = key;
            KeyCount++;
            if (KeyCount <= 0)
            {
                return;     // just catching keys for Con_NotifyBox
            }

            // update auto-repeat status
            if (down)
            {
                _Repeats[key]++;
                if (key != KeysDef.K_BACKSPACE && key != KeysDef.K_PAUSE && key != KeysDef.K_PGUP && key != KeysDef.K_PGDN && _Repeats[key] > 1)
                {
                    return; // ignore most autorepeats
                }

                if (key >= 200 && string.IsNullOrEmpty(Bindings[key]))
                {
                    Host.Console.Print("{0} is unbound, hit F4 to set.\n", KeynumToString(key));
                }
            }

            if (key == KeysDef.K_SHIFT)
            {
                _ShiftDown = down;
            }

            //
            // handle escape specialy, so the user can never unbind it
            //
            if (key == KeysDef.K_ESCAPE)
            {
                if (!down)
                {
                    return;
                }

                switch (Destination)
                {
                    case KeyDestination.key_message:
                        KeyMessage(key);
                        break;

                    case KeyDestination.key_menu:
                        Rendering.UI.Menu.KeyDown(key);
                        break;

                    case KeyDestination.key_game:
                    case KeyDestination.key_console:
                        Host.Menu.ToggleMenu_f(null);
                        break;

                    default:
                        Utilities.Error("Bad key_dest");
                        break;
                }
                return;
            }

            //
            // key up events only generate commands if the game key binding is
            // a button command (leading + sign).  These will occur even in console mode,
            // to keep the character from continuing an action started before a console
            // switch.  Button commands include the keynum as a parameter, so multiple
            // downs can be matched with ups
            //
            if (!down)
            {
                var kb = Bindings[key];

                if (!string.IsNullOrEmpty(kb) && kb.StartsWith("+"))
                {
                    Host.Commands.Buffer.Append(string.Format("-{0} {1}\n", kb[1..], key));
                }

                if (_KeyShift[key] != key)
                {
                    kb = Bindings[_KeyShift[key]];
                    if (!string.IsNullOrEmpty(kb) && kb.StartsWith("+"))
                    {
                        Host.Commands.Buffer.Append(string.Format("-{0} {1}\n", kb[1..], key));
                    }
                }
                return;
            }

            //
            // during demo playback, most keys bring up the main menu
            //
            if (Host.Client.Cls.demoplayback && down && _ConsoleKeys[key] && Destination == KeyDestination.key_game)
            {
                Host.Menu.ToggleMenu_f(null);
                return;
            }

            //
            // if not a consolekey, send to the interpreter no matter what mode is
            //
            if ((Destination == KeyDestination.key_menu && _MenuBound[key]) ||
                (Destination == KeyDestination.key_console && !_ConsoleKeys[key]) ||
                (Destination == KeyDestination.key_game && (!Host.Console.ForcedUp || !_ConsoleKeys[key])))
            {
                var kb = Bindings[key];
                if (!string.IsNullOrEmpty(kb))
                {
                    if (kb.StartsWith("+"))
                    {
                        // button commands add keynum as a parm
                        Host.Commands.Buffer.Append(string.Format("{0} {1}\n", kb, key));
                    }
                    else
                    {
                        Host.Commands.Buffer.Append(kb);
                        Host.Commands.Buffer.Append("\n");
                    }
                }
                return;
            }

            if (!down)
            {
                return;     // other systems only care about key down events
            }

            if (_ShiftDown)
            {
                key = _KeyShift[key];
            }

            switch (Destination)
            {
                case KeyDestination.key_message:
                    KeyMessage(key);
                    break;

                case KeyDestination.key_menu:
                    Rendering.UI.Menu.KeyDown(key);
                    break;

                case KeyDestination.key_game:
                case KeyDestination.key_console:
                    KeyConsole(key);
                    break;

                default:
                    Utilities.Error("Bad key_dest");
                    break;
            }
        }

        // Key_Init (void);
        public void Initialise()
        {
            for (var i = 0; i < 32; i++)
            {
                Lines[i] = new char[KeysDef.MAXCMDLINE];
                Lines[i][0] = ']'; // key_lines[i][0] = ']'; key_lines[i][1] = 0;
            }

            LinePos = 1;

            //
            // init ascii characters in console mode
            //
            for (var i = 32; i < 128; i++)
            {
                _ConsoleKeys[i] = true;
            }

            _ConsoleKeys[KeysDef.K_ENTER] = true;
            _ConsoleKeys[KeysDef.K_TAB] = true;
            _ConsoleKeys[KeysDef.K_LEFTARROW] = true;
            _ConsoleKeys[KeysDef.K_RIGHTARROW] = true;
            _ConsoleKeys[KeysDef.K_UPARROW] = true;
            _ConsoleKeys[KeysDef.K_DOWNARROW] = true;
            _ConsoleKeys[KeysDef.K_BACKSPACE] = true;
            _ConsoleKeys[KeysDef.K_PGUP] = true;
            _ConsoleKeys[KeysDef.K_PGDN] = true;
            _ConsoleKeys[KeysDef.K_SHIFT] = true;
            _ConsoleKeys[KeysDef.K_MWHEELUP] = true;
            _ConsoleKeys[KeysDef.K_MWHEELDOWN] = true;
            _ConsoleKeys['`'] = false;
            _ConsoleKeys['~'] = false;

            for (var i = 0; i < 256; i++)
            {
                _KeyShift[i] = i;
            }

            for (int i = 'a'; i <= 'z'; i++)
            {
                _KeyShift[i] = i - 'a' + 'A';
            }

            _KeyShift['1'] = '!';
            _KeyShift['2'] = '@';
            _KeyShift['3'] = '#';
            _KeyShift['4'] = '$';
            _KeyShift['5'] = '%';
            _KeyShift['6'] = '^';
            _KeyShift['7'] = '&';
            _KeyShift['8'] = '*';
            _KeyShift['9'] = '(';
            _KeyShift['0'] = ')';
            _KeyShift['-'] = '_';
            _KeyShift['='] = '+';
            _KeyShift[','] = '<';
            _KeyShift['.'] = '>';
            _KeyShift['/'] = '?';
            _KeyShift[';'] = ':';
            _KeyShift['\''] = '"';
            _KeyShift['['] = '{';
            _KeyShift[']'] = '}';
            _KeyShift['`'] = '~';
            _KeyShift['\\'] = '|';

            _MenuBound[KeysDef.K_ESCAPE] = true;
            for (var i = 0; i < 12; i++)
            {
                _MenuBound[KeysDef.K_F1 + i] = true;
            }

            //
            // register our functions
            //
            Host.Commands.Add("bind", Bind_f);
            Host.Commands.Add("unbind", Unbind_f);
            Host.Commands.Add("unbindall", UnbindAll_f);
        }

        /// <summary>
        /// Key_WriteBindings
        /// </summary>
        public void WriteBindings(Stream dest)
        {
            var sb = new StringBuilder(4096);
            for (var i = 0; i < 256; i++)
            {
                if (!string.IsNullOrEmpty(Bindings[i]))
                {
                    sb.Append("bind \"");
                    sb.Append(KeynumToString(i));
                    sb.Append("\" \"");
                    sb.Append(Bindings[i]);
                    sb.AppendLine("\"");
                }
            }
            var buf = Encoding.ASCII.GetBytes(sb.ToString());
            dest.Write(buf, 0, buf.Length);
        }

        /// <summary>
        /// Key_SetBinding
        /// </summary>
        public void SetBinding(int keynum, string binding)
        {
            if (keynum != -1)
            {
                Bindings[keynum] = binding;
            }
        }

        // Key_ClearStates (void)
        public void ClearStates()
        {
            for (var i = 0; i < 256; i++)
            {
                _KeyDown[i] = false;
                _Repeats[i] = 0;
            }
        }

        // Key_KeynumToString
        //
        // Returns a string (either a single ascii char, or a K_* name) for the
        // given keynum.
        // FIXME: handle quote special (general escape sequence?)
        public static string KeynumToString(int keynum)
        {
            if (keynum == -1)
            {
                return "<KEY NOT FOUND>";
            }

            if (keynum is > 32 and < 127)
            {
                // printable ascii
                return ((char)keynum).ToString();
            }

            foreach (var kn in KeysDef.KeyNames)
            {
                if (kn.keynum == keynum)
                {
                    return kn.name;
                }
            }
            return "<UNKNOWN KEYNUM>";
        }

        // Key_StringToKeynum
        //
        // Returns a key number to be used to index keybindings[] by looking at
        // the given string.  Single ascii characters return themselves, while
        // the K_* names are matched up.
        private static int StringToKeynum(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return -1;
            }

            if (str.Length == 1)
            {
                return str[0];
            }

            foreach (var keyname in KeysDef.KeyNames)
            {
                if (Utilities.SameText(keyname.name, str))
                {
                    return keyname.keynum;
                }
            }
            return -1;
        }

        //Key_Unbind_f
        private void Unbind_f(CommandMessage msg)
        {
            var c = msg.Parameters != null ? msg.Parameters.Length : 0;

            if (c != 1)
            {
                Host.Console.Print("unbind <key> : remove commands from a key\n");
                return;
            }

            var b = StringToKeynum(msg.Parameters[0]);
            if (b == -1)
            {
                Host.Console.Print($"\"{msg.Parameters[0]}\" isn't a valid key\n");
                return;
            }

            SetBinding(b, null);
        }

        // Key_Unbindall_f
        private void UnbindAll_f(CommandMessage msg)
        {
            for (var i = 0; i < 256; i++)
            {
                if (!string.IsNullOrEmpty(Bindings[i]))
                {
                    SetBinding(i, null);
                }
            }
        }

        //Key_Bind_f
        private void Bind_f(CommandMessage msg)
        {
            var c = msg.Parameters != null ? msg.Parameters.Length : 0;
            if (c is not 1 and not 2)
            {
                Host.Console.Print("bind <key> [command] : attach a command to a key\n");
                return;
            }

            var b = StringToKeynum(msg.Parameters[0]);
            if (b == -1)
            {
                Host.Console.Print($"\"{msg.Parameters[0]}\" isn't a valid key\n");
                return;
            }

            if (c == 1)
            {
                if (!string.IsNullOrEmpty(Bindings[b]))// keybindings[b])
                {
                    Host.Console.Print($"\"{msg.Parameters[0]}\" = \"{Bindings[b]}\"\n");
                }
                else
                {
                    Host.Console.Print($"\"{msg.Parameters[0]}\" is not bound\n");
                }

                return;
            }

            // copy the rest of the command line
            // start out with a null string

            var args = string.Empty;

            if (msg.Parameters.Length > 1)
            {
                args = msg.ParametersFrom(1);
            }

            SetBinding(b, args);
        }

        // Key_Message (int key)
        private void KeyMessage(int key)
        {
            if (key == KeysDef.K_ENTER)
            {
                if (TeamMessage)
                {
                    Host.Commands.Buffer.Append("say_team \"");
                }
                else
                {
                    Host.Commands.Buffer.Append("say \"");
                }

                Host.Commands.Buffer.Append(_ChatBuffer.ToString());
                Host.Commands.Buffer.Append("\"\n");

                Destination = KeyDestination.key_game;
                _ChatBuffer.Length = 0;
                return;
            }

            if (key == KeysDef.K_ESCAPE)
            {
                Destination = KeyDestination.key_game;
                _ChatBuffer.Length = 0;
                return;
            }

            if (key is < 32 or > 127)
            {
                return; // non printable
            }

            if (key == KeysDef.K_BACKSPACE)
            {
                if (_ChatBuffer.Length > 0)
                {
                    _ChatBuffer.Length--;
                }
                return;
            }

            if (_ChatBuffer.Length == 31)
            {
                return; // all full
            }

            _ChatBuffer.Append((char)key);
        }

        /// <summary>
        /// Key_Console
        /// Interactive line editing and console scrollback
        /// </summary>
        private void KeyConsole(int key)
        {
            if (key == KeysDef.K_ENTER)
            {
                var line = new string(Lines[EditLine]).TrimEnd('\0', ' ');
                var cmd = line[1..];
                Host.Commands.Buffer.Append(cmd);	// skip the >
                Host.Commands.Buffer.Append("\n");
                Host.Console.Print("{0}\n", line);
                EditLine = (EditLine + 1) & 31;
                _HistoryLine = EditLine;
                Lines[EditLine][0] = ']';
                LinePos = 1;
                if (Host.Client.Cls.state == ClientActive.ca_disconnected)
                {
                    Host.Screen.UpdateScreen();    // force an update, because the command
                }
                // may take some time
                return;
            }

            if (key == KeysDef.K_TAB)
            {
                // command completion
                var txt = new string(Lines[EditLine], 1, KeysDef.MAXCMDLINE - 1).TrimEnd('\0', ' ');
                var cmds = Host.Commands.Complete(txt);
                var vars = Host.CVars.CompleteName(txt);
                string match = null;
                if (cmds != null)
                {
                    if (cmds.Length > 1 || vars != null)
                    {
                        Host.Console.Print("\nCommands:\n");
                        foreach (var s in cmds)
                        {
                            Host.Console.Print("  {0}\n", s);
                        }
                    }
                    else
                    {
                        match = cmds[0];
                    }
                }
                if (vars != null)
                {
                    if (vars.Length > 1 || cmds != null)
                    {
                        Host.Console.Print("\nVariables:\n");
                        foreach (var s in vars)
                        {
                            Host.Console.Print("  {0}\n", s);
                        }
                    }
                    else
                    {
                        match ??= vars[0];
                    }
                }
                if (!string.IsNullOrEmpty(match))
                {
                    var len = Math.Min(match.Length, KeysDef.MAXCMDLINE - 3);
                    for (var i = 0; i < len; i++)
                    {
                        Lines[EditLine][i + 1] = match[i];
                    }
                    LinePos = len + 1;
                    Lines[EditLine][LinePos] = ' ';
                    LinePos++;
                    Lines[EditLine][LinePos] = '\0';
                    return;
                }
            }

            if (key is KeysDef.K_BACKSPACE or KeysDef.K_LEFTARROW)
            {
                if (LinePos > 1)
                {
                    LinePos--;
                }

                return;
            }

            if (key == KeysDef.K_UPARROW)
            {
                do
                {
                    _HistoryLine = (_HistoryLine - 1) & 31;
                } while (_HistoryLine != EditLine && (Lines[_HistoryLine][1] == 0));
                if (_HistoryLine == EditLine)
                {
                    _HistoryLine = (EditLine + 1) & 31;
                }

                Array.Copy(Lines[_HistoryLine], Lines[EditLine], KeysDef.MAXCMDLINE);
                LinePos = 0;
                while (Lines[EditLine][LinePos] != '\0' && LinePos < KeysDef.MAXCMDLINE)
                {
                    LinePos++;
                }

                return;
            }

            if (key == KeysDef.K_DOWNARROW)
            {
                if (_HistoryLine == EditLine)
                {
                    return;
                }

                do
                {
                    _HistoryLine = (_HistoryLine + 1) & 31;
                }
                while (_HistoryLine != EditLine && (Lines[_HistoryLine][1] == '\0'));
                if (_HistoryLine == EditLine)
                {
                    Lines[EditLine][0] = ']';
                    LinePos = 1;
                }
                else
                {
                    Array.Copy(Lines[_HistoryLine], Lines[EditLine], KeysDef.MAXCMDLINE);
                    LinePos = 0;
                    while (Lines[EditLine][LinePos] != '\0' && LinePos < KeysDef.MAXCMDLINE)
                    {
                        LinePos++;
                    }
                }
                return;
            }

            if (key is KeysDef.K_PGUP or KeysDef.K_MWHEELUP)
            {
                Host.Console.BackScroll += 2;
                if (Host.Console.BackScroll > Host.Console.TotalLines - (Host.Screen.VidDef.height >> 3) - 1)
                {
                    Host.Console.BackScroll = Host.Console.TotalLines - (Host.Screen.VidDef.height >> 3) - 1;
                }

                return;
            }

            if (key is KeysDef.K_PGDN or KeysDef.K_MWHEELDOWN)
            {
                Host.Console.BackScroll -= 2;
                if (Host.Console.BackScroll < 0)
                {
                    Host.Console.BackScroll = 0;
                }

                return;
            }

            if (key == KeysDef.K_HOME)
            {
                Host.Console.BackScroll = Host.Console.TotalLines - (Host.Screen.VidDef.height >> 3) - 1;
                return;
            }

            if (key == KeysDef.K_END)
            {
                Host.Console.BackScroll = 0;
                return;
            }

            if (key is < 32 or > 127)
            {
                return; // non printable
            }

            if (LinePos < KeysDef.MAXCMDLINE - 1)
            {
                Lines[EditLine][LinePos] = (char)key;
                LinePos++;
                Lines[EditLine][LinePos] = '\0';
            }
        }
    }

    // keydest_t;
}
