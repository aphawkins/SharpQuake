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
    using System.IO;
    using System.Text;
    using SharpQuake.Framework;
    using SharpQuake.Framework.IO;

    public class LoadMenu : MenuBase
    {
        public const int MAX_SAVEGAMES = 12;
        protected string[] _FileNames; //[MAX_SAVEGAMES]; // filenames
        protected bool[] _Loadable; //[MAX_SAVEGAMES]; // loadable

        public override void Show(Host host)
        {
            base.Show(host);
            ScanSaves();
        }

        public override void KeyEvent(int key)
        {
            switch (key)
            {
                case KeysDef.K_ESCAPE:
                    SinglePlayerMenu.Show(Host);
                    break;

                case KeysDef.K_ENTER:
                    Host.Sound.LocalSound("misc/menu2.wav");
                    if (!_Loadable[_Cursor])
                        return;
                    CurrentMenu.Hide();

                    // Host_Loadgame_f can't bring up the loading plaque because too much
                    // stack space has been used, so do it now
                    Host.Screen.BeginLoadingPlaque();

                    // issue the load command
                    Host.Commands.Buffer.Append(string.Format("load s{0}\n", _Cursor));
                    return;

                case KeysDef.K_UPARROW:
                case KeysDef.K_LEFTARROW:
                    Host.Sound.LocalSound("misc/menu1.wav");
                    _Cursor--;
                    if (_Cursor < 0)
                        _Cursor = MAX_SAVEGAMES - 1;
                    break;

                case KeysDef.K_DOWNARROW:
                case KeysDef.K_RIGHTARROW:
                    Host.Sound.LocalSound("misc/menu1.wav");
                    _Cursor++;
                    if (_Cursor >= MAX_SAVEGAMES)
                        _Cursor = 0;
                    break;
            }
        }

        public override void Draw()
        {
            var p = Host.DrawingContext.CachePic("gfx/p_load.lmp", "GL_NEAREST");
            Host.Menu.DrawPic((320 - p.Width) / 2, 4, p);

            for (var i = 0; i < MAX_SAVEGAMES; i++)
                Host.Menu.Print(16, 32 + (8 * i), _FileNames[i]);

            // line cursor
            Host.Menu.DrawCharacter(8, 32 + (_Cursor * 8), 12 + ((int)(Host.RealTime * 4) & 1));
        }

        /// <summary>
        /// M_ScanSaves
        /// </summary>
        protected void ScanSaves()
        {
            for (var i = 0; i < MAX_SAVEGAMES; i++)
            {
                _FileNames[i] = "--- UNUSED SLOT ---";
                _Loadable[i] = false;
                var name = string.Format("{0}/s{1}.sav", FileSystem.GameDir, i);
                var fs = FileSystem.OpenRead(name);
                if (fs == null)
                    continue;

                using var reader = new StreamReader(fs, Encoding.ASCII);
                var version = reader.ReadLine();
                if (version == null)
                    continue;
                var info = reader.ReadLine();
                if (info == null)
                    continue;
                info = info.TrimEnd('\0', '_').Replace('_', ' ');
                if (!string.IsNullOrEmpty(info))
                {
                    _FileNames[i] = info;
                    _Loadable[i] = true;
                }
            }
        }

        public LoadMenu()
        {
            _FileNames = new string[MAX_SAVEGAMES];
            _Loadable = new bool[MAX_SAVEGAMES];
        }
    }
}
