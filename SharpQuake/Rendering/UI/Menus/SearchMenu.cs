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
    public class SearchMenu : MenuBase
    {
        private bool _SearchComplete;
        private double _SearchCompleteTime;

        public override void Show(Host host)
        {
            base.Show(host);
            Host.Network.SlistSilent = true;
            Host.Network.SlistLocal = false;
            _SearchComplete = false;
            Host.Network.Slist_f(null);
        }

        public override void KeyEvent(int key)
        {
            // nothing to do
        }

        public override void Draw()
        {
            var p = Host.DrawingContext.CachePic("gfx/p_multi.lmp", "GL_NEAREST");
            Host.Menu.DrawPic((320 - p.Width) / 2, 4, p);
            var x = (320 / 2) - (12 * 8 / 2) + 4;
            Host.Menu.DrawTextBox(x - 8, 32, 12, 1);
            Host.Menu.Print(x, 40, "Searching...");

            if (Host.Network.SlistInProgress)
            {
                Host.Network.Poll();
                return;
            }

            if (!_SearchComplete)
            {
                _SearchComplete = true;
                _SearchCompleteTime = Host.RealTime;
            }

            if (Host.Network.HostCacheCount > 0)
            {
                ServerListMenu.Show(Host);
                return;
            }

            Host.Menu.PrintWhite((320 / 2) - (22 * 8 / 2), 64, "No Quake servers found");
            if ((Host.RealTime - _SearchCompleteTime) < 3.0)
            {
                return;
            }

            LanConfigMenu.Show(Host);
        }
    }
}
