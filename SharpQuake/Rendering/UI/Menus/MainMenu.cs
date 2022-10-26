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
    using SharpQuake.Framework;
    using SharpQuake.Framework.IO.Input;

    /// <summary>
    /// MainMenu
    /// </summary>
    public class MainMenu : MenuBase
    {
        private const int MAIN_ITEMS = 5;
        private int _SaveDemoNum;

        public override void Show(Host host)
        {
            if (host.Keyboard.Destination != KeyDestination.key_menu)
            {
                _SaveDemoNum = host.Client.Cls.demonum;
                host.Client.Cls.demonum = -1;
            }

            base.Show(host);
        }

        /// <summary>
        /// M_Main_Key
        /// </summary>
        public override void KeyEvent(int key)
        {
            switch (key)
            {
                case KeysDef.K_ESCAPE:
                    //Host.Keyboard.Destination = keydest_t.key_game;
                    CurrentMenu.Hide();
                    Host.Client.Cls.demonum = _SaveDemoNum;
                    if (Host.Client.Cls.demonum != -1 && !Host.Client.Cls.demoplayback && Host.Client.Cls.state != ClientActive.ca_connected)
                    {
                        Host.Client.NextDemo();
                    }

                    break;

                case KeysDef.K_DOWNARROW:
                    Host.Sound.LocalSound("misc/menu1.wav");
                    if (++_Cursor >= MAIN_ITEMS)
                    {
                        _Cursor = 0;
                    }

                    break;

                case KeysDef.K_UPARROW:
                    Host.Sound.LocalSound("misc/menu1.wav");
                    if (--_Cursor < 0)
                    {
                        _Cursor = MAIN_ITEMS - 1;
                    }

                    break;

                case KeysDef.K_ENTER:
                    Host.Menu.EnterSound = true;

                    switch (_Cursor)
                    {
                        case 0:
                            SinglePlayerMenu.Show(Host);
                            break;

                        case 1:
                            MultiPlayerMenu.Show(Host);
                            break;

                        case 2:
                            OptionsMenu.Show(Host);
                            break;

                        case 3:
                            HelpMenu.Show(Host);
                            break;

                        case 4:
                            QuitMenu.Show(Host);
                            break;
                    }
                    break;
            }
        }

        public override void Draw()
        {
            Host.Menu.DrawTransPic(16, 4, Host.DrawingContext.CachePic("gfx/qplaque.lmp", "GL_NEAREST"));
            var p = Host.DrawingContext.CachePic("gfx/ttl_main.lmp", "GL_NEAREST");
            Host.Menu.DrawPic((320 - p.Width) / 2, 4, p);
            Host.Menu.DrawTransPic(72, 32, Host.DrawingContext.CachePic("gfx/mainmenu.lmp", "GL_NEAREST"));

            var f = (int)(Host.Time * 10) % 6;

            Host.Menu.DrawTransPic(54, 32 + (_Cursor * 20), Host.DrawingContext.CachePic(string.Format("gfx/menudot{0}.lmp", f + 1), "GL_NEAREST"));
        }
    }
}
