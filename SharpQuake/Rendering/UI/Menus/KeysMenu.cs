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
using SharpQuake.Framework;

namespace SharpQuake.Rendering.UI
{
    public class KeysMenu : MenuBase
    {
        private static readonly string[][] _BindNames = new string[][]
        {
            new string[] {"+attack",        "attack"},
            new string[] {"impulse 10",     "change weapon"},
            new string[] {"+jump",          "jump / swim up"},
            new string[] {"+forward",       "walk forward"},
            new string[] {"+back",          "backpedal"},
            new string[] {"+left",          "turn left"},
            new string[] {"+right",         "turn right"},
            new string[] {"+speed",         "run"},
            new string[] {"+moveleft",      "step left"},
            new string[] {"+moveright",     "step right"},
            new string[] {"+strafe",        "sidestep"},
            new string[] {"+lookup",        "look up"},
            new string[] {"+lookdown",      "look down"},
            new string[] {"centerview",     "center view"},
            new string[] {"+mlook",         "mouse look"},
            new string[] {"+klook",         "keyboard look"},
            new string[] {"+moveup",        "swim up"},
            new string[] {"+movedown",      "swim down"}
        };

        //const inte	NUMCOMMANDS	(sizeof(bindnames)/sizeof(bindnames[0]))

        private bool _BindGrab; // bind_grab

        public override void Show(Host host)
        {
            base.Show(host);
        }

        public override void KeyEvent(int key)
        {
            if (_BindGrab)
            {
                // defining a key
                Host.Sound.LocalSound("misc/menu1.wav");
                if (key == KeysDef.K_ESCAPE)
                {
                    _BindGrab = false;
                }
                else if (key != '`')
                {
                    var cmd = string.Format("bind \"{0}\" \"{1}\"\n", Host.Keyboard.KeynumToString(key), _BindNames[_Cursor][0]);
                    Host.Commands.Buffer.Insert(cmd);
                }

                _BindGrab = false;
                return;
            }

            switch (key)
            {
                case KeysDef.K_ESCAPE:
                    OptionsMenu.Show(Host);
                    break;

                case KeysDef.K_LEFTARROW:
                case KeysDef.K_UPARROW:
                    Host.Sound.LocalSound("misc/menu1.wav");
                    _Cursor--;
                    if (_Cursor < 0)
                        _Cursor = _BindNames.Length - 1;
                    break;

                case KeysDef.K_DOWNARROW:
                case KeysDef.K_RIGHTARROW:
                    Host.Sound.LocalSound("misc/menu1.wav");
                    _Cursor++;
                    if (_Cursor >= _BindNames.Length)
                        _Cursor = 0;
                    break;

                case KeysDef.K_ENTER:		// go into bind mode
                    var keys = new int[2];
                    FindKeysForCommand(_BindNames[_Cursor][0], keys);
                    Host.Sound.LocalSound("misc/menu2.wav");
                    if (keys[1] != -1)
                        UnbindCommand(_BindNames[_Cursor][0]);
                    _BindGrab = true;
                    break;

                case KeysDef.K_BACKSPACE:		// delete bindings
                case KeysDef.K_DEL:				// delete bindings
                    Host.Sound.LocalSound("misc/menu2.wav");
                    UnbindCommand(_BindNames[_Cursor][0]);
                    break;
            }
        }

        public override void Draw()
        {
            var p = Host.DrawingContext.CachePic("gfx/ttl_cstm.lmp", "GL_NEAREST");
            Host.Menu.DrawPic((320 - p.Width) / 2, 4, p);

            if (_BindGrab)
                Host.Menu.Print(12, 32, "Press a key or button for this action");
            else
                Host.Menu.Print(18, 32, "Enter to change, backspace to clear");

            // search for known bindings
            var keys = new int[2];

            for (var i = 0; i < _BindNames.Length; i++)
            {
                var y = 48 + (8 * i);

                Host.Menu.Print(16, y, _BindNames[i][1]);

                FindKeysForCommand(_BindNames[i][0], keys);

                if (keys[0] == -1)
                {
                    Host.Menu.Print(140, y, "???");
                }
                else
                {
                    var name = Host.Keyboard.KeynumToString(keys[0]);
                    Host.Menu.Print(140, y, name);
                    var x = name.Length * 8;
                    if (keys[1] != -1)
                    {
                        Host.Menu.Print(140 + x + 8, y, "or");
                        Host.Menu.Print(140 + x + 32, y, Host.Keyboard.KeynumToString(keys[1]));
                    }
                }
            }

            if (_BindGrab)
                Host.Menu.DrawCharacter(130, 48 + (_Cursor * 8), '=');
            else
                Host.Menu.DrawCharacter(130, 48 + (_Cursor * 8), 12 + ((int)(Host.RealTime * 4) & 1));
        }

        /// <summary>
        /// M_FindKeysForCommand
        /// </summary>
        private void FindKeysForCommand(string command, int[] twokeys)
        {
            twokeys[0] = twokeys[1] = -1;
            var len = command.Length;
            var count = 0;

            for (var j = 0; j < 256; j++)
            {
                var b = Host.Keyboard.Bindings[j];
                if (string.IsNullOrEmpty(b))
                    continue;

                if (string.Compare(b, 0, command, 0, len) == 0)
                {
                    twokeys[count] = j;
                    count++;
                    if (count == 2)
                        break;
                }
            }
        }

        /// <summary>
        /// M_UnbindCommand
        /// </summary>
        private void UnbindCommand(string command)
        {
            var len = command.Length;

            for (var j = 0; j < 256; j++)
            {
                var b = Host.Keyboard.Bindings[j];
                if (string.IsNullOrEmpty(b))
                    continue;

                if (string.Compare(b, 0, command, 0, len) == 0)
                    Host.Keyboard.SetBinding(j, string.Empty);
            }
        }
    }

}
