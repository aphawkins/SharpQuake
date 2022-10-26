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

    /// <summary>
    /// M_Menu_GameOptions_functions
    /// </summary>
    public class GameOptionsMenu : MenuBase
    {
        private const int NUM_GAMEOPTIONS = 9;

        private static readonly Level[] Levels = new Level[]
        {
            new Level("start", "Entrance"),	// 0

	        new Level("e1m1", "Slipgate Complex"),				// 1
	        new Level("e1m2", "Castle of the Damned"),
            new Level("e1m3", "The Necropolis"),
            new Level("e1m4", "The Grisly Grotto"),
            new Level("e1m5", "Gloom Keep"),
            new Level("e1m6", "The Door To Chthon"),
            new Level("e1m7", "The House of Chthon"),
            new Level("e1m8", "Ziggurat Vertigo"),

            new Level("e2m1", "The Installation"),				// 9
	        new Level("e2m2", "Ogre Citadel"),
            new Level("e2m3", "Crypt of Decay"),
            new Level("e2m4", "The Ebon Fortress"),
            new Level("e2m5", "The Wizard's Manse"),
            new Level("e2m6", "The Dismal Oubliette"),
            new Level("e2m7", "Underearth"),

            new Level("e3m1", "Termination Central"),			// 16
	        new Level("e3m2", "The Vaults of Zin"),
            new Level("e3m3", "The Tomb of Terror"),
            new Level("e3m4", "Satan's Dark Delight"),
            new Level("e3m5", "Wind Tunnels"),
            new Level("e3m6", "Chambers of Torment"),
            new Level("e3m7", "The Haunted Halls"),

            new Level("e4m1", "The Sewage System"),				// 23
	        new Level("e4m2", "The Tower of Despair"),
            new Level("e4m3", "The Elder God Shrine"),
            new Level("e4m4", "The Palace of Hate"),
            new Level("e4m5", "Hell's Atrium"),
            new Level("e4m6", "The Pain Maze"),
            new Level("e4m7", "Azure Agony"),
            new Level("e4m8", "The Nameless City"),

            new Level("end", "Shub-Niggurath's Pit"),			// 31

	        new Level("dm1", "Place of Two Deaths"),				// 32
	        new Level("dm2", "Claustrophobopolis"),
            new Level("dm3", "The Abandoned Base"),
            new Level("dm4", "The Bad Place"),
            new Level("dm5", "The Cistern"),
            new Level("dm6", "The Dark Zone")
        };

        //MED 01/06/97 added hipnotic levels
        private static readonly Level[] HipnoticLevels = new Level[]
        {
           new Level("start", "Command HQ"),  // 0

           new Level("hip1m1", "The Pumping Station"),          // 1
           new Level("hip1m2", "Storage Facility"),
           new Level("hip1m3", "The Lost Mine"),
           new Level("hip1m4", "Research Facility"),
           new Level("hip1m5", "Military Complex"),

           new Level("hip2m1", "Ancient Realms"),          // 6
           new Level("hip2m2", "The Black Cathedral"),
           new Level("hip2m3", "The Catacombs"),
           new Level("hip2m4", "The Crypt"),
           new Level("hip2m5", "Mortum's Keep"),
           new Level("hip2m6", "The Gremlin's Domain"),

           new Level("hip3m1", "Tur Torment"),       // 12
           new Level("hip3m2", "Pandemonium"),
           new Level("hip3m3", "Limbo"),
           new Level("hip3m4", "The Gauntlet"),

           new Level("hipend", "Armagon's Lair"),       // 16

           new Level("hipdm1", "The Edge of Oblivion")           // 17
        };

        //PGM 01/07/97 added rogue levels
        //PGM 03/02/97 added dmatch level
        private static readonly Level[] RogueLevels = new Level[]
        {
            new Level("start", "Split Decision"),
            new Level("r1m1", "Deviant's Domain"),
            new Level("r1m2", "Dread Portal"),
            new Level("r1m3", "Judgement Call"),
            new Level("r1m4", "Cave of Death"),
            new Level("r1m5", "Towers of Wrath"),
            new Level("r1m6", "Temple of Pain"),
            new Level("r1m7", "Tomb of the Overlord"),
            new Level("r2m1", "Tempus Fugit"),
            new Level("r2m2", "Elemental Fury I"),
            new Level("r2m3", "Elemental Fury II"),
            new Level("r2m4", "Curse of Osiris"),
            new Level("r2m5", "Wizard's Keep"),
            new Level("r2m6", "Blood Sacrifice"),
            new Level("r2m7", "Last Bastion"),
            new Level("r2m8", "Source of Evil"),
            new Level("ctf1", "Division of Change")
        };

        private static readonly Episode[] Episodes = new Episode[]
        {
            new Episode("Welcome to Quake", 0, 1),
            new Episode("Doomed Dimension", 1, 8),
            new Episode("Realm of Black Magic", 9, 7),
            new Episode("Netherworld", 16, 7),
            new Episode("The Elder World", 23, 8),
            new Episode("Final Level", 31, 1),
            new Episode("Deathmatch Arena", 32, 6)
        };

        //MED 01/06/97  added hipnotic episodes
        private static readonly Episode[] HipnoticEpisodes = new Episode[]
        {
           new Episode("Scourge of Armagon", 0, 1),
           new Episode("Fortress of the Dead", 1, 5),
           new Episode("Dominion of Darkness", 6, 6),
           new Episode("The Rift", 12, 4),
           new Episode("Final Level", 16, 1),
           new Episode("Deathmatch Arena", 17, 1)
        };

        //PGM 01/07/97 added rogue episodes
        //PGM 03/02/97 added dmatch episode
        private static readonly Episode[] RogueEpisodes = new Episode[]
        {
            new Episode("Introduction", 0, 1),
            new Episode("Hell's Fortress", 1, 7),
            new Episode("Corridors of Time", 8, 8),
            new Episode("Deathmatch Arena", 16, 1)
        };

        private static readonly int[] _CursorTable = new int[]
        {
            40, 56, 64, 72, 80, 88, 96, 112, 120
        };

        private int _StartEpisode;

        private int _StartLevel;

        private int _MaxPlayers;

        private bool _ServerInfoMessage;

        private double _ServerInfoMessageTime;


        public override void Show(Host host)
        {
            base.Show(host);

            if (_MaxPlayers == 0)
            {
                _MaxPlayers = Host.Server.ServerStatic.maxclients;
            }

            if (_MaxPlayers < 2)
            {
                _MaxPlayers = Host.Server.ServerStatic.maxclientslimit;
            }
        }

        public override void KeyEvent(int key)
        {
            switch (key)
            {
                case KeysDef.K_ESCAPE:
                    LanConfigMenu.Show(Host);
                    break;

                case KeysDef.K_UPARROW:
                    Host.Sound.LocalSound("misc/menu1.wav");
                    _Cursor--;
                    if (_Cursor < 0)
                    {
                        _Cursor = NUM_GAMEOPTIONS - 1;
                    }

                    break;

                case KeysDef.K_DOWNARROW:
                    Host.Sound.LocalSound("misc/menu1.wav");
                    _Cursor++;
                    if (_Cursor >= NUM_GAMEOPTIONS)
                    {
                        _Cursor = 0;
                    }

                    break;

                case KeysDef.K_LEFTARROW:
                    if (_Cursor == 0)
                    {
                        break;
                    }

                    Host.Sound.LocalSound("misc/menu3.wav");
                    Change(-1);
                    break;

                case KeysDef.K_RIGHTARROW:
                    if (_Cursor == 0)
                    {
                        break;
                    }

                    Host.Sound.LocalSound("misc/menu3.wav");
                    Change(1);
                    break;

                case KeysDef.K_ENTER:
                    Host.Sound.LocalSound("misc/menu2.wav");
                    if (_Cursor == 0)
                    {
                        if (Host.Server.IsActive)
                        {
                            Host.Commands.Buffer.Append("disconnect\n");
                        }

                        Host.Commands.Buffer.Append("listen 0\n");	// so host_netport will be re-examined
                        Host.Commands.Buffer.Append(string.Format("maxplayers {0}\n", _MaxPlayers));
                        Host.Screen.BeginLoadingPlaque();

                        if (MainWindow.Common.GameKind == GameKind.Hipnotic)
                        {
                            Host.Commands.Buffer.Append(string.Format("map {0}\n",
                                HipnoticLevels[HipnoticEpisodes[_StartEpisode].firstLevel + _StartLevel].name));
                        }
                        else if (MainWindow.Common.GameKind == GameKind.Rogue)
                        {
                            Host.Commands.Buffer.Append(string.Format("map {0}\n",
                                RogueLevels[RogueEpisodes[_StartEpisode].firstLevel + _StartLevel].name));
                        }
                        else
                        {
                            Host.Commands.Buffer.Append(string.Format("map {0}\n", Levels[Episodes[_StartEpisode].firstLevel + _StartLevel].name));
                        }

                        return;
                    }

                    Change(1);
                    break;
            }
        }

        public override void Draw()
        {
            Host.Menu.DrawTransPic(16, 4, Host.DrawingContext.CachePic("gfx/qplaque.lmp", "GL_NEAREST"));
            var p = Host.DrawingContext.CachePic("gfx/p_multi.lmp", "GL_NEAREST");
            Host.Menu.DrawPic((320 - p.Width) / 2, 4, p);

            Host.Menu.DrawTextBox(152, 32, 10, 1);
            Host.Menu.Print(160, 40, "begin game");

            Host.Menu.Print(0, 56, "      Max players");
            Host.Menu.Print(160, 56, _MaxPlayers.ToString());

            Host.Menu.Print(0, 64, "        Game Type");
            if (Host.Cvars.Coop.Get<bool>())
            {
                Host.Menu.Print(160, 64, "Cooperative");
            }
            else
            {
                Host.Menu.Print(160, 64, "Deathmatch");
            }

            Host.Menu.Print(0, 72, "        Teamplay");
            if (MainWindow.Common.GameKind == GameKind.Rogue)
            {
                string msg = Host.Cvars.TeamPlay.Get<int>() switch
                {
                    1 => "No Friendly Fire",
                    2 => "Friendly Fire",
                    3 => "Tag",
                    4 => "Capture the Flag",
                    5 => "One Flag CTF",
                    6 => "Three Team CTF",
                    _ => "Off",
                };
                Host.Menu.Print(160, 72, msg);
            }
            else
            {
                string msg = Host.Cvars.TeamPlay.Get<int>() switch
                {
                    1 => "No Friendly Fire",
                    2 => "Friendly Fire",
                    _ => "Off",
                };
                Host.Menu.Print(160, 72, msg);
            }

            Host.Menu.Print(0, 80, "            Skill");
            if (Host.Cvars.Skill.Get<int>() == 0)
            {
                Host.Menu.Print(160, 80, "Easy difficulty");
            }
            else if (Host.Cvars.Skill.Get<int>() == 1)
            {
                Host.Menu.Print(160, 80, "Normal difficulty");
            }
            else if (Host.Cvars.Skill.Get<int>() == 2)
            {
                Host.Menu.Print(160, 80, "Hard difficulty");
            }
            else
            {
                Host.Menu.Print(160, 80, "Nightmare difficulty");
            }

            Host.Menu.Print(0, 88, "       Frag Limit");
            if (Host.Cvars.FragLimit.Get<int>() == 0)
            {
                Host.Menu.Print(160, 88, "none");
            }
            else
            {
                Host.Menu.Print(160, 88, string.Format("{0} frags", Host.Cvars.FragLimit.Get<int>()));
            }

            Host.Menu.Print(0, 96, "       Time Limit");
            if (Host.Cvars.TimeLimit.Get<int>() == 0)
            {
                Host.Menu.Print(160, 96, "none");
            }
            else
            {
                Host.Menu.Print(160, 96, string.Format("{0} minutes", Host.Cvars.TimeLimit.Get<int>()));
            }

            Host.Menu.Print(0, 112, "         Episode");
            //MED 01/06/97 added hipnotic episodes
            if (MainWindow.Common.GameKind == GameKind.Hipnotic)
            {
                Host.Menu.Print(160, 112, HipnoticEpisodes[_StartEpisode].description);
            }
            //PGM 01/07/97 added rogue episodes
            else if (MainWindow.Common.GameKind == GameKind.Rogue)
            {
                Host.Menu.Print(160, 112, RogueEpisodes[_StartEpisode].description);
            }
            else
            {
                Host.Menu.Print(160, 112, Episodes[_StartEpisode].description);
            }

            Host.Menu.Print(0, 120, "           Level");
            //MED 01/06/97 added hipnotic episodes
            if (MainWindow.Common.GameKind == GameKind.Hipnotic)
            {
                Host.Menu.Print(160, 120, HipnoticLevels[HipnoticEpisodes[_StartEpisode].firstLevel + _StartLevel].description);
                Host.Menu.Print(160, 128, HipnoticLevels[HipnoticEpisodes[_StartEpisode].firstLevel + _StartLevel].name);
            }
            //PGM 01/07/97 added rogue episodes
            else if (MainWindow.Common.GameKind == GameKind.Rogue)
            {
                Host.Menu.Print(160, 120, RogueLevels[RogueEpisodes[_StartEpisode].firstLevel + _StartLevel].description);
                Host.Menu.Print(160, 128, RogueLevels[RogueEpisodes[_StartEpisode].firstLevel + _StartLevel].name);
            }
            else
            {
                Host.Menu.Print(160, 120, Levels[Episodes[_StartEpisode].firstLevel + _StartLevel].description);
                Host.Menu.Print(160, 128, Levels[Episodes[_StartEpisode].firstLevel + _StartLevel].name);
            }

            // line cursor
            Host.Menu.DrawCharacter(144, _CursorTable[_Cursor], 12 + ((int)(Host.RealTime * 4) & 1));

            if (_ServerInfoMessage)
            {
                if ((Host.RealTime - _ServerInfoMessageTime) < 5.0)
                {
                    var x = (320 - (26 * 8)) / 2;
                    Host.Menu.DrawTextBox(x, 138, 24, 4);
                    x += 8;
                    Host.Menu.Print(x, 146, "  More than 4 players   ");
                    Host.Menu.Print(x, 154, " requires using command ");
                    Host.Menu.Print(x, 162, "line parameters; please ");
                    Host.Menu.Print(x, 170, "   see techinfo.txt.    ");
                }
                else
                {
                    _ServerInfoMessage = false;
                }
            }
        }

        private class Level
        {
            public string name;
            public string description;

            public Level(string name, string desc)
            {
                this.name = name;
                description = desc;
            }
        } //level_t;

        private class Episode
        {
            public string description;
            public int firstLevel;
            public int levels;

            public Episode(string desc, int firstLevel, int levels)
            {
                description = desc;
                this.firstLevel = firstLevel;
                this.levels = levels;
            }
        } //episode_t;

        /// <summary>
        /// M_NetStart_Change
        /// </summary>
        private void Change(int dir)
        {
            int count;

            switch (_Cursor)
            {
                case 1:
                    _MaxPlayers += dir;
                    if (_MaxPlayers > Host.Server.ServerStatic.maxclientslimit)
                    {
                        _MaxPlayers = Host.Server.ServerStatic.maxclientslimit;
                        _ServerInfoMessage = true;
                        _ServerInfoMessageTime = Host.RealTime;
                    }
                    if (_MaxPlayers < 2)
                    {
                        _MaxPlayers = 2;
                    }

                    break;

                case 2:
                    Host.CVars.Set("coop", Host.Cvars.Coop.Get<bool>());
                    break;

                case 3:
                    count = MainWindow.Common.GameKind == GameKind.Rogue ? 6 : 2;

                    var tp = Host.Cvars.TeamPlay.Get<int>() + dir;
                    if (tp > count)
                    {
                        tp = 0;
                    }
                    else if (tp < 0)
                    {
                        tp = count;
                    }

                    Host.CVars.Set("teamplay", tp);
                    break;

                case 4:
                    var skill = Host.Cvars.Skill.Get<int>() + dir;
                    if (skill > 3)
                    {
                        skill = 0;
                    }

                    if (skill < 0)
                    {
                        skill = 3;
                    }

                    Host.CVars.Set("skill", skill);
                    break;

                case 5:
                    var fraglimit = Host.Cvars.FragLimit.Get<int>() + (dir * 10);
                    if (fraglimit > 100)
                    {
                        fraglimit = 0;
                    }

                    if (fraglimit < 0)
                    {
                        fraglimit = 100;
                    }

                    Host.CVars.Set("fraglimit", fraglimit);
                    break;

                case 6:
                    var timelimit = Host.Cvars.TimeLimit.Get<int>() + (dir * 5);
                    if (timelimit > 60)
                    {
                        timelimit = 0;
                    }

                    if (timelimit < 0)
                    {
                        timelimit = 60;
                    }

                    Host.CVars.Set("timelimit", timelimit);
                    break;

                case 7:
                    _StartEpisode += dir;
                    //MED 01/06/97 added hipnotic count
                    count = MainWindow.Common.GameKind == GameKind.Hipnotic
                        ? 6
                        : MainWindow.Common.GameKind == GameKind.Rogue ? 4 : MainWindow.Common.IsRegistered ? 7 : 2;

                    if (_StartEpisode < 0)
                    {
                        _StartEpisode = count - 1;
                    }

                    if (_StartEpisode >= count)
                    {
                        _StartEpisode = 0;
                    }

                    _StartLevel = 0;
                    break;

                case 8:
                    _StartLevel += dir;
                    //MED 01/06/97 added hipnotic episodes
                    count = MainWindow.Common.GameKind == GameKind.Hipnotic
                        ? HipnoticEpisodes[_StartEpisode].levels
                        : MainWindow.Common.GameKind == GameKind.Rogue ? RogueEpisodes[_StartEpisode].levels : Episodes[_StartEpisode].levels;

                    if (_StartLevel < 0)
                    {
                        _StartLevel = count - 1;
                    }

                    if (_StartLevel >= count)
                    {
                        _StartLevel = 0;
                    }

                    break;
            }
        }
    }

}
