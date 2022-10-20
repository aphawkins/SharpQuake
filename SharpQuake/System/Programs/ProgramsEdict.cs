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
    using System.Globalization;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Text;
    using OpenTK;
    using SharpQuake.Framework;
    using SharpQuake.Framework.IO;

    public partial class Programs
    {
        private struct gefv_cache
        {
            public ProgramDefinition pcache;
            public string field;// char	field[MAX_FIELD_LEN];
        }

        public int EdictSize { get; private set; }

        //static StringBuilder _AddedStrings = new StringBuilder(4096);
        public long GlobalStructAddr { get; private set; }

        public int Crc
        {
            get
            {
                return _Crc;
            }
        }

        public GlobalVariables GlobalStruct;
        private const int GEFV_CACHESIZE = 2;

        //gefv_cache;

        private gefv_cache[] _gefvCache = new gefv_cache[GEFV_CACHESIZE]; // gefvCache
        private int _gefvPos;

        private int[] _TypeSize = new int[8] // type_size
        {
            1, sizeof(int)/4, 1, 3, 1, 1, sizeof(int)/4, IntPtr.Size/4
        };

        private Program _Progs; // progs
        private ProgramFunction[] _Functions; // pr_functions
        private string _Strings; // pr_strings
        private ProgramDefinition[] _FieldDefs; // pr_fielddefs
        private ProgramDefinition[] _GlobalDefs; // pr_globaldefs
        private Statement[] _Statements; // pr_statements

        // pr_global_struct
        private float[] _Globals; // Added by Uze: all data after globalvars_t (numglobals * 4 - globalvars_t.SizeInBytes)
        private ushort _Crc; // pr_crc
        private GCHandle _HGlobalStruct;
        private GCHandle _HGlobals;
        private long _GlobalsAddr;
        private List<string> _DynamicStrings = new List<string>(512);

        // Instances
        public Host Host
        {
            get;
            private set;
        }

        public Programs(Host host)
        {
            Host = host;

            // Temporary workaround - will fix later
            ProgramsWrapper.OnGetString += (strId) => GetString(strId);
        }

        // PR_Init
        public void Initialise()
        {
            Host.Commands.Add("edict", PrintEdict_f);
            Host.Commands.Add("edicts", PrintEdicts);
            Host.Commands.Add("edictcount", EdictCount);
            Host.Commands.Add("profile", Profile_f);
            Host.Commands.Add("test5", Test5_f);

            if (Host.Cvars.NoMonsters == null)
            {
                Host.Cvars.NoMonsters = Host.CVars.Add("nomonsters", false);
                Host.Cvars.GameCfg = Host.CVars.Add("gamecfg", false);
                Host.Cvars.Scratch1 = Host.CVars.Add("scratch1", false);
                Host.Cvars.Scratch2 = Host.CVars.Add("scratch2", false);
                Host.Cvars.Scratch3 = Host.CVars.Add("scratch3", false);
                Host.Cvars.Scratch4 = Host.CVars.Add("scratch4", false);
                Host.Cvars.SavedGameCfg = Host.CVars.Add("savedgamecfg", false, ClientVariableFlags.Archive);
                Host.Cvars.Saved1 = Host.CVars.Add("saved1", false, ClientVariableFlags.Archive);
                Host.Cvars.Saved2 = Host.CVars.Add("saved2", false, ClientVariableFlags.Archive);
                Host.Cvars.Saved3 = Host.CVars.Add("saved3", false, ClientVariableFlags.Archive);
                Host.Cvars.Saved4 = Host.CVars.Add("saved4", false, ClientVariableFlags.Archive);
            }
        }

        /// <summary>
        /// PR_LoadProgs
        /// </summary>
        public void LoadProgs()
        {
            FreeHandles();

            Host.ProgramsBuiltIn.ClearState();
            _DynamicStrings.Clear();

            // flush the non-C variable lookup cache
            for (var i = 0; i < GEFV_CACHESIZE; i++)
                _gefvCache[i].field = null;

            Framework.Crc.Init(out _Crc);

            var buf = FileSystem.LoadFile("progs.dat");

            _Progs = Utilities.BytesToStructure<Program>(buf, 0);
            if (_Progs == null)
                Utilities.Error("PR_LoadProgs: couldn't load Host.Programs.dat");
            Host.Console.DPrint("Programs occupy {0}K.\n", buf.Length / 1024);

            for (var i = 0; i < buf.Length; i++)
                Framework.Crc.ProcessByte(ref _Crc, buf[i]);

            // byte swap the header
            _Progs.SwapBytes();

            if (_Progs.version != ProgramDef.PROG_VERSION)
                Utilities.Error("progs.dat has wrong version number ({0} should be {1})", _Progs.version, ProgramDef.PROG_VERSION);
            if (_Progs.crc != ProgramDef.PROGHEADER_CRC)
                Utilities.Error("progs.dat system vars have been modified, progdefs.h is out of date");

            // Functions
            _Functions = new ProgramFunction[_Progs.numfunctions];
            var offset = _Progs.ofs_functions;
            for (var i = 0; i < _Functions.Length; i++, offset += ProgramFunction.SizeInBytes)
            {
                _Functions[i] = Utilities.BytesToStructure<ProgramFunction>(buf, offset);
                _Functions[i].SwapBytes();
            }

            // strings
            offset = _Progs.ofs_strings;
            var str0 = offset;
            for (var i = 0; i < _Progs.numstrings; i++, offset++)
            {
                // count string length
                while (buf[offset] != 0)
                    offset++;
            }
            var length = offset - str0;
            _Strings = Encoding.ASCII.GetString(buf, str0, length);

            // Globaldefs
            _GlobalDefs = new ProgramDefinition[_Progs.numglobaldefs];
            offset = _Progs.ofs_globaldefs;
            for (var i = 0; i < _GlobalDefs.Length; i++, offset += ProgramDefinition.SizeInBytes)
            {
                _GlobalDefs[i] = Utilities.BytesToStructure<ProgramDefinition>(buf, offset);
                _GlobalDefs[i].SwapBytes();
            }

            // Fielddefs
            _FieldDefs = new ProgramDefinition[_Progs.numfielddefs];
            offset = _Progs.ofs_fielddefs;
            for (var i = 0; i < _FieldDefs.Length; i++, offset += ProgramDefinition.SizeInBytes)
            {
                _FieldDefs[i] = Utilities.BytesToStructure<ProgramDefinition>(buf, offset);
                _FieldDefs[i].SwapBytes();
                if ((_FieldDefs[i].type & ProgramDef.DEF_SAVEGLOBAL) != 0)
                    Utilities.Error("PR_LoadProgs: pr_fielddefs[i].type & DEF_SAVEGLOBAL");
            }

            // Statements
            _Statements = new Statement[_Progs.numstatements];
            offset = _Progs.ofs_statements;
            for (var i = 0; i < _Statements.Length; i++, offset += Statement.SizeInBytes)
            {
                _Statements[i] = Utilities.BytesToStructure<Statement>(buf, offset);
                _Statements[i].SwapBytes();
            }

            // Swap bytes inplace if needed
            if (!BitConverter.IsLittleEndian)
            {
                offset = _Progs.ofs_globals;
                for (var i = 0; i < _Progs.numglobals; i++, offset += 4)
                {
                    SwapHelper.Swap4b(buf, offset);
                }
            }
            GlobalStruct = Utilities.BytesToStructure<GlobalVariables>(buf, _Progs.ofs_globals);
            _Globals = new float[_Progs.numglobals - (GlobalVariables.SizeInBytes / 4)];
            Buffer.BlockCopy(buf, _Progs.ofs_globals + GlobalVariables.SizeInBytes, _Globals, 0, _Globals.Length * 4);

            EdictSize = (_Progs.entityfields * 4) + Edict.SizeInBytes - EntVars.SizeInBytes;
            ProgramDef.EdictSize = EdictSize;
            _HGlobals = GCHandle.Alloc(_Globals, GCHandleType.Pinned);
            _GlobalsAddr = _HGlobals.AddrOfPinnedObject().ToInt64();

            _HGlobalStruct = GCHandle.Alloc(Host.Programs.GlobalStruct, GCHandleType.Pinned);
            GlobalStructAddr = _HGlobalStruct.AddrOfPinnedObject().ToInt64();
        }

        // ED_PrintEdicts
        //
        // For debugging, prints all the entities in the current server
        public void PrintEdicts(CommandMessage msg)
        {
            Host.Console.Print("{0} entities\n", Host.Server.sv.num_edicts);
            for (var i = 0; i < Host.Server.sv.num_edicts; i++)
                PrintNum(i);
        }

        public int StringOffset(string value)
        {
            var tmp = '\0' + value + '\0';
            var offset = _Strings.IndexOf(tmp, StringComparison.Ordinal);
            if (offset != -1)
            {
                return MakeStingId(offset + 1, true);
            }

            for (var i = 0; i < _DynamicStrings.Count; i++)
            {
                if (_DynamicStrings[i] == value)
                {
                    return MakeStingId(i, false);
                }
            }
            return -1;
        }

        /// <summary>
        /// ED_LoadFromFile
        /// The entities are directly placed in the array, rather than allocated with
        /// ED_Alloc, because otherwise an error loading the map would have entity
        /// number references out of order.
        ///
        /// Creates a server's entity / program execution context by
        /// parsing textual entity definitions out of an ent file.
        ///
        /// Used for both fresh maps and savegame loads.  A fresh map would also need
        /// to call ED_CallSpawnFunctions () to let the objects initialize themselves.
        /// </summary>
        public void LoadFromFile(string data)
        {
            MemoryEdict ent = null;
            var inhibit = 0;
            Host.Programs.GlobalStruct.time = (float)Host.Server.sv.time;

            // parse ents
            while (true)
            {
                // parse the opening brace
                data = Tokeniser.Parse(data);
                if (data == null)
                    break;

                if (Tokeniser.Token != "{")
                    Utilities.Error("ED_LoadFromFile: found {0} when expecting {", Tokeniser.Token);

                ent = ent == null ? Host.Server.EdictNum(0) : Host.Server.AllocEdict();
                data = ParseEdict(data, ent);

                // remove things from different skill levels or deathmatch
                if (Host.Cvars.Deathmatch.Get<int>() != 0)
                {
                    if (((int)ent.v.spawnflags & SpawnFlags.SPAWNFLAG_NOT_DEATHMATCH) != 0)
                    {
                        Host.Server.FreeEdict(ent);
                        inhibit++;
                        continue;
                    }
                }
                else if ((Host.CurrentSkill == 0 && ((int)ent.v.spawnflags & SpawnFlags.SPAWNFLAG_NOT_EASY) != 0) ||
                    (Host.CurrentSkill == 1 && ((int)ent.v.spawnflags & SpawnFlags.SPAWNFLAG_NOT_MEDIUM) != 0) ||
                    (Host.CurrentSkill >= 2 && ((int)ent.v.spawnflags & SpawnFlags.SPAWNFLAG_NOT_HARD) != 0))
                {
                    Host.Server.FreeEdict(ent);
                    inhibit++;
                    continue;
                }

                //
                // immediately call spawn function
                //
                if (ent.v.classname == 0)
                {
                    Host.Console.Print("No classname for:\n");
                    Print(ent);
                    Host.Server.FreeEdict(ent);
                    continue;
                }

                // look for the spawn function
                var func = IndexOfFunction(GetString(ent.v.classname));
                if (func == -1)
                {
                    Host.Console.Print("No spawn function for:\n");
                    Print(ent);
                    Host.Server.FreeEdict(ent);
                    continue;
                }

                GlobalStruct.self = Host.Server.EdictToProg(ent);
                Execute(func);
            }

            Host.Console.DPrint("{0} entities inhibited\n", inhibit);
        }

        /// <summary>
        /// ED_ParseEdict
        /// Parses an edict out of the given string, returning the new position
        /// ed should be a properly initialized empty edict.
        /// Used for initial level load and for savegames.
        /// </summary>
        public string ParseEdict(string data, MemoryEdict ent)
        {
            var init = false;

            // clear it
            if (ent != Host.Server.sv.edicts[0])	// hack
                ent.Clear();

            // go through all the dictionary pairs
            bool anglehack;
            while (true)
            {
                // parse key
                data = Tokeniser.Parse(data);
                if (Tokeniser.Token.StartsWith("}"))
                    break;

                if (data == null)
                    Utilities.Error("ED_ParseEntity: EOF without closing brace");

                var token = Tokeniser.Token;

                // anglehack is to allow QuakeEd to write single scalar angles
                // and allow them to be turned into vectors. (FIXME...)
                if (token == "angle")
                {
                    token = "angles";
                    anglehack = true;
                }
                else
                    anglehack = false;

                // FIXME: change light to _light to get rid of this hack
                if (token == "light")
                    token = "light_lev";	// hack for single light def

                var keyname = token.TrimEnd();

                // parse value
                data = Tokeniser.Parse(data);
                if (data == null)
                    Utilities.Error("ED_ParseEntity: EOF without closing brace");

                if (Tokeniser.Token.StartsWith("}"))
                    Utilities.Error("ED_ParseEntity: closing brace without data");

                init = true;

                // keynames with a leading underscore are used for utility comments,
                // and are immediately discarded by quake
                if (keyname[0] == '_')
                    continue;

                var key = FindField(keyname);
                if (key == null)
                {
                    Host.Console.Print("'{0}' is not a field\n", keyname);
                    continue;
                }

                token = Tokeniser.Token;
                if (anglehack)
                {
                    token = "0 " + token + " 0";
                }

                if (!ParsePair(ent, key, token))
                    Host.Error("ED_ParseEdict: parse error");
            }

            if (!init)
                ent.free = true;

            return data;
        }

        /// <summary>
        /// ED_Print
        /// For debugging
        /// </summary>
        public unsafe void Print(MemoryEdict ed)
        {
            if (ed.free)
            {
                Host.Console.Print("FREE\n");
                return;
            }

            Host.Console.Print("\nEDICT {0}:\n", Host.Server.NumForEdict(ed));
            for (var i = 1; i < _Progs.numfielddefs; i++)
            {
                var d = _FieldDefs[i];
                var name = GetString(d.s_name);

                if (name.Length > 2 && name[^2] == '_')
                    continue; // skip _x, _y, _z vars

                var type = d.type & ~ProgramDef.DEF_SAVEGLOBAL;
                int offset;
                if (ed.IsV(d.ofs, out offset))
                {
                    fixed (void* ptr = &ed.v)
                    {
                        var v = (int*)ptr + offset;
                        if (IsEmptyField(type, v))
                            continue;

                        Host.Console.Print("{0,15} ", name);
                        Host.Console.Print("{0}\n", ValueString((EdictType)d.type, (void*)v));
                    }
                }
                else
                {
                    fixed (void* ptr = ed.fields)
                    {
                        var v = (int*)ptr + offset;
                        if (IsEmptyField(type, v))
                            continue;

                        Host.Console.Print("{0,15} ", name);
                        Host.Console.Print("{0}\n", ValueString((EdictType)d.type, (void*)v));
                    }
                }
            }
        }

        public string GetString(int strId)
        {
            int offset;
            if (IsStaticString(strId, out offset))
            {
                var i0 = offset;
                while (offset < _Strings.Length && _Strings[offset] != 0)
                    offset++;

                var length = offset - i0;
                if (length > 0)
                    return _Strings.Substring(i0, length);
            }
            else
            {
                if (offset < 0 || offset >= _DynamicStrings.Count)
                {
                    throw new ArgumentException("Invalid string id!");
                }
                return _DynamicStrings[offset];
            }

            return string.Empty;
        }

        public bool SameName(int name1, string name2)
        {
            var offset = name1;
            if (offset + name2.Length > _Strings.Length)
                return false;

            for (var i = 0; i < name2.Length; i++, offset++)
                if (_Strings[offset] != name2[i])
                    return false;

            if (offset < _Strings.Length && _Strings[offset] != 0)
                return false;

            return true;
        }

        /// <summary>
        /// Like ED_NewString but returns string id (string_t)
        /// </summary>
        public int NewString(string s)
        {
            var id = AllocString();
            var sb = new StringBuilder(s.Length);
            var len = s.Length;
            for (var i = 0; i < len; i++)
            {
                if (s[i] == '\\' && i < len - 1)
                {
                    i++;
                    if (s[i] == 'n')
                        sb.Append('\n');
                    else
                        sb.Append('\\');
                }
                else
                    sb.Append(s[i]);
            }
            SetString(id, sb.ToString());
            return id;
        }

        public float GetEdictFieldFloat(MemoryEdict ed, string field, float defValue = 0)
        {
            var def = CachedSearch(ed, field);
            if (def == null)
                return defValue;

            return ed.GetFloat(def.ofs);
        }

        public bool SetEdictFieldFloat(MemoryEdict ed, string field, float value)
        {
            var def = CachedSearch(ed, field);
            if (def != null)
            {
                ed.SetFloat(def.ofs, value);
                return true;
            }
            return false;
        }

        public int AllocString()
        {
            var id = _DynamicStrings.Count;
            _DynamicStrings.Add(string.Empty);
            return MakeStingId(id, false);
        }

        public void SetString(int id, string value)
        {
            int offset;
            if (IsStaticString(id, out offset))
            {
                throw new ArgumentException("Static strings are read-only!");
            }
            if (offset < 0 || offset >= _DynamicStrings.Count)
            {
                throw new ArgumentException("Invalid string id!");
            }
            _DynamicStrings[offset] = value;
        }

        /// <summary>
        /// ED_WriteGlobals
        /// </summary>
        public unsafe void WriteGlobals(StreamWriter writer)
        {
            writer.WriteLine("{");
            for (var i = 0; i < _Progs.numglobaldefs; i++)
            {
                var def = _GlobalDefs[i];
                var type = (EdictType)def.type;
                if ((def.type & ProgramDef.DEF_SAVEGLOBAL) == 0)
                    continue;

                type &= (EdictType)~ProgramDef.DEF_SAVEGLOBAL;

                if (type is not EdictType.ev_string and not EdictType.ev_float and not EdictType.ev_entity)
                    continue;

                writer.Write("\"");
                writer.Write(GetString(def.s_name));
                writer.Write("\" \"");
                writer.Write(UglyValueString(type, (EVal*)Get(def.ofs)));
                writer.WriteLine("\"");
            }
            writer.WriteLine("}");
        }

        /// <summary>
        /// ED_Write
        /// </summary>
        public unsafe void WriteEdict(StreamWriter writer, MemoryEdict ed)
        {
            writer.WriteLine("{");

            if (ed.free)
            {
                writer.WriteLine("}");
                return;
            }

            for (var i = 1; i < _Progs.numfielddefs; i++)
            {
                var d = _FieldDefs[i];
                var name = GetString(d.s_name);
                if (name != null && name.Length > 2 && name[^2] == '_')// [strlen(name) - 2] == '_')
                    continue;	// skip _x, _y, _z vars

                var type = d.type & ~ProgramDef.DEF_SAVEGLOBAL;
                int offset1;
                if (ed.IsV(d.ofs, out offset1))
                {
                    fixed (void* ptr = &ed.v)
                    {
                        var v = (int*)ptr + offset1;
                        if (IsEmptyField(type, v))
                            continue;

                        writer.WriteLine("\"{0}\" \"{1}\"", name, UglyValueString((EdictType)d.type, (EVal*)v));
                    }
                }
                else
                {
                    fixed (void* ptr = ed.fields)
                    {
                        var v = (int*)ptr + offset1;
                        if (IsEmptyField(type, v))
                            continue;

                        writer.WriteLine("\"{0}\" \"{1}\"", name, UglyValueString((EdictType)d.type, (EVal*)v));
                    }
                }
            }

            writer.WriteLine("}");
        }

        /// <summary>
        /// ED_ParseGlobals
        /// </summary>
        public void ParseGlobals(string data)
        {
            while (true)
            {
                // parse key
                data = Tokeniser.Parse(data);
                if (Tokeniser.Token.StartsWith("}"))
                    break;

                if (string.IsNullOrEmpty(data))
                    Utilities.Error("ED_ParseEntity: EOF without closing brace");

                var keyname = Tokeniser.Token;

                // parse value
                data = Tokeniser.Parse(data);
                if (string.IsNullOrEmpty(data))
                    Utilities.Error("ED_ParseEntity: EOF without closing brace");

                if (Tokeniser.Token.StartsWith("}"))
                    Utilities.Error("ED_ParseEntity: closing brace without data");

                var key = FindGlobal(keyname);
                if (key == null)
                {
                    Host.Console.Print("'{0}' is not a global\n", keyname);
                    continue;
                }

                if (!ParseGlobalPair(key, Tokeniser.Token))
                    Host.Error("ED_ParseGlobals: parse error");
            }
        }

        /// <summary>
        /// ED_PrintNum
        /// </summary>
        public void PrintNum(int ent)
        {
            Print(Host.Server.EdictNum(ent));
        }

        private void Test5_f(CommandMessage msg)
        {
            var p = Host.Client.ViewEntity;
            if (p == null)
                return;

            var org = p.origin;

            for (var i = 0; i < Host.Server.sv.edicts.Length; i++)
            {
                var ed = Host.Server.sv.edicts[i];

                if (ed.free)
                    continue;

                Vector3 vmin, vmax;
                MathLib.Copy(ref ed.v.absmax, out vmax);
                MathLib.Copy(ref ed.v.absmin, out vmin);

                if (org.X >= vmin.X && org.Y >= vmin.Y && org.Z >= vmin.Z &&
                    org.X <= vmax.X && org.Y <= vmax.Y && org.Z <= vmax.Z)
                {
                    Host.Console.Print("{0}\n", i);
                }
            }
        }

        private void FreeHandles()
        {
            if (_HGlobals.IsAllocated)
            {
                _HGlobals.Free();
                _GlobalsAddr = 0;
            }
            if (_HGlobalStruct.IsAllocated)
            {
                _HGlobalStruct.Free();
                GlobalStructAddr = 0;
            }
        }

        /// <summary>
        /// ED_PrintEdict_f
        /// For debugging, prints a single edict
        /// </summary>
        private void PrintEdict_f(CommandMessage msg)
        {
            var i = MathLib.atoi(msg.Parameters[0]);
            if (i >= Host.Server.sv.num_edicts)
            {
                Host.Console.Print("Bad edict number\n");
                return;
            }
            Host.Programs.PrintNum(i);
        }

        // ED_Count
        //
        // For debugging
        private void EdictCount(CommandMessage msg)
        {
            int active = 0, models = 0, solid = 0, step = 0;

            for (var i = 0; i < Host.Server.sv.num_edicts; i++)
            {
                var ent = Host.Server.EdictNum(i);
                if (ent.free)
                    continue;
                active++;
                if (ent.v.solid != 0)
                    solid++;
                if (ent.v.model != 0)
                    models++;
                if (ent.v.movetype == Movetypes.MOVETYPE_STEP)
                    step++;
            }

            Host.Console.Print("num_edicts:{0}\n", Host.Server.sv.num_edicts);
            Host.Console.Print("active    :{0}\n", active);
            Host.Console.Print("view      :{0}\n", models);
            Host.Console.Print("touch     :{0}\n", solid);
            Host.Console.Print("step      :{0}\n", step);
        }

        private int IndexOfFunction(string name)
        {
            for (var i = 0; i < _Functions.Length; i++)
            {
                if (SameName(_Functions[i].s_name, name))
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Since memory block containing original edict_t plus additional data
        /// is split into two fiels - edict_t.v and edict_t.fields we must check key.ofs
        /// to choose between thistwo parts.
        /// Warning: Key offset is in integers not bytes!
        /// </summary>
        private unsafe bool ParsePair(MemoryEdict ent, ProgramDefinition key, string s)
        {
            int offset1;
            if (ent.IsV(key.ofs, out offset1))
            {
                fixed (EntVars* ptr = &ent.v)
                {
                    return ParsePair((int*)ptr + offset1, key, s);
                }
            }
            else
                fixed (float* ptr = ent.fields)
                {
                    return ParsePair(ptr + offset1, key, s);
                }
        }

        /// <summary>
        /// ED_ParseEpair
        /// Can parse either fields or globals returns false if error
        /// Uze: Warning! value pointer is already with correct offset (value = base + key.ofs)!
        /// </summary>
        private unsafe bool ParsePair(void* value, ProgramDefinition key, string s)
        {
            var d = value;// (void *)((int *)base + key->ofs);

            switch ((EdictType)(key.type & ~ProgramDef.DEF_SAVEGLOBAL))
            {
                case EdictType.ev_string:
                    *(int*)d = NewString(s);// - pr_strings;
                    break;

                case EdictType.ev_float:
                    *(float*)d = MathLib.atof(s);
                    break;

                case EdictType.ev_vector:
                    var vs = s.Split(' ');
                    ((float*)d)[0] = MathLib.atof(vs[0]);
                    ((float*)d)[1] = vs.Length > 1 ? MathLib.atof(vs[1]) : 0;
                    ((float*)d)[2] = vs.Length > 2 ? MathLib.atof(vs[2]) : 0;
                    break;

                case EdictType.ev_entity:
                    *(int*)d = Host.Server.EdictToProg(Host.Server.EdictNum(MathLib.atoi(s)));
                    break;

                case EdictType.ev_field:
                    var f = IndexOfField(s);
                    if (f == -1)
                    {
                        Host.Console.Print("Can't find field {0}\n", s);
                        return false;
                    }
                    *(int*)d = GetInt32(_FieldDefs[f].ofs);
                    break;

                case EdictType.ev_function:
                    var func = IndexOfFunction(s);
                    if (func == -1)
                    {
                        Host.Console.Print("Can't find function {0}\n", s);
                        return false;
                    }
                    *(int*)d = func;// - pr_functions;
                    break;

                default:
                    break;
            }
            return true;
        }

        private int IndexOfField(string name)
        {
            for (var i = 0; i < _FieldDefs.Length; i++)
            {
                if (SameName(_FieldDefs[i].s_name, name))
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Returns true if ofs is inside GlobalStruct or false if ofs is in _Globals
        /// Out parameter offset is set to correct offset inside either GlobalStruct or _Globals
        /// </summary>
        private bool IsGlobalStruct(int ofs, out int offset)
        {
            if (ofs < GlobalVariables.SizeInBytes >> 2)
            {
                offset = ofs;
                return true;
            }
            offset = ofs - (GlobalVariables.SizeInBytes >> 2);
            return false;
        }

        /// <summary>
        /// Mimics G_xxx macros
        /// But globals are split too, so we must check offset and choose
        /// GlobalStruct or _Globals
        /// </summary>
        private unsafe void* Get(int offset)
        {
            int offset1;
            if (IsGlobalStruct(offset, out offset1))
            {
                return (int*)GlobalStructAddr + offset1;
            }
            return (int*)_GlobalsAddr + offset1;
        }

        private unsafe void Set(int offset, int value)
        {
            if (offset < GlobalVariables.SizeInBytes >> 2)
            {
                *((int*)GlobalStructAddr + offset) = value;
            }
            else
            {
                *((int*)_GlobalsAddr + offset - (GlobalVariables.SizeInBytes >> 2)) = value;
            }
        }

        private unsafe int GetInt32(int offset)
        {
            return *(int*)Get(offset);
        }

        /// <summary>
        /// ED_FindField
        /// </summary>
        private ProgramDefinition FindField(string name)
        {
            var i = IndexOfField(name);
            if (i != -1)
                return _FieldDefs[i];

            return null;
        }

        /// <summary>
        /// PR_ValueString
        /// </summary>
        private unsafe string ValueString(EdictType type, void* val)
        {
            string result;
            type &= (EdictType)~ProgramDef.DEF_SAVEGLOBAL;

            switch (type)
            {
                case EdictType.ev_string:
                    result = GetString(*(int*)val);
                    break;

                case EdictType.ev_entity:
                    result = "entity " + Host.Server.NumForEdict(Host.Server.ProgToEdict(*(int*)val));
                    break;

                case EdictType.ev_function:
                    var f = _Functions[*(int*)val];
                    result = GetString(f.s_name) + "()";
                    break;

                case EdictType.ev_field:
                    var def = FindField(*(int*)val);
                    result = "." + GetString(def.s_name);
                    break;

                case EdictType.ev_void:
                    result = "void";
                    break;

                case EdictType.ev_float:
                    result = (*(float*)val).ToString("F1", CultureInfo.InvariantCulture.NumberFormat);
                    break;

                case EdictType.ev_vector:
                    result = string.Format(CultureInfo.InvariantCulture.NumberFormat,
                        "{0,5:F1} {1,5:F1} {2,5:F1}", ((float*)val)[0], ((float*)val)[1], ((float*)val)[2]);
                    break;

                case EdictType.ev_pointer:
                    result = "pointer";
                    break;

                default:
                    result = "bad type " + type.ToString();
                    break;
            }

            return result;
        }

        private int IndexOfField(int ofs)
        {
            for (var i = 0; i < _FieldDefs.Length; i++)
            {
                if (_FieldDefs[i].ofs == ofs)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// ED_FieldAtOfs
        /// </summary>
        private ProgramDefinition FindField(int ofs)
        {
            var i = IndexOfField(ofs);
            if (i != -1)
                return _FieldDefs[i];

            return null;
        }

        private ProgramDefinition CachedSearch(MemoryEdict ed, string field)
        {
            ProgramDefinition def = null;
            for (var i = 0; i < GEFV_CACHESIZE; i++)
            {
                if (field == _gefvCache[i].field)
                {
                    def = _gefvCache[i].pcache;
                    return def;
                }
            }

            def = FindField(field);

            _gefvCache[_gefvPos].pcache = def;
            _gefvCache[_gefvPos].field = field;
            _gefvPos ^= 1;

            return def;
        }

        private int MakeStingId(int index, bool isStatic)
        {
            return ((isStatic ? 0 : 1) << 24) + (index & 0xFFFFFF);
        }

        private bool IsStaticString(int stringId, out int offset)
        {
            offset = stringId & 0xFFFFFF;
            return ((stringId >> 24) & 1) == 0;
        }

        /// <summary>
        /// PR_UglyValueString
        /// Returns a string describing *data in a type specific manner
        /// Easier to parse than PR_ValueString
        /// </summary>
        private unsafe string UglyValueString(EdictType type, EVal* val)
        {
            type &= (EdictType)~ProgramDef.DEF_SAVEGLOBAL;
            string result;

            switch (type)
            {
                case EdictType.ev_string:
                    result = GetString(val->_string);
                    break;

                case EdictType.ev_entity:
                    result = Host.Server.NumForEdict(Host.Server.ProgToEdict(val->edict)).ToString();
                    break;

                case EdictType.ev_function:
                    var f = _Functions[val->function];
                    result = GetString(f.s_name);
                    break;

                case EdictType.ev_field:
                    var def = FindField(val->_int);
                    result = GetString(def.s_name);
                    break;

                case EdictType.ev_void:
                    result = "void";
                    break;

                case EdictType.ev_float:
                    result = val->_float.ToString("F6", CultureInfo.InvariantCulture.NumberFormat);
                    break;

                case EdictType.ev_vector:
                    result = string.Format(CultureInfo.InvariantCulture.NumberFormat,
                        "{0:F6} {1:F6} {2:F6}", val->vector[0], val->vector[1], val->vector[2]);
                    break;

                default:
                    result = "bad type " + type.ToString();
                    break;
            }

            return result;
        }

        private unsafe bool IsEmptyField(int type, int* v)
        {
            for (var j = 0; j < _TypeSize[type]; j++)
                if (v[j] != 0)
                    return false;

            return true;
        }

        /// <summary>
        /// ED_FindGlobal
        /// </summary>
        private ProgramDefinition FindGlobal(string name)
        {
            for (var i = 0; i < _GlobalDefs.Length; i++)
            {
                var def = _GlobalDefs[i];
                if (name == GetString(def.s_name))
                    return def;
            }
            return null;
        }

        private unsafe bool ParseGlobalPair(ProgramDefinition key, string value)
        {
            int offset;
            if (IsGlobalStruct(key.ofs, out offset))
            {
                return ParsePair((float*)GlobalStructAddr + offset, key, value);
            }
            return ParsePair((float*)_GlobalsAddr + offset, key, value);
        }

        /// <summary>
        /// PR_GlobalString
        /// Returns a string with a description and the contents of a global,
        /// padded to 20 field width
        /// </summary>
        private unsafe string GlobalString(int ofs)
        {
            var line = string.Empty;
            var val = Get(ofs);// (void*)&pr_globals[ofs];
            var def = GlobalAtOfs(ofs);
            if (def == null)
                line = string.Format("{0}(???)", ofs);
            else
            {
                var s = ValueString((EdictType)def.type, val);
                line = string.Format("{0}({1}){2} ", ofs, GetString(def.s_name), s);
            }

            line = line.PadRight(20);

            return line;
        }

        /// <summary>
        /// PR_GlobalStringNoContents
        /// </summary>
        private string GlobalStringNoContents(int ofs)
        {
            var line = string.Empty;
            var def = GlobalAtOfs(ofs);
            line = def == null ? string.Format("{0}(???)", ofs) : string.Format("{0}({1}) ", ofs, GetString(def.s_name));

            line = line.PadRight(20);

            return line;
        }

        /// <summary>
        /// ED_GlobalAtOfs
        /// </summary>
        private ProgramDefinition GlobalAtOfs(int ofs)
        {
            for (var i = 0; i < _GlobalDefs.Length; i++)
            {
                var def = _GlobalDefs[i];
                if (def.ofs == ofs)
                    return def;
            }
            return null;
        }
    }
}
