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

namespace SharpQuake.Rendering.UI
{
    using System;
    using SharpQuake.Framework;

    /// <summary>
    /// M_Menu_LanConfig_functions
    /// </summary>
    public class LanConfigMenu : MenuBase
    {
        public bool JoiningGame
        {
            get
            {
                return MultiPlayerMenu.Cursor == 0;
            }
        }

        public bool StartingGame
        {
            get
            {
                return MultiPlayerMenu.Cursor == 1;
            }
        }

        private const int NUM_LANCONFIG_CMDS = 3;

        private static readonly int[] _CursorTable = new int[] { 72, 92, 124 };

        private int _Port;
        private string _PortName;
        private string _JoinName;

        public override void Show(Host host)
        {
            base.Show(host);

            if (_Cursor == -1)
            {
                if (JoiningGame)
                    _Cursor = 2;
                else
                    _Cursor = 1;
            }
            if (StartingGame && _Cursor == 2)
                _Cursor = 1;
            _Port = Host.Network.DefaultHostPort;
            _PortName = _Port.ToString();

            Host.Menu.ReturnOnError = false;
            Host.Menu.ReturnReason = string.Empty;
        }

        public override void KeyEvent(int key)
        {
            switch (key)
            {
                case KeysDef.K_ESCAPE:
                    MultiPlayerMenu.Show(Host);
                    break;

                case KeysDef.K_UPARROW:
                    Host.Sound.LocalSound("misc/menu1.wav");
                    _Cursor--;
                    if (_Cursor < 0)
                        _Cursor = NUM_LANCONFIG_CMDS - 1;
                    break;

                case KeysDef.K_DOWNARROW:
                    Host.Sound.LocalSound("misc/menu1.wav");
                    _Cursor++;
                    if (_Cursor >= NUM_LANCONFIG_CMDS)
                        _Cursor = 0;
                    break;

                case KeysDef.K_ENTER:
                    if (_Cursor == 0)
                        break;

                    Host.Menu.EnterSound = true;
                    Host.Network.HostPort = _Port;

                    if (_Cursor == 1)
                    {
                        if (StartingGame)
                        {
                            GameOptionsMenu.Show(Host);
                        }
                        else
                        {
                            SearchMenu.Show(Host);
                        }
                        break;
                    }

                    if (_Cursor == 2)
                    {
                        Host.Menu.ReturnMenu = this;
                        Host.Menu.ReturnOnError = true;
                        CurrentMenu.Hide();
                        Host.Commands.Buffer.Append(string.Format("connect \"{0}\"\n", _JoinName));
                        break;
                    }
                    break;

                case KeysDef.K_BACKSPACE:
                    if (_Cursor == 0)
                    {
                        if (!string.IsNullOrEmpty(_PortName))
                            _PortName = _PortName[..^1];
                    }

                    if (_Cursor == 2)
                    {
                        if (!string.IsNullOrEmpty(_JoinName))
                            _JoinName = _JoinName[..^1];
                    }
                    break;

                default:
                    if (key is < 32 or > 127)
                        break;

                    if (_Cursor == 2)
                    {
                        if (_JoinName.Length < 21)
                            _JoinName += (char)key;
                    }

                    if (key is < '0' or > '9')
                        break;

                    if (_Cursor == 0)
                    {
                        if (_PortName.Length < 5)
                            _PortName += (char)key;
                    }
                    break;
            }

            if (StartingGame && _Cursor == 2)
                if (key == KeysDef.K_UPARROW)
                    _Cursor = 1;
                else
                    _Cursor = 0;

            var k = MathLib.atoi(_PortName);
            if (k > 65535)
                k = _Port;
            else
                _Port = k;
            _PortName = _Port.ToString();
        }

        public override void Draw()
        {
            Host.Menu.DrawTransPic(16, 4, Host.DrawingContext.CachePic("gfx/qplaque.lmp", "GL_NEAREST"));
            var p = Host.DrawingContext.CachePic("gfx/p_multi.lmp", "GL_NEAREST");
            var basex = (320 - p.Width) / 2;
            Host.Menu.DrawPic(basex, 4, p);

            string startJoin;
            if (StartingGame)
                startJoin = "New Game - TCP/IP";
            else
                startJoin = "Join Game - TCP/IP";

            Host.Menu.Print(basex, 32, startJoin);
            basex += 8;

            Host.Menu.Print(basex, 52, "Address:");
            Host.Menu.Print(basex + (9 * 8), 52, Host.Network.MyTcpIpAddress);

            Host.Menu.Print(basex, _CursorTable[0], "Port");
            Host.Menu.DrawTextBox(basex + (8 * 8), _CursorTable[0] - 8, 6, 1);
            Host.Menu.Print(basex + (9 * 8), _CursorTable[0], _PortName);

            if (JoiningGame)
            {
                Host.Menu.Print(basex, _CursorTable[1], "Search for local games...");
                Host.Menu.Print(basex, 108, "Join game at:");
                Host.Menu.DrawTextBox(basex + 8, _CursorTable[2] - 8, 22, 1);
                Host.Menu.Print(basex + 16, _CursorTable[2], _JoinName);
            }
            else
            {
                Host.Menu.DrawTextBox(basex, _CursorTable[1] - 8, 2, 1);
                Host.Menu.Print(basex + 8, _CursorTable[1], "OK");
            }

            Host.Menu.DrawCharacter(basex - 8, _CursorTable[_Cursor], 12 + ((int)(Host.RealTime * 4) & 1));

            if (_Cursor == 0)
                Host.Menu.DrawCharacter(basex + (9 * 8) + (8 * _PortName.Length),
                    _CursorTable[0], 10 + ((int)(Host.RealTime * 4) & 1));

            if (_Cursor == 2)
                Host.Menu.DrawCharacter(basex + 16 + (8 * _JoinName.Length), _CursorTable[2],
                    10 + ((int)(Host.RealTime * 4) & 1));

            if (!string.IsNullOrEmpty(Host.Menu.ReturnReason))
                Host.Menu.PrintWhite(basex, 148, Host.Menu.ReturnReason);
        }

        public LanConfigMenu()
        {
            _Cursor = -1;
            _JoinName = string.Empty;
        }
    }
}
