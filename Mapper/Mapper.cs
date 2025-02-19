﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Mapper.Scripting;
using ProxyCore;
using ProxyCore.Input;
using ProxyCore.Output;
using ProxyCore.Scripting;
using System.IO;
using System.Runtime.Serialization;

namespace Mapper
{
    public partial class Mapper : Plugin
    {
        public Mapper()
            : base("mapper", "Mapper")
        {
            Author = "Duckbat";
            Version = 15;
            Description = "This plugin will record what rooms you have been in and from that can generate speedwalks for you to easily move around the world of Aardwolf.";
            UpdateUrl = "www.duckbat.com/plugins/update.mapper.txt";
            Website = "code.google.com/p/proxymud/";

            Config = new MapperConfig();

            RegisterCommand("map", "", CommandMapper);
            RegisterCommand("all", @"(.+)", CommandAll, 0, CMDFlags.None, "map");
            RegisterCommand("count", "", CommandCount, 0, CMDFlags.None, "map");
			RegisterCommand("clean", "", CommandClean, 5, CMDFlags.None, "map");
            RegisterCommand("createexit", "^(\\d+)(\\s+\\d+)?\\s+\"(.+)\"$", CommandCreateExit, 0, CMDFlags.None, "map");
            RegisterCommand("createportal", "^(\\d+)\\s+\"(.+)\"$", CommandCreatePortal, 0, CMDFlags.None, "map");
            RegisterCommand("delete", @"^(area|room|exit|portal)\s+(\d+)", CommandDelete, 3, CMDFlags.None, "map");
            RegisterCommand("exits", @"^(help)?(room\s+\d+)?(\d+)?(\s+.+)?", CommandExit, 4, CMDFlags.None, "map");
            RegisterCommand("find", @"^(room|area)(\s+exact)?(\s+case)?\s+(.+)", CommandFind, 1, CMDFlags.None, "map");
            RegisterCommand("portals", "", CommandPortal, 4, CMDFlags.None, "map");
            RegisterCommand("goto", @"(.+)", CommandGoto, 2, CMDFlags.None, "map");
            RegisterCommand("import", @"(.+)", CommandImport, 0, CMDFlags.None, "map");
            RegisterCommand("roominfo", @"^(help)?(\d+)?(\s+.+)?", CommandRoomInfo, 4, CMDFlags.None, "map");
            RegisterCommand("save", @"(.+)", CommandSave, 4, CMDFlags.None, "map");
            RegisterCommand("unmapped", @"^(go|all)$", CommandUnmapped, 2, CMDFlags.None, "map");
            RegisterCommand("unreconed", @"^(go|all)$", CommandUnreconed, 3, CMDFlags.None, "map");

            RegisterTrigger("room.id", @"^\$gmcp\.room\.info\.num (-?\d+)$", TriggerRoomInfoNum);
            RegisterTrigger("room.name", @"^\$gmcp\.room\.info\.name (.*)$", TriggerRoomInfoName);
            RegisterTrigger("room.area", @"^\$gmcp\.room\.info\.zone (.*)$", TriggerRoomInfoArea);
            RegisterTrigger("room.exit", @"^\$gmcp\.room\.info\.exits\.(\w) (-?\d+)$", TriggerRoomInfoExits);
            RegisterTrigger("room.finish", @"^\$gmcp\.room\.info\.coords?\.", TriggerRoomInfoFinish);
            RegisterTrigger("char.level", @"^\$gmcp\.char\.status\.level (\d+)$", TriggerCharStatusLevel);
            RegisterTrigger("char.tier", @"^\$gmcp\.char\.base\.tier (\d+)$", TriggerCharBaseTier);
            RegisterTrigger("char.remorts", @"^\$gmcp\.char\.base\.remorts (\d+)$", TriggerCharBaseRemorts);
            RegisterTrigger("gq.join", @"@wYou have now joined @gGlobal Quest # \d+@w. See 'help gquest' for available commands.",
                            TriggerJoinedGQ);
            RegisterTrigger("gq.extended", @"@wGlobal Quest: The global quest will go into extended time for \d+ minutes.",
                TriggerJoinedGQ, TriggerFlags.NotRegex);
            RegisterTrigger("gq.left", @"@wYou are no longer part of the current quest.", TriggerLeftGQ,
                            TriggerFlags.NotRegex);
            RegisterTrigger("gq.finished", @"@wYou have finished this global quest.", TriggerLeftGQ,
                TriggerFlags.NotRegex);
            RegisterTrigger("gq.win",
                            @"^@RGlobal Quest@Y: @gGlobal quest # \d+ @whas been won by @Y\w+ @w- @Y\d+.. @wwin\.$",
                            TriggerLeftGQ);
            RegisterTrigger("gq.quit", @"@wYou are no longer part of the current quest.", TriggerLeftGQ, TriggerFlags.NotRegex);
            RegisterTrigger("gq.quit", @"@wYou are not in a global quest.", TriggerLeftGQ, TriggerFlags.NotRegex);
            RegisterTrigger("where.name", @"^@GYou are in area : (.+)", TriggerWhereName);
            RegisterTrigger("where.level", @"^@GLevel range is  : @R(\d+) to (\d+)", TriggerWhereLevel);
            RegisterTrigger("areas.start", @"^@WFrom To   Lock  Keyword          Area Name", TriggerAreasStart);
            RegisterTrigger("areas.end", @"@w---------------------------------------------------------------", TriggerAreasEnd, TriggerFlags.NotRegex);
            RegisterTrigger("areas.entry", @"^\s+@w(\d+)\s+(\d+)\s+(@R\d+\s+)?@g([\d\w]+)\s+@c(.+)", TriggerAreasEntry);
            RegisterTrigger("home", @"@wYou cannot return home from this room.", TriggerNoRecall, TriggerFlags.NotRegex);
            RegisterTrigger("recall", @"@wYou cannot recall from this room.", TriggerNoRecall, TriggerFlags.NotRegex);
            RegisterTrigger("portal", @"@wMagic walls bounce you back.", TriggerPrison, TriggerFlags.NotRegex);
            RegisterTrigger("recon.area", @"^@wArea Name       : (.+)", TriggerReconArea);
            RegisterTrigger("recon.sector", @"^@wSector type is  : (.+)", TriggerReconSector);
            RegisterTrigger("recon.flags", @"^@wBase flags      :(.*)", TriggerReconFlags);
            RegisterTrigger("recon.healrate", @"^@wHeal rate       : @c(-?\d+)", TriggerReconHealRate);
            RegisterTrigger("recon.manarate", @"^@wMana rate       : @c(-?\d+)", TriggerReconManaRate);
            RegisterTrigger("recon.unable", "@wYou cannot perform reconnaissance in this room.", TriggerNoRecon, TriggerFlags.NotRegex);
            RegisterTrigger("tags.exits", @"^(@w)?\{exits\}(.*)", TriggerTagsExits);

            Load();

            // If we don't have this unmapped area set then create it, just so we can save rooms we haven't explored yet
            if(GetArea(uint.MaxValue) == null)
            {
                Area a = new Area(uint.MaxValue);
                a.Keyword = "mapper_unmapped";
                a.Name = "Mapper unmapped rooms";
                IAreas[a.Entry] = a;
            }

            Path_Init();
        }

        private const string DBFileName = "mapperdb.xml";
        private const string DBFileBackup = "mapperdb_backup.xml";

        private int Level = 1;
        private int Tier = 0;
        private bool ListenArea = false;
        private int Remorts = 1;
        private bool HasGQ = false;
        private long WhenSave = 0;

        private void OnDeleted(Room r)
        {
            List<uint> del = new List<uint>();
			List<uint> del2 = new List<uint>();
            foreach(KeyValuePair<uint, Exit> x in IExits)
            {
                if(x.Key == uint.MaxValue || x.Key == 0)
                    continue;
                if(!IRooms.ContainsKey(x.Value.ToRoom))
                    del.Add(x.Key);
            }

           foreach(KeyValuePair<uint, Exit> x in IPortals)
           {
               if(x.Key == uint.MaxValue || x.Key == 0)
                   continue;
               if(x.Value == null)
                   continue;
               if(!IRooms.ContainsKey(x.Value.ToRoom))
                   del2.Add(x.Key);
           }
			
            foreach(uint x in del)
            {
                IExits[x].From.exits.Remove(IExits[x]);
                IExits.Remove(x);
            }
           foreach(uint x in del2)
            {
                IPortals.Remove(x);
            }
        }

        /// <summary>
        /// Fill a pathfinder with our current character values and return if we were successful in doing that.
        /// </summary>
        /// <param name="p"></param>
        public bool FillPathFinder(Pathfinder p)
        {
            Room r;
            if(CurrentRoomId == uint.MaxValue || (r = GetRoom(CurrentRoomId)) == null || r.Area.Entry == uint.MaxValue)
                return false;
            p.CharacterLevel = Level;
            p.CharacterTier = Tier;
            p.CanUseRecalls = true;
            p.CanUsePortals = true;
            p.IsGlobalQuest = HasGQ;
            p.IsSingleClassTier0 = Tier == 0 && Remorts == 1;
            p.StartRooms = new[] { r };
            return true;
        }

        #region Commands
        private bool CommandAll(InputData i)
        {
            string[] spl;
            if(!i.Arguments.Success || (spl = i.Arguments.Groups[1].Value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)).Length == 0)
            {
                World.Instance.SendMessage("@wSyntax: map all <room id> <room id> [room id] ...", i.ClientMask);
                return true;
            }

            List<Room> rooms = new List<Room>();
            bool hadConfirm = false;
            foreach(string x in spl)
            {
                uint u;
                if(uint.TryParse(x, out u))
                {
                    Room r = GetRoom(u);
                    if(r != null && !rooms.Contains(r))
                        rooms.Add(r);
                }
                else if(x.ToLower() == "confirm")
                    hadConfirm = true;
            }

            if(rooms.Count < 2)
            {
                World.Instance.SendMessage("@wSyntax: map all <room id> <room id> [room id] ...", i.ClientMask);
                return true;
            }

            if(rooms.Count > 10 && !hadConfirm)
            {
                World.Instance.SendMessage("@RWarning! @wEntered more than 10 rooms. This may be too slow to finish. Enter confirm at the end if you are sure you wish to do this pathfind.", i.ClientMask);
                return true;
            }

            int ms = Environment.TickCount;
            PathFinder_VisitAll pf = new PathFinder_VisitAll(this, rooms.ToArray());
            FillPathFinder(pf);
            PathfindResult pr = Get(pf);
            ms = Environment.TickCount - ms;

            World.Instance.SendMessage("@wPathfind took @W" + ms + " @wms.", i.ClientMask);
            if(!pr.Success)
            {
                World.Instance.SendMessage("@wPathfind failed.", i.ClientMask);
                return true;
            }

            string sw = PathfindResult.Speedwalk(pr.Path);
            World.Instance.SendMessage("@wCost: @W" + pr.Cost, i.ClientMask);
            World.Instance.SendMessage("@wSW: " + sw, i.ClientMask);
            return true;
        }

        private bool CommandImport(InputData i)
        {
            if(!i.Arguments.Success)
            {
                World.Instance.SendMessage("@wSyntax: map import <filename>", i.ClientMask);
                return true;
            }

            StreamReader f;
            try
            {
                f = new StreamReader(i.Arguments.Groups[1].Value);
            }
            catch
            {
                World.Instance.SendMessage("@wFailed opening file. Make sure it is in the same folder as ProxyMud.exe and make sure you entered the file extension too.", i.ClientMask);
                return true;
            }

            string l;
            while((l = f.ReadLine()) != null)
            {
                if(l.StartsWith("{area}"))
                {
                    l = l.Substring(6);
                    string key = l.Substring(0, l.IndexOf('\t')).Trim().ToLower();
                    l = l.Substring(l.IndexOf('\t') + 1);

                    if(string.IsNullOrEmpty(key))
                        continue;

                    if(GetArea(key) == null)
                    {
                        Area a = new Area(++_guidArea);
                        a.Name = l;
                        a.Keyword = key;
                        IAreas[a.Entry] = a;
                    }
                    else if(string.IsNullOrEmpty(GetArea(key).Name))
                        GetArea(key).Name = l;
                }
                else if(l.StartsWith("{room}"))
                {
                    l = l.Substring(6);
                    string key = l.Substring(0, l.IndexOf('\t'));
                    uint k;
                    if(!uint.TryParse(key, out k))
                        continue;

                    l = l.Substring(l.IndexOf('\t') + 1);
                    string name = l.Substring(0, l.IndexOf('\t'));

                    l = l.Substring(l.IndexOf('\t') + 1).Trim().ToLower();
                    Area a = GetArea(l);
                    if(a == null)
                    {
                        a = new Area(++_guidArea);
                        a.Keyword = l;
                        IAreas[a.Entry] = a;
                    }

                    Room r = GetRoom(k);
                    if(r == null)
                    {
                        r = new Room(k);
                        IRooms[k] = r;
                        r.Name = name;
                        r.Area = a;
                    }
                    else
                    {
                        if(r.Area != a)
                            r.Area = a;
                        r.Name = name;
                    }
                }
                else if(l.StartsWith("{exit}"))
                {
                    l = l.Substring(6);
                    string cmd = l.Substring(0, l.IndexOf('\t'));
                    l = l.Substring(l.IndexOf('\t') + 1);

                    string fr = l.Substring(0, l.IndexOf('\t'));
                    l = l.Substring(l.IndexOf('\t') + 1);

                    string tr = l.Substring(0, l.IndexOf('\t'));
                    l = l.Substring(l.IndexOf('\t') + 1);

                    uint from;
                    uint to;
                    int minLevel;

                    if(!uint.TryParse(fr, out from) || !uint.TryParse(tr, out to) || !int.TryParse(l, out minLevel))
                        continue;

                    Room fromroom = GetRoom(from);
                    Room toroom = GetRoom(to);
                    if(fromroom == null || toroom == null)
                        continue;

                    string door = "";
                    if(cmd.Contains(';'))
                    {
                        door = cmd.Substring(0, cmd.IndexOf(';'));
                        if(!door.StartsWith("open ") && !door.StartsWith("ope ") && !door.StartsWith("op ") &&
                            !door.StartsWith("o "))
                            door = "";
                        else
                        {
                            door = door.Substring(door.IndexOf(' ') + 1).Trim();
                            char dir = PathfindResult.IsDirectionCommand(door);
                            if(dir == 'x')
                                door = "open " + door;
                            else
                                door = "o";
                            cmd = cmd.Substring(cmd.IndexOf(';') + 1);
                        }
                    }

                    Exit e = null;
                    if(PathfindResult.IsDirectionCommand(cmd) != 'x')
                        e = fromroom.GetExit(PathfindResult.IsDirectionCommand(cmd));
                    else
                        e = fromroom.GetExit(cmd);
                    if(e == null)
                    {
                        e = new Exit(++_guidExit);
                        e.Command = PathfindResult.IsDirectionCommand(cmd) != 'x' ? PathfindResult.IsDirectionCommand(cmd).ToString() : cmd;
                        e.From = fromroom;
                        e.To = toroom;
                        e.From.exits.Add(e);
                        e.From.UpdateExits();
                        IExits[e.Entry] = e;

                        if(door == "o")
                            e.AddFlag("door");
                        else if(!string.IsNullOrEmpty(door))
                            e.DoorCommand = door;
                    }
                }
                else if(l.Length == 0)
                    continue;
                else
                {
                    f.Close();
                    World.Instance.SendMessage("@wInvalid mapper format. You must convert it to readable format with the converter.", i.ClientMask);
                    return true;
                }
            }

            f.Close();
            World.Instance.SendMessage("@wDone importing missing rooms / areas / exits from MUSH database.", i.ClientMask);
            return true;
        }

        private bool CommandSave(InputData i)
        {
            string fileName = DBFileName;
            if(i.Arguments.Success)
                fileName = i.Arguments.Groups[0].Value;

            Save(fileName);
            World.Instance.SendMessage("@wSaved mapper database to '@W" + fileName + "@w'.", i.ClientMask);
            return true;
        }

        private bool CommandUnmapped(InputData i)
        {
            if(!i.Arguments.Success)
            {
                Room r = GetRoom(CurrentRoomId);
                if(r == null)
                {
                    World.Instance.SendMessage("@wYou are in an unkown room.", i.ClientMask);
                    return true;
                }

                uint c = 0;
                foreach(KeyValuePair<uint, Room> x in IRooms)
                {
                    if(x.Value.Area.Entry != r.Area.Entry)
                        continue;

                    bool f = false;
                    foreach(Exit e in x.Value.exits)
                    {
                        if(e.To.Area.Entry == uint.MaxValue)
                        {
                            f = true;
                            break;
                        }
                    }

                    if(f)
                        c++;
                }

                if(c == 0)
                {
                    World.Instance.SendMessage("@wDidn't find any rooms with exits to unmapped rooms in this area.",
                                               i.ClientMask);
                }
                else
                {
                    World.Instance.SendMessage(
                        "@wFound @C" + c + " @wroom" + (c != 1 ? "s" : "") +
                        " in this area that have exits to unmapped rooms.", i.ClientMask);
                }
                World.Instance.SendMessage("@wUse '@Wmap unmapped all@w' to see areas where exits to unmapped rooms are found.", i.ClientMask);
                World.Instance.SendMessage("@wUse '@Wmap unmapped go@w' to go to the closest room where an exit to unmapped room is found (in current area only).", i.ClientMask);
                return true;
            }

            if(i.Arguments.Groups[1].Value.ToLower() == "all")
            {
                SortedDictionary<string, Dictionary<Area, int>> Unmapped = new SortedDictionary<string, Dictionary<Area, int>>();
                foreach(KeyValuePair<uint, Room> x in IRooms)
                {
                    bool f = false;
                    foreach(Exit e in x.Value.exits)
                    {
                        if(e.To.Area.Entry == uint.MaxValue)
                        {
                            f = true;
                            break;
                        }
                    }

                    if(f)
                    {
                        string n = x.Value.Area.Name;
                        if(string.IsNullOrEmpty(n))
                            n = x.Value.Area.Keyword;
                        if(!Unmapped.ContainsKey(n))
                            Unmapped[n] = new Dictionary<Area, int>();
                        if(!Unmapped[n].ContainsKey(x.Value.Area))
                            Unmapped[n][x.Value.Area] = 1;
                        else
                            Unmapped[n][x.Value.Area]++;
                    }
                }

                if(Unmapped.Count == 0)
                    World.Instance.SendMessage("@wDidn't find any rooms in any area that have exits to unmapped rooms.", i.ClientMask);
                else
                {
                    World.Instance.SendMessage("@WEntry Area                                         Unmapped rooms", i.ClientMask);
                    World.Instance.SendMessage("@G===== ============================================ ==============", i.ClientMask);
                    int c = 0;
                    foreach(KeyValuePair<string, Dictionary<Area, int>> x in Unmapped)
                    {
                        foreach(KeyValuePair<Area, int> y in x.Value)
                        {
                            c++;
                            World.Instance.SendMessage("@Y" + string.Format("{0,-5}", y.Key.Entry) + " @M" + string.Format("{0,-" + "============================================".Length + "}", (!string.IsNullOrEmpty(y.Key.Name) ? y.Key.Name : y.Key.Keyword)) + " @C" + y.Value, i.ClientMask);
                        }
                    }

                    World.Instance.SendMessage("@wFound @C" + c + " @warea" + (c != 1 ? "s" : "") + " with exits to unmapped rooms.", i.ClientMask);
                }
                return true;
            }

            if(GetRoom(CurrentRoomId) == null)
            {
                World.Instance.SendMessage("@wYou are in an invalid room.", i.ClientMask);
                return true;
            }

            Pathfinder_Unmapped p = new Pathfinder_Unmapped(false);
            FillPathFinder(p);
            PathfindResult pr = Get(p);
            if(!pr.Success)
            {
                World.Instance.SendMessage("@wCouldn't find a path or there weren't any rooms in this area with exits that lead to unmapped rooms.", i.ClientMask);
                return true;
            }
            Goto(pr);
            return true;
        }

        private bool CommandUnreconed(InputData i)
        {
            if(!i.Arguments.Success)
            {
                Room r = GetRoom(CurrentRoomId);
                if(r == null)
                {
                    World.Instance.SendMessage("@wYou are in an unknown room.", i.ClientMask);
                    return true;
                }

                uint c = 0;
                foreach(KeyValuePair<uint, Room> x in IRooms)
                {
                    if(x.Value.Area.Entry != r.Area.Entry)
                        continue;

                    bool f = false;
                    foreach(Exit e in x.Value.exits)
                    {
                        if(e.To.Area.Entry == uint.MaxValue)
                        {
                            f = true;
                            break;
                        }
                    }
                    if(!f && !x.Value.HasCustomFlag("reconed"))
                        f = true;

                    if(f)
                        c++;
                }

                if(c == 0)
                {
                    World.Instance.SendMessage("@wDidn't find any rooms with exits to unmapped rooms or unreconed in this area.",
                                               i.ClientMask);
                }
                else
                {
                    World.Instance.SendMessage(
                        "@wFound @C" + c + " @wroom" + (c != 1 ? "s" : "") +
                        " in this area that have exits to unmapped rooms or aren't reconed.", i.ClientMask);
                }
                World.Instance.SendMessage("@wUse '@Wmap unreconed all@w' to see areas where exits to unmapped rooms or rooms that aren't reconed are found.", i.ClientMask);
                World.Instance.SendMessage("@wUse '@Wmap unreconed go@w' to go to the closest room where an exit to unmapped room is found or that isn't reconed (in current area only).", i.ClientMask);
                return true;
            }

            if(i.Arguments.Groups[1].Value.ToLower() == "all")
            {
                SortedDictionary<string, Dictionary<Area, int>> Unmapped = new SortedDictionary<string, Dictionary<Area, int>>();
                foreach(KeyValuePair<uint, Room> x in IRooms)
                {
                    bool f = false;
                    foreach(Exit e in x.Value.exits)
                    {
                        if(e.To.Area.Entry == uint.MaxValue)
                        {
                            f = true;
                            break;
                        }
                    }
                    if(!f && !x.Value.HasCustomFlag("reconed"))
                        f = true;

                    if(f)
                    {
                        string n = x.Value.Area.Name;
                        if(string.IsNullOrEmpty(n))
                            n = x.Value.Area.Keyword;
                        if(!Unmapped.ContainsKey(n))
                            Unmapped[n] = new Dictionary<Area, int>();
                        if(!Unmapped[n].ContainsKey(x.Value.Area))
                            Unmapped[n][x.Value.Area] = 1;
                        else
                            Unmapped[n][x.Value.Area]++;
                    }
                }

                if(Unmapped.Count == 0)
                    World.Instance.SendMessage("@wDidn't find any rooms in any area that have exits to unmapped rooms or that aren't reconed.", i.ClientMask);
                else
                {
                    World.Instance.SendMessage("@WEntry Area                                         Unmapped/Unreconed rooms", i.ClientMask);
                    World.Instance.SendMessage("@G===== ============================================ ========================", i.ClientMask);
                    int c = 0;
                    foreach(KeyValuePair<string, Dictionary<Area, int>> x in Unmapped)
                    {
                        foreach(KeyValuePair<Area, int> y in x.Value)
                        {
                            c++;
                            World.Instance.SendMessage("@Y" + string.Format("{0,-5}", y.Key.Entry) + " @M" + string.Format("{0,-" + "============================================".Length + "}", (!string.IsNullOrEmpty(y.Key.Name) ? y.Key.Name : y.Key.Keyword)) + " @C" + y.Value, i.ClientMask);
                        }
                    }

                    World.Instance.SendMessage("@wFound @C" + c + " @warea" + (c != 1 ? "s" : "") + " with exits to unmapped rooms or unreconed rooms.", i.ClientMask);
                }
                return true;
            }

            if(GetRoom(CurrentRoomId) == null)
            {
                World.Instance.SendMessage("@wYou are in an invalid room.", i.ClientMask);
                return true;
            }

            Pathfinder_Unmapped p = new Pathfinder_Unmapped(true);
            FillPathFinder(p);
            PathfindResult pr = Get(p);
            if(!pr.Success)
            {
                World.Instance.SendMessage("@wCouldn't find a path or there weren't any rooms in this area with exits that lead to unmapped rooms or that aren't reconed.", i.ClientMask);
                return true;
            }
            Goto(pr);
            return true;
        }

        private bool CommandDelete(InputData i)
        {
            // @"^(area|room|exit|portal)\s+(\d+)"
            uint id;
            if(!i.Arguments.Success || !uint.TryParse(i.Arguments.Groups[2].Value, out id) || id == uint.MaxValue)
            {
                World.Instance.SendMessage("@wSyntax: map delete area <Id>", i.ClientMask);
                World.Instance.SendMessage("        @wmap delete exit <Id>", i.ClientMask);
                World.Instance.SendMessage("        @wmap delete portal <Id>", i.ClientMask);
                World.Instance.SendMessage("        @wmap delete room <Id>", i.ClientMask);
                return true;
            }

            string w = i.Arguments.Groups[1].Value.ToLower();
            
            switch(w)
            {
                case "area":
                    {
                        Area a = GetArea(id);
                        if(a == null)
                        {
                            World.Instance.SendMessage("@wNo such area (@R" + id + "@w).", i.ClientMask);
                            return true;
                        }

                        IAreas.Remove(a.Entry);
                        foreach(Room r in a.rooms)
                        {
                            IRooms.Remove(r.Entry);
                            foreach(Exit e in r.exits)
                                IExits.Remove(e.Entry);
                            OnDeleted(r);
                        }

                        if(!string.IsNullOrEmpty(a.Name))
                            World.Instance.SendMessage("@wDeleted area '@M" + a.Name + "@w'.", i.ClientMask);
                        else
                            World.Instance.SendMessage("@wDeleted area '@w" + a.Keyword + "@w'.", i.ClientMask);
                    } break;

                case "exit":
                case "portal":
                    {
                        Exit e = GetExit(id);
                        if(e == null)
                            e = IPortals.ContainsKey(id) ? IPortals[id] : null;
                        if(e == null)
                        {
                            World.Instance.SendMessage("@wNo such exit or portal (@R" + id + "@w).", i.ClientMask);
                            return true;
                        }

                        IExits.Remove(e.Entry);
                        IPortals.Remove(e.Entry);
                        if(!e.HasFlag("portal"))
                            e.From.exits.Remove(e);
                        else
                            e.To.Area.Portals.Remove(e);

                        World.Instance.SendMessage("@wDeleted exit '@Y" + e.Entry + "@w'.", i.ClientMask);
                    } break;

                case "room":
                    {
                        Room r = GetRoom(id);
                        if(r == null)
                        {
                            World.Instance.SendMessage("@wNo such room (@R" + id + "@w).", i.ClientMask);
                            return true;
                        }

                        IRooms.Remove(r.Entry);
                        r.Area.rooms.Remove(r);
                        OnDeleted(r);
                        World.Instance.SendMessage("@wDeleted room '@G" + r.Name + "@w'.", i.ClientMask);
                    } break;
            }

            return true;
        }

        private bool CommandFind(InputData i)
        {
            // @"^(room|area)(\s+exact)?(\s+case)?\s+(.+)"
            if(!i.Arguments.Success)
            {
                World.Instance.SendMessage("@wSyntax: map find room [exact] [case] <name>", i.ClientMask);
                World.Instance.SendMessage("        @wmap find area [exact] [case] <name>", i.ClientMask);
                return true;
            }

            if(i.Arguments.Groups[1].Value.ToLower() == "room")
            {
                string Str = i.Arguments.Groups[4].Value;
                if(i.Arguments.Groups[3].Length == 0)
                    Str = Str.ToLower();
                List<Room> Found = new List<Room>();
                foreach(KeyValuePair<uint, Room> x in IRooms)
                {
                    string Name = x.Value.Name;
                    if(string.IsNullOrEmpty(Name))
                        continue;
                    if(i.Arguments.Groups[3].Length == 0)
                        Name = Name.ToLower();

                    if(i.Arguments.Groups[2].Length != 0)
                    {
                        if(Name == Str)
                            Found.Add(x.Value);
                    }
                    else if(Name.Contains(Str))
                        Found.Add(x.Value);
                }

                if(Found.Count == 0)
                    World.Instance.SendMessage("@wFound nothing!", i.ClientMask);
                else
                {
                    World.Instance.SendMessage("@WEntry  Name                             Area", i.ClientMask);
                    World.Instance.SendMessage("@G====== ================================ ===============================", i.ClientMask);
                    foreach(Room f in Found)
                    {
                        World.Instance.SendMessage("@Y" + string.Format("{0,6}", f.Entry) + " @G" + Utility.FormatColoredString(f.Name, -"================================".Length) + " " + (!string.IsNullOrEmpty(f.Area.Name) ? ("@M" + f.Area.Name) : ("@w" + f.Area.Keyword)), i.ClientMask);
                    }
                    World.Instance.SendMessage("@wFound @C" + Found.Count + " @wroom" + (Found.Count != 1 ? "s" : "") + ".", i.ClientMask);
                }
            }
            else
            {
                string Str = i.Arguments.Groups[4].Value;
                if(i.Arguments.Groups[3].Length == 0)
                    Str = Str.ToLower();
                List<Area> Found = new List<Area>();
                foreach(KeyValuePair<uint, Area> x in IAreas)
                {
                    string Name = x.Value.Name;
                    if(string.IsNullOrEmpty(Name))
                        continue;
                    if(i.Arguments.Groups[3].Length == 0)
                        Name = Name.ToLower();

                    if(i.Arguments.Groups[2].Length != 0)
                    {
                        if(Name == Str)
                            Found.Add(x.Value);
                    }
                    else if(Name.Contains(Str))
                        Found.Add(x.Value);
                }

                if(Found.Count == 0)
                    World.Instance.SendMessage("@wFound nothing!", i.ClientMask);
                else
                {
                    World.Instance.SendMessage("@WEntry  Keyword    Name", i.ClientMask);
                    World.Instance.SendMessage("@G====== ========== ========================================", i.ClientMask);
                    foreach(Area f in Found)
                    {
                        World.Instance.SendMessage("@Y" + string.Format("{0,6}", f.Entry) + " @w" + Utility.FormatColoredString(f.Keyword, -10) + " @M" + f.Name, i.ClientMask);
                    }
                    World.Instance.SendMessage("@wFound @C" + Found.Count + " @warea" + (Found.Count != 1 ? "s" : "") + ".", i.ClientMask);
                }
            }
            return true;
        }

        private bool CommandMapper(InputData i)
        {
            World.Instance.SendMessage("@wAvailable mapper commands:", i.ClientMask);
            World.Instance.SendMessage("@Y" + string.Format("{0,-20}", "map all") + " @w- Shows speedwalk that runs through all the rooms (IDs) you entered starting from your current location.", i.ClientMask);
            World.Instance.SendMessage("@Y" + string.Format("{0,-20}", "map count") + " @w- Shows how many rooms you have mapped.", i.ClientMask);
            World.Instance.SendMessage("@Y" + string.Format("{0,-20}", "map createexit") + " @w- Create a new exit.", i.ClientMask);
            World.Instance.SendMessage("@Y" + string.Format("{0,-20}", "map createportal") + " @w- Create a new portal.", i.ClientMask);
            World.Instance.SendMessage("@Y" + string.Format("{0,-20}", "map delete") + " @w- Delete a room, area or an exit.", i.ClientMask);
            World.Instance.SendMessage("@Y" + string.Format("{0,-20}", "map exits") + " @w- Show exits information or edit them.", i.ClientMask);
            World.Instance.SendMessage("@Y" + string.Format("{0,-20}", "map find") + " @w- Find rooms or areas in mapper.", i.ClientMask);
            World.Instance.SendMessage("@Y" + string.Format("{0,-20}", "map goto") + " @w- Goto a room or an area.", i.ClientMask);
            World.Instance.SendMessage("@Y" + string.Format("{0,-20}", "map import") + " @w- Import data file converted from mush mapper.", i.ClientMask);
            World.Instance.SendMessage("@Y" + string.Format("{0,-20}", "map portals") + " @w- List all portals.", i.ClientMask);
            World.Instance.SendMessage("@Y" + string.Format("{0,-20}", "map roominfo") + " @w- Show more information about a room or edit it.", i.ClientMask);
            World.Instance.SendMessage("@Y" + string.Format("{0,-20}", "map save") + " @w- Save the mapper database now. Enter argument to save to another file.", i.ClientMask);
            World.Instance.SendMessage("@Y" + string.Format("{0,-20}", "map unmapped") + " @w- Find unmapped rooms.", i.ClientMask);
            World.Instance.SendMessage("@Y" + string.Format("{0,-20}", "map unreconed") + " @w- Find unmapped or unreconed rooms.", i.ClientMask);
            return true;
        }

        private bool CommandGoto(InputData i)
        {
            if(!i.Arguments.Success || i.Arguments.Groups[1].Value.Trim().Length == 0)
            {
                World.Instance.SendMessage("@wGo to the closest room entered.", i.ClientMask);
                World.Instance.SendMessage("@wSyntax: map goto <room id> [room id] [room id]", i.ClientMask);
                World.Instance.SendMessage("          @wmap goto <partial room name>", i.ClientMask);
                World.Instance.SendMessage("          @wmap goto <area keyword>", i.ClientMask);
                return true;
            }

            if(CurrentRoomId == uint.MaxValue || GetRoom(CurrentRoomId) == null)
            {
                World.Instance.SendMessage("@wWe are in an unknown room.", i.ClientMask);
                return true;
            }

            List<uint> roomId = new List<uint>();
            Area aTarget = null;
            string[] testIds = i.Arguments.Groups[1].Value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach(string x in testIds)
            {
                uint u;
                if(uint.TryParse(x, out u))
                {
                    if(GetRoom(u) == null)
                    {
                        World.Instance.SendMessage("@wNo such room (@R" + u + "@w)!", i.ClientMask);
                        continue;
                    }
                    if(!roomId.Contains(u))
                        roomId.Add(u);
                }
                else
                {
                    roomId = null;
                    break;
                }
            }

            if(roomId == null)
                aTarget = GetArea(i.Arguments.Groups[1].Value);

            Pathfinder e;
            if(roomId != null)
                e = new Pathfinder_Entry(roomId.ToArray());
            else if(aTarget != null)
            {
                if(aTarget.StartRoom != 0)
                    e = new Pathfinder_Entry(aTarget.StartRoom);
                else
                    e = new Pathfinder_Area(aTarget.Entry);
            }
            else
                e = new Pathfinder_Name(NameTypes.Partial | NameTypes.CaseInsensitive, i.Arguments.Groups[1].Value);

            FillPathFinder(e);
            PathfindResult pr = Get(e);
            if(!pr.Success)
            {
                World.Instance.SendMessage("@wCouldn't find a path to any of the rooms entered.", i.ClientMask);
                return true;
            }

            if(aTarget != null)
                World.Instance.SendMessage("@wGoing to area '@C" + pr.Target.Area.Name + "@w'...", i.ClientMask);
            else
                World.Instance.SendMessage("@wGoing to room '@G" + pr.Target.Name + "@w'...", i.ClientMask);

            Goto(pr);
            return true;
        }

        private bool CommandCount(InputData i)
        {
            int thisArea = 0;
            int total = 0;
            foreach(KeyValuePair<uint, Room> x in IRooms)
            {
                if(x.Value.Area.Keyword == RoomInfoArea)
                    thisArea++;
                if(x.Value.Area.Entry != uint.MaxValue)
                    total++;
            }
            World.Instance.SendMessage("@wYou have mapped @G" + thisArea + " @wrooms in this area.", i.ClientMask);
            World.Instance.SendMessage("@wYou have @G" + (IAreas.Count - 1) + " @wareas in mapper.", i.ClientMask);
            World.Instance.SendMessage("@wYou have mapped @G" + total + " @wrooms of Aardwolf.", i.ClientMask);
            return true;
        }

        #endregion

        #region Triggers
        private bool TriggerNoRecon(TriggerData t)
        {
            Room r = GetRoom(CurrentRoomId);
            if(r != null)
            {
                r.AddCustomFlag("reconed");
                r.AddCustomFlag("norecon");
            }
            return false;
        }

        private bool TriggerTagsExits(TriggerData t)
        {
            if(Config.GetInt32("Tags.Exits", 1) == 0)
                return false;
            
            Room r = GetRoom(CurrentRoomId);
            
            StringBuilder str = new StringBuilder();
            string[] Exits = t.Match.Groups[2].Value.ToLower().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            CheckExit("north", 'n', Exits.Contains("north") || Exits.Contains("(north)"), Exits.Contains("(north)"), RoomInfoExits.ContainsKey('n') ? RoomInfoExits['n'] : 0, str);
            CheckExit("east", 'e', Exits.Contains("east") || Exits.Contains("(east)"), Exits.Contains("(east)"), RoomInfoExits.ContainsKey('e') ? RoomInfoExits['e'] : 0, str);
            CheckExit("south", 's', Exits.Contains("south") || Exits.Contains("(south)"), Exits.Contains("(south)"), RoomInfoExits.ContainsKey('s') ? RoomInfoExits['s'] : 0, str);
            CheckExit("west", 'w', Exits.Contains("west") || Exits.Contains("(west)"), Exits.Contains("(west)"), RoomInfoExits.ContainsKey('w') ? RoomInfoExits['w'] : 0, str);
            CheckExit("up", 'u', Exits.Contains("up") || Exits.Contains("(up)"), Exits.Contains("(up)"), RoomInfoExits.ContainsKey('u') ? RoomInfoExits['u'] : 0, str);
            CheckExit("down", 'd', Exits.Contains("down") || Exits.Contains("(down)"), Exits.Contains("(down)"), RoomInfoExits.ContainsKey('d') ? RoomInfoExits['d'] : 0, str);

            if(r != null)
            {
                bool hadCustom = false;
                foreach(Exit e in r.exits)
                {
                    if(PathfindResult.IsDirectionCommand(e.Command) != 'x')
                        continue;

                    hadCustom = true;
                    if(str.Length > 0)
                        str.Append(" ");
                    str.Append("@w'@C" + e.Command + "@w'");
                }

                if(!hadCustom && Exits.Contains("custom"))
                {
                    if(str.Length > 0)
                        str.Append(" ");
                    str.Append("@Rcustom");
                }
            }

            if(str.Length == 0)
                str.Append("@Gnone");
            t.Msg.Msg = "@g[Exits: " + str.ToString() + "@g]";
            return false;
        }

        private void CheckExit(string Word, char Dir, bool HasExits, bool IsClosed, uint LeadTo, StringBuilder str)
        {
            Room r = GetRoom(CurrentRoomId);
            if(r == null)
            {
                if(!HasExits)
                    return;

                if(str.Length > 0)
                    str.Append(" ");
                if(LeadTo == uint.MaxValue)
                    str.Append("@Y");
                else
                    str.Append("@G");
                str.Append(IsClosed ? ("(" + Word + ")") : Word);
                return;
            }

            if(LeadTo == uint.MaxValue)
            {
                if(str.Length > 0)
                    str.Append(" ");
                str.Append("@Y");
                str.Append(IsClosed ? ("(" + Word + ")") : Word);
                return;
            }

            Exit e = r.GetExit(Dir);
            if(e == null)
                return;

            if(!HasExits)
            {
                if(e.HasFlag("hidden"))
                {
                    if(str.Length > 0)
                        str.Append(" ");
                    str.Append("@y(" + Word + ")");
                    return;
                }
                if(str.Length > 0)
                    str.Append(" ");
                str.Append("@D(" + Word + ")");
                return;
            }

            if(str.Length > 0)
                str.Append(" ");
            if(e.To.Area.Entry == uint.MaxValue)
                str.Append("@R");
            else if(!e.To.HasCustomFlag("reconed") && Config.GetInt32("Tags.Exits.Recon", 0) != 0)
                str.Append("@r");
            else
                str.Append("@G");

            if(IsClosed)
                str.Append("(" + Word + ")");
            else
                str.Append(Word);
        }

        private bool TriggerNoRecall(TriggerData t)
        {
            Room r = GetRoom(CurrentRoomId);
            if(r != null)
                r.AddFlag("norecall");
            return false;
        }

        private bool TriggerPrison(TriggerData t)
        {
            Room r = GetRoom(CurrentRoomId);
            if(r != null)
                r.AddFlag("prison");
            return false;
        }

        private bool TriggerReconArea(TriggerData t)
        {
            Room r = GetRoom(CurrentRoomId);
            if(r == null)
                return false;
            string msg = Colors.RemoveColors(t.Match.Groups[1].Value, false);
            string name = msg.Substring(0, msg.LastIndexOf('(')).Trim();
            msg = msg.Substring(msg.LastIndexOf('(') + 1);

            r.Area.Name = name;
            try
            {
                int min, max;
                if(int.TryParse(msg.Substring(0, msg.IndexOf(' ')), out min))
                    r.Area.MinLevel = min;
                msg = msg.Substring(msg.IndexOf("to ") + 3);
                if(int.TryParse(msg.Substring(0, msg.IndexOf(' ')), out max))
                    r.Area.MaxLevel = max;
            }
            catch
            {
            }
            r.AddCustomFlag("reconed");
            return false;
        }

        private bool TriggerReconSector(TriggerData t)
        {
            Room r = GetRoom(CurrentRoomId);
            if(r == null)
                return false;

            r.Sector = Colors.RemoveColors(t.Match.Groups[1].Value, false);
            return false;
        }

        private bool TriggerReconHealRate(TriggerData t)
        {
            Room r = GetRoom(CurrentRoomId);
            if(r == null)
                return false;

            int rate;
            if(int.TryParse(t.Match.Groups[1].Value, out rate))
                r.HealRate = rate;
            return false;
        }

        private bool TriggerReconManaRate(TriggerData t)
        {
            Room r = GetRoom(CurrentRoomId);
            if(r == null)
                return false;

            int rate;
            if(int.TryParse(t.Match.Groups[1].Value, out rate))
                r.ManaRate = rate;
            return false;
        }

        private bool TriggerReconFlags(TriggerData t)
        {
            Room r = GetRoom(CurrentRoomId);
            if(r == null)
                return false;

            string[] fl = Colors.RemoveColors(t.Match.Groups[1].Value, false).Trim().ToLower().Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if(r.IFlags != null)
                r.IFlags.Clear();
            foreach(string x in fl)
                r.AddFlag(x.Trim());
            return false;
        }

        private bool TriggerAreasStart(TriggerData t)
        {
            ListenArea = true;
            return false;
        }

        private bool TriggerAreasEnd(TriggerData t)
        {
            ListenArea = false;
            return false;
        }

        private bool TriggerAreasEntry(TriggerData t)
        {
            if(!ListenArea)
                return false;

            int minLevel, maxLevel, levelLock = 0;

            if(!int.TryParse(t.Match.Groups[1].Value, out minLevel) ||
                !int.TryParse(t.Match.Groups[2].Value, out maxLevel))
                return false;

            if(t.Match.Groups[3].Length != 0)
            {
                if(!int.TryParse(t.Match.Groups[3].Value, out levelLock))
                    levelLock = 0;
            }

            string keyWord = t.Match.Groups[4].Value;
            string areaName = t.Match.Groups[5].Value.Trim();

            Area a = GetArea(keyWord);
            if(a == null)
            {
                a = new Area(++_guidArea);
                IAreas[a.Entry] = a;
            }

            a.Keyword = keyWord;
            a.LevelLock = levelLock;
            a.MaxLevel = maxLevel;
            a.MinLevel = minLevel;
            if(string.IsNullOrEmpty(a.Name) || a.Name.Length <= areaName.Length || !a.Name.StartsWith(areaName))
                a.Name = areaName;
            return false;
        }

        private bool TriggerWhereLevel(TriggerData t)
        {
            int minLevel, maxLevel;
            if(!int.TryParse(t.Match.Groups[1].Value, out minLevel) ||
                !int.TryParse(t.Match.Groups[2].Value, out maxLevel))
                return false;

            Room cur = GetRoom(CurrentRoomId);
            if(cur != null)
            {
                cur.Area.MinLevel = minLevel;
                cur.Area.MaxLevel = maxLevel;
            }
            return false;
        }

        private bool TriggerWhereName(TriggerData t)
        {
            string aName = Colors.RemoveColors(t.Match.Groups[1].Value, false).Trim();
            Room cur = GetRoom(CurrentRoomId);
            if(cur != null)
                cur.Area.Name = aName;
            return false;
        }

        private bool TriggerJoinedGQ(TriggerData t)
        {
            HasGQ = true;
            return false;
        }

        private bool TriggerLeftGQ(TriggerData t)
        {
            HasGQ = false;
            return false;
        }

        private bool TriggerCharStatusLevel(TriggerData t)
        {
            int i;
            if(int.TryParse(t.Match.Groups[1].Value, out i))
                Level = i;
            return false;
        }

        private bool TriggerCharBaseTier(TriggerData t)
        {
            int i;
            if(int.TryParse(t.Match.Groups[1].Value, out i))
                Tier = i;
            return false;
        }

        private bool TriggerCharBaseRemorts(TriggerData t)
        {
            int i;
            if(int.TryParse(t.Match.Groups[1].Value, out i))
                Remorts = i;
            return false;
        }

        private bool TriggerRoomInfoNum(TriggerData t)
        {
            RoomInfoPrevious = RoomInfoEntry;
            RoomInfoListen = true;
            RoomInfoExits.Clear();
            uint i;
            if(!uint.TryParse(t.Match.Groups[1].Value, out i))
            {
                RoomInfoEntry = uint.MaxValue;
                return false;
            }

            RoomInfoEntry = i;
            return false;
        }

        private bool TriggerRoomInfoName(TriggerData t)
        {
            RoomInfoName = Colors.RemoveColors(t.Match.Groups[1].Value, true).Trim();
            return false;
        }

        private bool TriggerRoomInfoArea(TriggerData t)
        {
            RoomInfoArea = t.Match.Groups[1].Value.Trim();
            return false;
        }

        private bool TriggerRoomInfoExits(TriggerData t)
        {
            uint exitId;
            RoomInfoExits[t.Match.Groups[1].Value.ToLower()[0]] = !uint.TryParse(t.Match.Groups[2].Value, out exitId)
                                                            ? uint.MaxValue
                                                            : exitId;
            return false;
        }

        private bool TriggerRoomInfoFinish(TriggerData t)
        {
            if(!RoomInfoListen)
                return false;

            RoomInfoListen = false;
            UpdateRoom();
            return false;
        }

        #endregion

        #region Data
        internal Dictionary<uint, Area> IAreas = new Dictionary<uint, Area>();
        internal Dictionary<uint, Room> IRooms = new Dictionary<uint, Room>();
        internal Dictionary<uint, Exit> IExits = new Dictionary<uint, Exit>();
        internal Dictionary<uint, Exit> IPortals = new Dictionary<uint, Exit>();

        /// <summary>
        /// Collection of all areas.
        /// </summary>
        public IEnumerable<Area> Areas
        {
            get
            {
                return IAreas.Values;
            }
        }

        /// <summary>
        /// Collection of all rooms.
        /// </summary>
        public IEnumerable<Room> Rooms
        {
            get
            {
                return IRooms.Values;
            }
        }

        /// <summary>
        /// Collection of all exits.
        /// </summary>
        public IEnumerable<Exit> Exits
        {
            get
            {
                return IExits.Values;
            }
        }

        /// <summary>
        /// Collection of all portals.
        /// </summary>
        public IEnumerable<Exit> Portals
        {
            get
            {
                return IPortals.Values;
            }
        }

        private uint _guidArea = 0;
        private uint _guidExit = 0;

        private void UpdateRoom()
        {
            CurrentRoomId = RoomInfoEntry;

            if(RoomInfoEntry == uint.MaxValue)
            {
                if(CurrentRoomId != RoomInfoPrevious)
                {
                    Room prev = GetRoom(RoomInfoPrevious);
                    if(prev != null)
                    {
                        GetScript(prev).OnLeaveRoom(prev, null);
                        GetScript(prev).OnLeaveArea(prev.Area, null);
                    }
                }
                return;
            }

            Room r = null;
            Area a = null;
            if(!IRooms.ContainsKey(CurrentRoomId))
            {
                r = new Room(CurrentRoomId);
                IRooms[r.Entry] = r;
            }
            else
                r = IRooms[CurrentRoomId];

            a = GetArea(RoomInfoArea);
            if(a == null)
            {
                a = new Area(++_guidArea);
                IAreas[a.Entry] = a;
                a.Keyword = RoomInfoArea;
            }

            if(r.Area != a)
                r.Area = a;

            r.Name = RoomInfoName;
            bool u = false;
            foreach(KeyValuePair<char, uint> e in RoomInfoExits)
            {
                UpdateRoom(e.Value);
                if(e.Value == uint.MaxValue)
                    continue;
                Exit prev = r.GetExit(e.Key);
                if(prev != null && prev.ToRoom == e.Value)
                    continue;
                if(prev != null)
                    r.exits.Remove(prev);
                Exit newExit = new Exit(++_guidExit);
                newExit.To = GetRoom(e.Value);
                newExit.Command = e.Key.ToString();
                newExit.From = r;
                r.exits.Add(newExit);
                IExits[newExit.Entry] = newExit;
                u = true;
            }

            if(u)
                r.UpdateExits();

            if(CurrentRoomId != RoomInfoPrevious)
            {
                Room prev = GetRoom(RoomInfoPrevious);
                if(prev != null)
                {
                    GetScript(prev).OnLeaveRoom(prev, r);
                    GetScript(prev).OnLeaveArea(prev.Area, r.Area);
                }
                GetScript(r).OnEnterArea(r.Area, prev != null ? prev.Area : null);
                GetScript(r).OnEnterRoom(r, prev);
            }
        }

        private void UpdateRoom(uint Entry)
        {
            if(Entry == uint.MaxValue)
                return;

            Room r = GetRoom(Entry);
            if(r == null)
            {
                r = new Room(Entry);
                IRooms[r.Entry] = r;
                r.Area = GetArea(uint.MaxValue);
            }
        }

        internal uint CurrentRoomId;
        private uint RoomInfoPrevious = uint.MaxValue;
        private uint RoomInfoEntry = uint.MaxValue;
        private string RoomInfoName;
        private string RoomInfoArea;
        private bool RoomInfoListen;
        private Dictionary<char, uint> RoomInfoExits = new Dictionary<char, uint>();

        /// <summary>
        /// Get room by ID.
        /// </summary>
        /// <param name="Entry">ID of the room to get.</param>
        /// <returns></returns>
        public Room GetRoom(uint Entry)
        {
            return IRooms.ContainsKey(Entry) ? IRooms[Entry] : null;
        }

        /// <summary>
        /// Get exit by ID.
        /// </summary>
        /// <param name="Entry">ID of the exit to get.</param>
        /// <returns></returns>
        public Exit GetExit(uint Entry)
        {
            return IExits.ContainsKey(Entry) ? IExits[Entry] : null;
        }

        /// <summary>
        /// Get area by ID.
        /// </summary>
        /// <param name="Entry">ID of the area to get.</param>
        /// <returns></returns>
        public Area GetArea(uint Entry)
        {
            return IAreas.ContainsKey(Entry) ? IAreas[Entry] : null;
        }

        /// <summary>
        /// Get area by keyword.
        /// </summary>
        /// <param name="Keyword">Keyword of area to get.</param>
        /// <returns></returns>
        public Area GetArea(string Keyword)
        {
            foreach(KeyValuePair<uint, Area> x in IAreas)
            {
                if(x.Value.Keyword == Keyword)
                    return x.Value;
            }
            return null;
        }
        #endregion

        public override void Shutdown()
        {
            base.Shutdown();

            Save(DBFileName);
        }

        /// <summary>
        /// Use mapper to go to the end of pathresult.
        /// </summary>
        /// <param name="pr"></param>
        public void Goto(PathfindResult pr)
        {
            string sw = PathfindResult.Speedwalk(pr.Path);
            if(string.IsNullOrEmpty(sw))
                sw = "";

            if(Config.GetInt32("Speedwalk.Echo", 0) != 0)
            {
                World.Instance.SendMessage("@w{mapperpath}" + sw, Config.GetUInt64("Speedwalk.Echo.AuthMask", ulong.MaxValue));
            }
            else if(!string.IsNullOrEmpty(sw))
            {
                string[] swp = sw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach(string x in swp)
                    World.Instance.Execute(x, true);
            }
            else
                World.Instance.SendMessage("@wTrying to execute an empty path. Using goto when you are already at target?");
        }

        #region Saving
        private void Load()
        {
            StreamReader f;
            try
            {
                f = new StreamReader(DBFileName);
            }
            catch
            {
                // No database exists or we aren't allowed to read it. Make a new database.
                return;
            }

            Area[] data;
            try
            {
                DataContractSerializer x = new DataContractSerializer(typeof(Area[]));
                data = x.ReadObject(f.BaseStream) as Area[];
            }
            catch
            {
                f.Close();
                Log.Error("Failed loading mapper database! File corrupted?");
                return;
            }

            f.Close();

            if(data == null)
                return;

            foreach(Area a in data)
            {
                if(a == null)
                    continue;

                IAreas[a.Entry] = a;
                if(a.Entry > _guidArea && a.Entry != uint.MaxValue)
                    _guidArea = a.Entry;
                foreach(Room r in a.rooms)
                {
                    if(r == null)
                        continue;
                    r.Area = a;
                    IRooms[r.Entry] = r;
                    foreach(Exit e in r.exits)
                    {
                        if(e == null)
                            continue;
                        e.From = r;
                        IExits[e.Entry] = e;
                        if(e.Entry > _guidExit)
                            _guidExit = e.Entry;
                    }
                    r.UpdateExits();
                }
                foreach(Exit p in a.Portals)
                {
                    p.To = GetRoom(p.ToRoom);
                    IPortals[p.Entry] = p;
                    if(p.Entry > _guidExit)
                        _guidExit = p.Entry;
                }
            }

            foreach(KeyValuePair<uint, Exit> e in IExits)
            {
                if(IPortals.ContainsKey(e.Key))
                {
                    IPortals[e.Key].To.Area.Portals.Remove(IPortals[e.Key]);
                    IPortals.Remove(e.Key);
                }
            }

            List<Exit> toDelete = new List<Exit>();
            foreach(KeyValuePair<uint, Exit> e in IExits)
            {
                e.Value.To = IRooms.ContainsKey(e.Value.ToRoom) ? IRooms[e.Value.ToRoom] : null;
                if(e.Value.To == null)
                    toDelete.Add(e.Value);
            }

            foreach(Exit e in toDelete)
            {
                IExits.Remove(e.Entry);
                e.From.exits.Remove(e);
            }

            // Successfully loaded a database. Now make a backup because we have a working copy at the moment.
            File.Delete(DBFileBackup);
            File.Copy(DBFileName, DBFileBackup);
        }

        private void Save(string fileName)
        {
            if(IAreas.Count == 0)
                return;
            StreamWriter f = new StreamWriter(fileName, false);

            try
            {
                DataContractSerializer x = new DataContractSerializer(typeof(Area[]));
                x.WriteObject(f.BaseStream, IAreas.Values.ToArray());
            }
            catch(Exception e)
            {
                f.Close();
                throw e;
            }

            f.Close();
            if(Config.GetInt32("AutoSave", 0) != 0)
                WhenSave = World.Instance.MSTime + Config.GetInt32("AutoSave", 0) * 1000;
        }
        #endregion

        public override void Update(long msTime)
        {
            base.Update(msTime);

            if(WhenSave == 0 && Config.GetInt32("AutoSave", 0) != 0)
                WhenSave = Config.GetInt32("AutoSave", 0) * 1000 + msTime;
            else if(WhenSave > 0 && WhenSave <= msTime)
                Save(DBFileName);
        }
    }

    public class MapperConfig : ConfigFile
    {
        protected override void OnCreated()
        {
            base.OnCreated();

            CreateSetting("Tags.Exits", 1, "Display extra information about exits using {exits} tag. You need to have \"tags exits on\".");
            CreateSetting("Tags.Exits.Recon", 0, "Display exits that lead to rooms that haven't been reconed yet in dark red.");
            CreateSetting("Speedwalk.Echo", 0, "Echo speedwalk to clients instead of executing the command when using goto. This is useful if you like your MUD client to process it instead of mapper just sending to MUD, for example portal alias etc.");
            CreateSetting("Speedwalk.Echo.AuthMask", ulong.MaxValue, "Security mask of who to echo the commands if you set Echo to 1.");
            CreateSetting("AutoSave", 0, "Save mapper database every X seconds. For example enter 600 to save mapper database every 10 minutes. Enter 0 to disable this feature. The map is also saved on shutdown of program. You can also type \"map save\" to manually save the database.");
        }
    }
}
