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
    public class OptionsMenu : MenuBase
    {
        private const int OPTIONS_ITEMS = 13;

        //private float _BgmVolumeCoeff = 0.1f;

        public override void Show(Host host)
        {
            /*if( sys.IsWindows )  fix cd audio first
             {
                 _BgmVolumeCoeff = 1.0f;
             }*/

            if (_Cursor > OPTIONS_ITEMS - 1)
                _Cursor = 0;

            if (_Cursor == OPTIONS_ITEMS - 1 && VideoMenu == null)
                _Cursor = 0;

            base.Show(host);
        }

        public override void KeyEvent(int key)
        {
            switch (key)
            {
                case KeysDef.K_ESCAPE:
                    MainMenu.Show(Host);
                    break;

                case KeysDef.K_ENTER:
                    Host.Menu.EnterSound = true;
                    switch (_Cursor)
                    {
                        case 0:
                            KeysMenu.Show(Host);
                            break;

                        case 1:
                            CurrentMenu.Hide();
                            Host.Console.ToggleConsole_f(null);
                            break;

                        case 2:
                            Host.Commands.Buffer.Append("exec default.cfg\n");
                            break;

                        case 12:
                            VideoMenu.Show(Host);
                            break;

                        default:
                            AdjustSliders(1);
                            break;
                    }
                    return;

                case KeysDef.K_UPARROW:
                    Host.Sound.LocalSound("misc/menu1.wav");
                    _Cursor--;
                    if (_Cursor < 0)
                        _Cursor = OPTIONS_ITEMS - 1;
                    break;

                case KeysDef.K_DOWNARROW:
                    Host.Sound.LocalSound("misc/menu1.wav");
                    _Cursor++;
                    if (_Cursor >= OPTIONS_ITEMS)
                        _Cursor = 0;
                    break;

                case KeysDef.K_LEFTARROW:
                    AdjustSliders(-1);
                    break;

                case KeysDef.K_RIGHTARROW:
                    AdjustSliders(1);
                    break;
            }

            /*if( _Cursor == 12 && VideoMenu == null )
            {
                if( key == KeysDef.K_UPARROW )
                    _Cursor = 11;
                else
                    _Cursor = 0;
            }*/

            if (_Cursor == 12)
            {
                if (key == KeysDef.K_UPARROW)
                    _Cursor = 11;
                else
                    _Cursor = 0;
            }

            /*#if _WIN32
                        if ((options_cursor == 13) && (modestate != MS_WINDOWED))
                        {
                            if (k == K_UPARROW)
                                options_cursor = 12;
                            else
                                options_cursor = 0;
                        }
            #endif*/
        }

        public override void Draw()
        {
            Host.Menu.DrawTransPic(16, 4, Host.DrawingContext.CachePic("gfx/qplaque.lmp", "GL_NEAREST"));
            var p = Host.DrawingContext.CachePic("gfx/p_option.lmp", "GL_NEAREST");
            Host.Menu.DrawPic((320 - p.Width) / 2, 4, p);

            Host.Menu.Print(16, 32, "    Customize controls");
            Host.Menu.Print(16, 40, "         Go to console");
            Host.Menu.Print(16, 48, "     Reset to defaults");

            Host.Menu.Print(16, 56, "           Screen size");
            var r = (Host.Screen.ViewSize.Get<float>() - 30) / (120 - 30);
            Host.Menu.DrawSlider(220, 56, r);

            Host.Menu.Print(16, 64, "            Brightness");
            r = (1.0f - Host.View.Gamma) / 0.5f;
            Host.Menu.DrawSlider(220, 64, r);

            Host.Menu.Print(16, 72, "           Mouse Speed");
            r = (Host.Client.Sensitivity - 1) / 10;
            Host.Menu.DrawSlider(220, 72, r);

            Host.Menu.Print(16, 80, "       CD Music Volume");
            r = Host.Sound.BgmVolume;
            Host.Menu.DrawSlider(220, 80, r);

            Host.Menu.Print(16, 88, "          Sound Volume");
            r = Host.Sound.Volume;
            Host.Menu.DrawSlider(220, 88, r);

            Host.Menu.Print(16, 96, "            Always Run");
            Host.Menu.DrawCheckbox(220, 96, Host.Client.ForwardSpeed > 200);

            Host.Menu.Print(16, 104, "          Invert Mouse");
            Host.Menu.DrawCheckbox(220, 104, Host.Client.MPitch < 0);

            Host.Menu.Print(16, 112, "            Lookspring");
            Host.Menu.DrawCheckbox(220, 112, Host.Client.LookSpring);

            Host.Menu.Print(16, 120, "            Lookstrafe");
            Host.Menu.DrawCheckbox(220, 120, Host.Client.LookStrafe);

            /*if( VideoMenu != null )
                Host.Menu.Print( 16, 128, "         Video Options" );*/

#if _WIN32
	if (modestate == MS_WINDOWED)
	{
		Host.Menu.Print (16, 136, "             Use Mouse");
		Host.Menu.DrawCheckbox (220, 136, _windowed_mouse.value);
	}
#endif

            // cursor
            Host.Menu.DrawCharacter(200, 32 + (_Cursor * 8), 12 + ((int)(Host.RealTime * 4) & 1));
        }

        /// <summary>
        /// M_AdjustSliders
        /// </summary>
        private void AdjustSliders(int dir)
        {
            Host.Sound.LocalSound("misc/menu3.wav");
            float value;

            switch (_Cursor)
            {
                case 3:	// screen size
                    value = Host.Screen.ViewSize.Get<float>() + (dir * 10);
                    if (value < 30)
                        value = 30;
                    if (value > 120)
                        value = 120;
                    Host.CVars.Set("viewsize", value);
                    break;

                case 4:	// gamma
                    value = Host.View.Gamma - (dir * 0.05f);
                    if (value < 0.5)
                        value = 0.5f;
                    if (value > 1)
                        value = 1;
                    Host.CVars.Set("gamma", value);
                    break;

                case 5:	// mouse speed
                    value = Host.Client.Sensitivity + (dir * 0.5f);
                    if (value < 1)
                        value = 1;
                    if (value > 11)
                        value = 11;
                    Host.CVars.Set("sensitivity", value);
                    break;

                case 6:	// music volume
                    value = Host.Sound.BgmVolume + (dir * 0.1f); ///_BgmVolumeCoeff;
                    if (value < 0)
                        value = 0;
                    if (value > 1)
                        value = 1;
                    Host.CVars.Set("bgmvolume", value);
                    break;

                case 7:	// sfx volume
                    value = Host.Sound.Volume + (dir * 0.1f);
                    if (value < 0)
                        value = 0;
                    if (value > 1)
                        value = 1;
                    Host.CVars.Set("volume", value);
                    break;

                case 8:	// allways run
                    if (Host.Client.ForwardSpeed > 200)
                    {
                        Host.CVars.Set("cl_forwardspeed", 200f);
                        Host.CVars.Set("cl_backspeed", 200f);
                    }
                    else
                    {
                        Host.CVars.Set("cl_forwardspeed", 400f);
                        Host.CVars.Set("cl_backspeed", 400f);
                    }
                    break;

                case 9:	// invert mouse
                    Host.CVars.Set("m_pitch", -Host.Client.MPitch);
                    break;

                case 10:	// lookspring
                    Host.CVars.Set("lookspring", !Host.Client.LookSpring);
                    break;

                case 11:	// lookstrafe
                    Host.CVars.Set("lookstrafe", !Host.Client.LookStrafe);
                    break;

#if _WIN32
	        case 13:	// _windowed_mouse
		        Cvar_SetValue ("_windowed_mouse", !_windowed_mouse.value);
		        break;
#endif
            }
        }
    }
}
