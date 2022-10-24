﻿using E3Core.Data;
using E3Core.Settings;
using E3Core.Settings.FeatureSettings;
using E3Core.Utility;
using Microsoft.Win32;
using MonoCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace E3Core.Processors
{
  
    public static class BegForBuffs
    {
        class BuffQueuedItem
        {
            public String Requester = String.Empty;
            public String SpellTouse = String.Empty;
        }

        public static string _lastSuccesfulCast = String.Empty;
        public static Logging _log = E3._log;
        private static IMQ MQ = E3.MQ;
        private static ISpawns _spawns = E3._spawns;
        private static Dictionary<string, Int64> _DIStickCooldown = new Dictionary<string, long>();
        private static Int64 _nextBegCheck = 0;
        private static Int64 _nextBegCheckInterval = 10000;
        private static Queue<BuffQueuedItem> _queuedBuffs = new Queue<BuffQueuedItem>();
        private static SpellAliasDataFile _spellAliasesDataFile = new SpellAliasDataFile();
        private static Dictionary<string, string> _spellAliases;
        public static void Init()
        {
            RegsterEvents();
            _spellAliasesDataFile.LoadData();
            _spellAliases = _spellAliasesDataFile.GetClassAliases();

        }

        private static void RegsterEvents()
        {

            EventProcessor.RegisterEvent("BuffBeg", "(.+) tells you, '(.+)'", (x) =>
            {
                if (x.match.Groups.Count > 2)
                {
                    string user = x.match.Groups[1].Value;
                    string spell = x.match.Groups[2].Value;
                    //check to see if its an alias.
                    string realSpell = string.Empty;
                    if(_spellAliases.TryGetValue(spell,out realSpell))
                    {
                        spell = realSpell;
                    }
                    bool inBook = MQ.Query<bool>($"${{Me.Book[{spell}]}}");
                    if(inBook)
                    {
                        MQ.Cmd($"/t {user} I'm queuing up {spell} to use on you, please wait.");
                        MQ.Delay(0);
                        _queuedBuffs.Enqueue(new BuffQueuedItem() { Requester = user, SpellTouse = spell });

                    }
                }
            });



        }
        [ClassInvoke(Data.Class.All)]
        public static void Check_QueuedBuffs()
        {
            if (!e3util.ShouldCheck(ref _nextBegCheck, _nextBegCheckInterval)) return;

            if (_queuedBuffs.Count>0)
            {
                var askedForSpell = _queuedBuffs.Peek();
                Spawn spawn;

                if (_spawns.TryByName(askedForSpell.Requester, out spawn))
                {
                    Spell s;
                    if (!Spell._loadedSpellsByName.TryGetValue(askedForSpell.SpellTouse, out s))
                    {
                        s = new Spell(askedForSpell.SpellTouse);
                    }
                    if (Casting.CheckReady(s) && Casting.CheckMana(s))
                    {
                        //get the id of the requestor.
                        Int32 cursorID = MQ.Query<Int32>("${Cursor.ID}");
                        Casting.Cast(spawn.ID, s);
                        //tells are stupid slow, so put a delay in case the cast was instant
                        MQ.Delay(300);
                        MQ.Cmd($"/t {askedForSpell.Requester} {askedForSpell.SpellTouse} has been cast on you.");
                        if(cursorID<1)
                        {
                            cursorID = MQ.Query<Int32>("${Cursor.ID}");
                            if(cursorID>0)
                            {
                                //the spell that was requested put something on our curosr, give it to them.
                                e3util.GiveItemOnCursorToTarget();

                            }
                        }

                        _queuedBuffs.Dequeue();
                    }
                }
                else
                {
                    //they are not in zone, remove it
                    _queuedBuffs.Dequeue();
                }
            }
        }
    }
}
