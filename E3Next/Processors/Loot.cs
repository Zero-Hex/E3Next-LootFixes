﻿using E3Core.Data;
using E3Core.Settings;
using E3Core.Settings.FeatureSettings;
using E3Core.Utility;
using MonoCore;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Dynamic;
using System.Linq;

namespace E3Core.Processors
{
    public static class Loot
    {
        public static Logging _log = E3._log;
        private static IMQ MQ = E3.MQ;
        private static ISpawns _spawns = E3._spawns;
        private static bool _shouldLoot = false;
        private static Int32 _seekRadius = 50;
        private static HashSet<Int32> _unlootableCorpses = new HashSet<int>();
        private static bool _fullInventoryAlert = false;
        private static bool _lootOnlyStackable = false;
        private static Int32 _lootOnlyStackableValue = 1;
        private static bool _lootOnlyStackableAllTradeSkils = false;
        private static bool _lootOnlyStackableCommonTradeSkils = false;

        public static void Init()
        {
            RegisterEvents();

            _shouldLoot =E3._characterSettings.Misc_AutoLootEnabled;
            _seekRadius = E3._generalSettings.Loot_CorpseSeekRadius;
            _lootOnlyStackable = E3._generalSettings.Loot_OnlyStackableEnabled;
            _lootOnlyStackableValue = E3._generalSettings.Loot_OnlyStackableValueGreaterThanInCopper;
            _lootOnlyStackableAllTradeSkils = E3._generalSettings.Loot_OnlyStackableAllTradeSkillItems;
            _lootOnlyStackableCommonTradeSkils = E3._generalSettings.Loot_OnlyStackableOnlyCommonTradeSkillItems;
        }

        private static void RegisterEvents()
        {
            EventProcessor.RegisterCommand("/E3LootAdd", (x) =>
            {
                if (x.args.Count > 1)
                {
                    if(x.args[1]=="KEEP")
                    {
                        if (!LootDataFile._keep.Contains(x.args[1]))
                        {
                            LootDataFile._keep.Add(x.args[1]);
                        }
                    }
                    else if(x.args[1]=="SELL")
                    {
                        if (!LootDataFile._sell.Contains(x.args[1]))
                        {
                            LootDataFile._sell.Add(x.args[1]);
                        }
                    }
                    else
                    {
                        if (!LootDataFile._skip.Contains(x.args[1]))
                        {
                            LootDataFile._skip.Add(x.args[1]);
                        }
                    }
                    //E3._bots.BroadcastCommandToOthers($"/E3LootAdd \"{x.args[0]}\" {x.args[1]}");

                }
            });
        }


        private static void LootArea()
        {

            List<Spawn> corpses = new List<Spawn>();
            foreach (var spawn in _spawns.Get())
            { 
                if(spawn.Distance<_seekRadius && spawn.Z<50 && spawn.TypeDesc=="Corpse")
                {
                    corpses.Add(spawn);
                }
            }
            //sort all the corpses, removing the ones we cannot loot
            corpses = corpses.OrderBy(x => x.Distance).Where(x=> !_unlootableCorpses.Contains(x.ID)).ToList();

            if (corpses.Count > 0)
            {
                MQ.Cmd("/squelch /hidecor looted");
                MQ.Delay(100);


                //lets check if we can loot.
                if (MQ.Query<bool>("${Stick.Active}")) MQ.Cmd("/squelch /stick off");
                if (MQ.Query<bool>("${AdvPath.Following}")) MQ.Cmd("/squelch /afollow off ");
                
                
                foreach(var c in corpses)
                {
                    Casting.TrueTarget(c.ID);
                    MQ.Delay(100);
                    e3util.TryMoveToTarget();
                    



                }



            }

        }
        private static bool LootCorpse(Spawn corpse)
        {
            return false;
            Int32 freeInventorySlots = MQ.Query<Int32>("${Me.FreeInventory}");
            bool importantItem = false;

            if(!_fullInventoryAlert && freeInventorySlots<1)
            {
                _fullInventoryAlert = true;
                E3._bots.Broadcast("\arMy inventory is full! \awI will continue to link items on corpses, but cannot loot anything else.");
                MQ.Cmd("/beep");
              
            }
           

            MQ.Cmd("/loot");
            MQ.Delay(1000, "${Window[LootWnd].Open}");
            if(!MQ.Query<bool>("!${Window[LootWnd].Open}"))
            {
                MQ.Write("\arERROR, Loot Window not opening, adding to ignore corpse list.");
                if(!_unlootableCorpses.Contains(corpse.ID))
                {
                    _unlootableCorpses.Add(corpse.ID);
                }
                return false;

            }
            MQ.Delay(1000, "${Corpse.Items}");

            Int32 corpseItems = MQ.Query<Int32>("${Corpse.Items}");

            if (corpseItems == 0)
            {
                //no items on the corpse, kick out
                return true;
            }

            for(Int32 i =0;i<=corpseItems;i++)
            {
                //lets try and loot them.
                importantItem = false;

                MQ.Delay(1000, $"${{Corpse.Item[{i}].ID}}");

                string corpseItem = MQ.Query<string>($"${{Corpse.Item[{i}].Name}}");
                if(_lootOnlyStackable)
                {
                    bool stackable = MQ.Query<bool>($"${{Corpse.Item[{i}].Stackable}}");
                    bool nodrop = MQ.Query<bool>($"${{Corpse.Item[{i}].NoDrop}}");
                    Int32 itemValue = MQ.Query<Int32>($"${{Corpse.Item[{i}].Value}}");
                    if (stackable && !nodrop)
                    {
                        //check if in our always loot. 
                        if (E3._generalSettings.Loot_OnlyStackableAlwaysLoot.Contains(corpseItem, StringComparer.OrdinalIgnoreCase))
                        {
                            importantItem = true;
                        }
                        if(!importantItem & itemValue>= _lootOnlyStackableValue)
                        {
                            importantItem = true;   
                        }
                        if (!importantItem &  _lootOnlyStackableAllTradeSkils)
                        {
                            if(corpseItem.Contains(" Pelt")) importantItem = true;
                            if (corpseItem.Contains(" Silk")) importantItem = true;
                            if (corpseItem.Contains(" Ore")) importantItem = true;
                        
                        }




                    }




                }

            }



        }


    }
}
