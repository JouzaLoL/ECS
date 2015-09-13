﻿#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using Color = System.Drawing.Color;
using ItemData = LeagueSharp.Common.Data.ItemData;
using U = EasyCarryKatarina.Utils;

#endregion

namespace EasyCarryKatarina
{
    internal class Program
    {
        private static Orbwalking.Orbwalker _orbwalker;
        private static SpellSlot _igniteSlot;
        private static Menu _config;
        public static readonly Obj_AI_Hero Player = ObjectManager.Player;
        private static bool _rBlock;
        private static int _lastE;
        private static Vector3 _lastWardPos;
        private static int _lastPlaced;
        private static int _lasttick;
        // ReSharper disable once InconsistentNaming
        private static readonly Dictionary<Spells, Spell> spells = new Dictionary<Spells, Spell>
        {
            { Spells.Q, new Spell(SpellSlot.Q, 675)},
            { Spells.W, new Spell(SpellSlot.W, 375)},
            { Spells.E, new Spell(SpellSlot.E, 700)},
            { Spells.R, new Spell(SpellSlot.R, 550)}
        };

        private static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += Game_OnGameLoad;
        }

        private static void Game_OnGameLoad(EventArgs args)
        {
            if (Player.CharData.BaseSkinName != "Katarina")
            {
                U.Log("Champion is not Katarina, exiting.");
                return;
            }

            _igniteSlot = Player.GetSpellSlot("SummonerDot");

            spells[Spells.Q].SetTargetted((float)0.3, 400);
            spells[Spells.R].SetCharged("KatarinaR", "KatarinaR", 550, 550, 1.0f);           

            InitMenu();

            Game.OnUpdate += OnUpdate;
            Drawing.OnDraw += Drawings;
            //Drawing.OnDraw += U.OnDraw;

            Obj_AI_Base.OnPlayAnimation += OnAnimation;
            Obj_AI_Base.OnIssueOrder += Obj_AI_Hero_OnIssueOrder;
            GameObject.OnCreate += GameObject_OnCreate;

            Notifications.AddNotification("EasyCarry - Katarina Loaded", 5000);
            Notifications.AddNotification("Version: " + Assembly.GetExecutingAssembly().GetName().Version, 5000);

            Console.Clear();
            U.Log("Katarina initialized.");
        }

        private static void OnUpdate(EventArgs args)
        {
            Player.SetSkin(Player.CharData.BaseSkinName, _config.Item("misc.skinchanger.enable").GetValue<bool>() ? _config.Item("misc.skinchanger.id").GetValue<StringList>().SelectedIndex : Player.BaseSkinId);

            if (Player.IsDead) return;

            if (Player.IsChannelingImportantSpell())
            {
                _orbwalker.SetAttack(false);
                _orbwalker.SetMovement(false);
            }
            else
            {
                _orbwalker.SetAttack(true);
                _orbwalker.SetMovement(true);
            }

            //Tick limiter
            if (_config.Item("misc.ticklimiter.enable").GetValue<bool>())
            {
                if (Environment.TickCount - _lasttick < _config.Item("misc.ticklimiter.amount").GetValue<Slider>().Value)
                    return;
                _lasttick = Environment.TickCount;
            }

            switch (_orbwalker.ActiveMode)
            {
                case Orbwalking.OrbwalkingMode.Combo:
                    Combo();
                    break;

                case Orbwalking.OrbwalkingMode.Mixed:
                    Harass();
                    break;

                case Orbwalking.OrbwalkingMode.LaneClear:
                    Laneclear();
                    Jungleclear();
                    break;

                case Orbwalking.OrbwalkingMode.LastHit:
                    Lasthit();
                    break;
            }
           

            var flee = _config.Item("flee.key").GetValue<KeyBind>().Active;
            if (flee) Flee();

            var autoharass = _config.Item("autoharass.enabled").GetValue<KeyBind>().Active;
            if (autoharass) AutoHarass();

            var killsteal = _config.Item("killsteal.enabled").GetValue<bool>();
            if (killsteal) Killsteal();

            var resmananger = _config.Item("resmanager.enabled").GetValue<bool>();
            if (resmananger) ResourceManager();
        }

        private static void ResourceManager()
        {
            if (Player.IsRecalling() || Player.InFountain() || Player.IsDead) return;
            var hp = (Player.MaxHealth/Player.Health)*100;
            var limit = _config.Item("resmanager.hp.slider").GetValue<Slider>().Value;
            var counter = _config.Item("resmanager.counter").GetValue<bool>();
            var potion = ItemData.Health_Potion.GetItem();

            if (!potion.IsOwned(Player) || !potion.IsReady() ) return;
            if (hp < limit || (counter && Player.HasBuff("SummonerIgnite")))
                potion.Cast();
        }

        private static void Combo()
        {
            if (_rBlock) return;

            var target = TargetSelector.GetTarget(spells[Spells.Q].Range, TargetSelector.DamageType.Magical);
            if (target == null) return;

            var useQ = _config.Item("combo.useQ").GetValue<bool>();
            var useW = _config.Item("combo.useW").GetValue<bool>();
            var useE = _config.Item("combo.useE").GetValue<bool>();
            var useR = _config.Item("combo.useR").GetValue<bool>();
            var useItems = _config.Item("combo.useItems").GetValue<bool>();
            var mode = _config.Item("combo.mode").GetValue<StringList>().SelectedIndex;

            switch (mode)
            {
                case 0: //Automatic
                    if (U.GetQCollision(target))
                    {
                        if (useE && spells[Spells.E].CanCast(target))
                            CastE(target);
                        if (useQ && spells[Spells.Q].CanCast(target))
                            spells[Spells.Q].CastOnUnit(target);
                    }
                    else
                    {
                        if (useQ && spells[Spells.Q].CanCast(target))
                            spells[Spells.Q].CastOnUnit(target);
                        if (useE && spells[Spells.E].CanCast(target))
                            CastE(target);
                    }
                    break;

                case 1: //EQ
                    if (useE && spells[Spells.E].CanCast(target))
                        CastE(target);
                    if (useQ && spells[Spells.Q].CanCast(target))
                        spells[Spells.Q].CastOnUnit(target);
                    if (useW && spells[Spells.W].CanCast(target))
                        spells[Spells.W].Cast();
                    break;

                case 2: //QE
                    if (useQ && spells[Spells.Q].CanCast(target))
                        spells[Spells.Q].CastOnUnit(target);
                    if (useE && spells[Spells.E].CanCast(target))
                        CastE(target);
                    if (useW && spells[Spells.W].CanCast(target))
                        spells[Spells.W].Cast();
                    break;
            }
            

            if (useItems)
                UseItems(target);            

            if (useR && !spells[Spells.Q].IsReady() && !spells[Spells.W].IsReady() && !spells[Spells.E].IsReady() && spells[Spells.R].CanCast(target))
                spells[Spells.R].StartCharging();
        }

        private static void Harass()
        {
            var target = TargetSelector.GetTarget(spells[Spells.Q].Range, TargetSelector.DamageType.Magical);
            if (target == null || !target.IsValidTarget())
                return;

            var menuItem = _config.Item("harass.mode").GetValue<StringList>().SelectedIndex;

            switch (menuItem)
            {
                case 0:
                    if (spells[Spells.Q].CanCast(target)) spells[Spells.Q].Cast(target);
                    break;
                case 1:
                    if (spells[Spells.Q].CanCast(target)) spells[Spells.Q].Cast(target);
                    if (spells[Spells.W].CanCast(target)) spells[Spells.W].Cast();
                    break;
                case 2:
                    if (spells[Spells.Q].CanCast(target)) spells[Spells.Q].Cast(target);
                    if (spells[Spells.E].CanCast(target) && Player.HasBuff("KatarinaQMark")) spells[Spells.E].Cast(target);
                    if (spells[Spells.W].CanCast(target)) spells[Spells.W].Cast();
                    break;
            }
        }

        private static void Killsteal()
        {
            var e = HeroManager.Enemies.Where(x => x.IsVisible && x.IsValidTarget());
            var useq = _config.Item("killsteal.useQ").GetValue<bool>();
            var usew = _config.Item("killsteal.useW").GetValue<bool>();
            var usee = _config.Item("killsteal.useE").GetValue<bool>();
            var ultks = _config.Item("killsteal.ultks").GetValue<bool>();
            var mode = _config.Item("killsteal.mode").GetValue<StringList>().SelectedIndex;
            var waitformark = _config.Item("killsteal.waitformark").GetValue<bool>();

            var aiHeroes = e as Obj_AI_Hero[] ?? e.ToArray();

            if (_rBlock && ultks) //KSing while Ulting
            {
                var target = aiHeroes.FirstOrDefault(x => x.Health < spells[Spells.Q].GetDamage(x) + spells[Spells.W].GetDamage(x) + spells[Spells.E].GetDamage(x) - 50);
                if (target != null && spells[Spells.Q].CanCast(target) && spells[Spells.W].IsReady() && spells[Spells.E].CanCast(target))
                {
                    if (mode == 0)
                    {
                        spells[Spells.E].Cast(target);
                        spells[Spells.Q].Cast(target);
                        if (waitformark && target.HasBuff("katarinaqmark"))
                        {
                            spells[Spells.W].Cast();
                        }
                        else if (!waitformark)
                        {
                            spells[Spells.W].Cast();
                        }

                    }
                    else
                    {
                        spells[Spells.Q].Cast(target);
                        if (waitformark && target.HasBuff("katarinaqmark"))
                        {
                            spells[Spells.E].Cast(target);
                        }
                        else if (!waitformark)
                        {
                            spells[Spells.E].Cast(target);
                        }
                        spells[Spells.W].Cast();
                    }
                }
            }

            var objAiHeroes = e as Obj_AI_Hero[] ?? aiHeroes.ToArray();
            var qtarget = objAiHeroes.FirstOrDefault(y => spells[Spells.Q].IsKillable(y));
            if (useq && spells[Spells.Q].CanCast(qtarget) && qtarget != null)
            {
                spells[Spells.Q].Cast(qtarget);
            }

            var wtarget = objAiHeroes.FirstOrDefault(y => spells[Spells.W].IsKillable(y));
            if (usew && spells[Spells.W].CanCast(wtarget) && wtarget != null)
            {
                spells[Spells.W].Cast();
            }

            var etarget = objAiHeroes.FirstOrDefault(y => spells[Spells.E].IsKillable(y));
            if (usee && spells[Spells.E].CanCast(etarget) && etarget != null)
            {
                CastE(etarget);
            }

            var itarget = objAiHeroes.FirstOrDefault(y => Player.GetSpellDamage(y, _igniteSlot) > y.Health && y.Distance(Player) <= 600);
            if (Player.Spellbook.CanUseSpell(_igniteSlot) == SpellState.Ready && itarget != null)
            {
                Player.Spellbook.CastSpell(_igniteSlot, itarget);
            }
        }

        private static void AutoHarass()
        {
            if (_orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo) return;
            var enabled = _config.Item("autoharass.enabled").GetValue<KeyBind>().Active;
            if (!enabled) return;

            var useq = _config.Item("autoharass.useQ").GetValue<bool>();
            var usew = _config.Item("autoharass.useW").GetValue<bool>();
            var target = TargetSelector.GetTarget(spells[Spells.Q].Range, TargetSelector.DamageType.Magical);
            if (target == null) return;

            if (useq && spells[Spells.Q].CanCast(target)) spells[Spells.Q].Cast(target);
            if (usew && spells[Spells.W].CanCast(target)) spells[Spells.W].Cast();
        }

        private static void UseItems(Obj_AI_Base target)
        {
            var cutlass = ItemData.Bilgewater_Cutlass.GetItem();
            var hextech = ItemData.Hextech_Gunblade.GetItem();

            if (cutlass.IsReady() && cutlass.IsOwned(Player) && cutlass.IsInRange(target))
                cutlass.Cast(target);

            if (hextech.IsReady() && hextech.IsOwned(Player) && hextech.IsInRange(target))
                hextech.Cast(target);
        }

        private static void Laneclear()
        {
            var m = MinionManager.GetMinions(spells[Spells.Q].Range, MinionTypes.All, MinionTeam.Enemy, MinionOrderTypes.MaxHealth).FirstOrDefault();
            if (m == null) return;
            var w = MinionManager.GetMinions(spells[Spells.W].Range).Count;
            var count = _config.Item("laneclear.whitcount").GetValue<Slider>().Value;
            var useQ = _config.Item("laneclear.useQ").GetValue<bool>();
            var useW = _config.Item("laneclear.useW").GetValue<bool>();
            var useE = _config.Item("laneclear.useE").GetValue<bool>();

            if (useQ && spells[Spells.Q].CanCast(m)) spells[Spells.Q].CastOnUnit(m);
            if (useW && spells[Spells.W].CanCast(m) && w >= count - 1) spells[Spells.W].Cast();
            if (useE && spells[Spells.E].CanCast(m) && !m.UnderTurret(true)) spells[Spells.E].CastOnUnit(m);
        }

        private static void Jungleclear()
        {
            var m = MinionManager.GetMinions(spells[Spells.Q].Range, MinionTypes.All, MinionTeam.Neutral, MinionOrderTypes.MaxHealth).FirstOrDefault();
            if (m == null) return;

            var mode = _config.Item("jungleclear.mode").GetValue<StringList>().SelectedIndex;
            var useq = _config.Item("jungleclear.useQ").GetValue<bool>();
            var usew = _config.Item("jungleclear.useW").GetValue<bool>();
            var usee = _config.Item("jungleclear.useE").GetValue<bool>();

            if (mode == 0)
            {
                if (useq && spells[Spells.Q].CanCast(m)) spells[Spells.Q].CastOnUnit(m);
                if (usee && spells[Spells.E].CanCast(m)) spells[Spells.E].CastOnUnit(m);
                if (usew && spells[Spells.W].CanCast(m)) spells[Spells.W].Cast();
            }
            else
            {
                if (usee && spells[Spells.E].CanCast(m)) spells[Spells.E].CastOnUnit(m);
                if (useq && spells[Spells.Q].CanCast(m)) spells[Spells.Q].CastOnUnit(m);
                if (usew && spells[Spells.W].CanCast(m)) spells[Spells.W].Cast();
            }
        }

        private static void Lasthit()
        {
            var minions = MinionManager.GetMinions(spells[Spells.Q].Range);
            var useQ = _config.Item("farm.useQ").GetValue<bool>();
            var useW = _config.Item("farm.useW").GetValue<bool>();
            var useE = _config.Item("farm.useE").GetValue<bool>();

            
            if (useQ && spells[Spells.Q].IsReady())
            {
                var qm = minions.FirstOrDefault(y => spells[Spells.Q].IsInRange(y) && spells[Spells.Q].IsKillable(y));
                if (qm != null && !Player.IsWindingUp)
                    spells[Spells.Q].Cast(qm);
            }

            if (useW && spells[Spells.W].IsReady())
            {
                var wm = minions.FirstOrDefault(y => spells[Spells.W].IsInRange(y) && spells[Spells.W].IsKillable(y));
                var qwm = minions.FirstOrDefault(y => spells[Spells.W].IsInRange(y) && y.HasBuff("KatarinaQMark") && y.Health < spells[Spells.W].GetDamage(y) + GetMarkDamage());
                if (wm != null && !Player.IsWindingUp)
                    spells[Spells.W].Cast();
                if (qwm != null && !Player.IsWindingUp)
                    spells[Spells.W].Cast();
            }

            if (useE && spells[Spells.E].IsReady())
            {
                var em = minions.FirstOrDefault(y => spells[Spells.E].IsInRange(y) && spells[Spells.E].IsKillable(y));
                if (em != null)
                    spells[Spells.E].Cast(em);
            }

        }

        private static void Flee()
        {
            var mode = _config.Item("flee.mode").GetValue<StringList>().SelectedIndex;
            var wardjump = _config.Item("flee.useWardJump").GetValue<bool>();
            var cursorpos = Game.CursorPos;
            var range = _config.Item("flee.range").GetValue<Slider>().Value;

            switch (mode)
            {
                case 0: //To mouse
                    var m = MinionManager.GetMinions(cursorpos, range, MinionTypes.All, MinionTeam.All).OrderBy(x => x.Distance(cursorpos)).FirstOrDefault(j => spells[Spells.E].IsInRange(j));
                    var wards = ObjectManager.Get<GameObject>().Where(x => x.Name.ToLower().Contains("ward")).FirstOrDefault(x => spells[Spells.E].IsInRange(x));
                    var h = ObjectManager.Get<Obj_AI_Hero>().FirstOrDefault(x => x.IsTargetable && x.IsEnemy && spells[Spells.W].CanCast(x));
                    if (h != null && spells[Spells.W].CanCast(h)) spells[Spells.W].Cast();
                    if (m != null && spells[Spells.E].CanCast(m)) spells[Spells.E].CastOnUnit(m);
                    else if (wards != null && spells[Spells.E].CanCast((Obj_AI_Base)wards) && wards.Position.Distance(cursorpos) < range) spells[Spells.E].Cast((Obj_AI_Base)wards);
                    else if (wardjump)
                    {
                        WardJump();
                    }
                    Player.IssueOrder(GameObjectOrder.MoveTo, Game.CursorPos);
                    break;
                case 1: //Auto
                    var minion = MinionManager.GetMinions(spells[Spells.E].Range, MinionTypes.All, MinionTeam.All);
                    var enemies = HeroManager.Enemies.Where(e => e.IsVisible);
                    var best = minion.OrderByDescending(l => enemies.OrderByDescending(e => e.Distance(l.Position)).FirstOrDefault().Distance(l.Position)).FirstOrDefault();
                    if (best != null && spells[Spells.E].CanCast(best))
                        spells[Spells.E].CastOnUnit(best);               
                    break;
            }
        }

        private static void CastE(Obj_AI_Base target)
        {
            var l = _config.Item("legit.enabled").GetValue<bool>();
            var random = _config.Item("legit.random").GetValue<bool>();
            var randomMin = _config.Item("legit.random.min").GetValue<Slider>().Value;
            var randomMax = _config.Item("legit.random.max").GetValue<Slider>().Value;
            var d = _config.Item("legit.delayE").GetValue<Slider>().Value;
            if (l)
            {
                if (random)
                {
                    var r = new Random();
                    var rnd = r.Next(randomMin, randomMax);
                    if (Environment.TickCount > _lastE + rnd)
                        spells[Spells.E].Cast(target);
                }
                else if (Environment.TickCount > _lastE + d) 
                {
                    spells[Spells.E].Cast(target);
                }
                
                _lastE = Environment.TickCount;
            }
            else
            {
                spells[Spells.E].Cast(target);
            }
        }

        private static void Drawings(EventArgs args)
        {
            var enabled = _config.Item("drawing.enable").GetValue<bool>();
            if (!enabled) return;

            var readyColor = _config.Item("drawing.readyColor").GetValue<Circle>().Color;
            var cdColor = _config.Item("drawing.cdColor").GetValue<Circle>().Color;
            var drawQ = _config.Item("drawing.drawQ").GetValue<bool>();
            var drawW = _config.Item("drawing.drawW").GetValue<bool>();
            var drawE = _config.Item("drawing.drawE").GetValue<bool>();

            if (drawQ)
                if (spells[Spells.Q].Level > 0)
                    Render.Circle.DrawCircle(ObjectManager.Player.Position, spells[Spells.Q].Range, spells[Spells.Q].IsReady() ? readyColor : cdColor);

            if (drawW)
                if (spells[Spells.W].Level > 0)
                    Render.Circle.DrawCircle(ObjectManager.Player.Position, spells[Spells.W].Range, spells[Spells.W].IsReady() ? readyColor : cdColor);

            if (drawE)
                if (spells[Spells.E].Level > 0)
                    Render.Circle.DrawCircle(ObjectManager.Player.Position, spells[Spells.E].Range, spells[Spells.E].IsReady() ? readyColor : cdColor);

            var drawrange = _config.Item("flee.draw").GetValue<bool>();
            var range = _config.Item("flee.range").GetValue<Slider>().Value;
            var cursorpos = Game.CursorPos;
            var color = _config.Item("flee.color").GetValue<Circle>().Color;
            if (drawrange && _config.Item("flee.key").GetValue<KeyBind>().Active)
                Render.Circle.DrawCircle(cursorpos, range, color);
        }

        private static void InitMenu()
        {
            _config = new Menu("[EasyCarry] - Katarina", "ecs.katarina", true);

            _config.AddSubMenu(new Menu("[Katarina] Orbwalker", "ecs.orbwalker"));
            _orbwalker = new Orbwalking.Orbwalker(_config.SubMenu("ecs.orbwalker"));

            var tsMenu = new Menu("[Katarina] Target Selector", "ecs.targetselector");
            TargetSelector.AddToMenu(tsMenu);
            _config.AddSubMenu(tsMenu);

            var combo = new Menu("[Katarina] Combo Settings", "katarina.combo");
            {
                combo.AddItem(new MenuItem("combo.mode", "Combo Mode:")).SetValue(new StringList(new[] {"Automatic", "EQ", "QE"}, 1));
                combo.AddItem(new MenuItem("combo.useItems", "Use Items")).SetValue(true);
                combo.AddItem(new MenuItem("combo.useQ", "Use Q")).SetValue(true);
                combo.AddItem(new MenuItem("combo.useW", "Use W")).SetValue(true);
                combo.AddItem(new MenuItem("combo.useE", "Use E")).SetValue(true);
                combo.AddItem(new MenuItem("combo.useR", "Use R")).SetValue(true);
            }
            _config.AddSubMenu(combo);

            var killsteal = new Menu("[Katarina] Killsteal Settings", "katarina.killsteal");
            {
                killsteal.AddItem(new MenuItem("killsteal.enabled", "Killsteal Enabled")).SetValue(true);
                killsteal.AddItem(new MenuItem("killsteal.useQ", "Use Q")).SetValue(true);
                killsteal.AddItem(new MenuItem("killsteal.useW", "Use W")).SetValue(true);
                killsteal.AddItem(new MenuItem("killsteal.useE", "Use E")).SetValue(true);
                killsteal.AddItem(new MenuItem("killsteal.useIgnite", "Use Ignite")).SetValue(true);
                killsteal.AddItem(new MenuItem("placeholder", "============================"));
                killsteal.AddItem(new MenuItem("killsteal.ultks", "KS when Ulting")).SetValue(true);
                killsteal.AddItem(new MenuItem("killsteal.mode", "Ult KS Mode")).SetValue(new StringList(new[] {"EQW", "QEW"}));
                killsteal.AddItem(new MenuItem("killsteal.waitformark", "Wait for Q mark")).SetValue(true);
            }
            _config.AddSubMenu(killsteal);

            var harass = new Menu("[Katarina] Harass Settings", "katarina.harass");
            {
                harass.AddItem(new MenuItem("harass.mode", "Harass Mode: ").SetValue(new StringList(new[] {"Q only", "Q -> W", "Q -> E -> W"})));
            }
            _config.AddSubMenu(harass);

            var autoharass = new Menu("[Katarina] Autoharass Settings", "katarina.autoharass");
            {
                autoharass.AddItem(new MenuItem("autoharass.enabled", "AutoHarass Enabled")).SetValue(new KeyBind("T".ToCharArray()[0], KeyBindType.Toggle));
                autoharass.AddItem(new MenuItem("autoharass.useQ", "Use Q")).SetValue(true);
                autoharass.AddItem(new MenuItem("autoharass.useW", "Use W")).SetValue(true);
            }
            _config.AddSubMenu(autoharass);

            var farm = new Menu("[Katarina] Farm Settings", "katarina.farm");
            {
                farm.AddItem(new MenuItem("farm.useQ", "Use Q")).SetValue(true);
                farm.AddItem(new MenuItem("farm.useW", "Use W")).SetValue(true);
                farm.AddItem(new MenuItem("farm.useE", "Use E")).SetValue(false);
            }
            _config.AddSubMenu(farm);

            var laneclear = new Menu("[Katarina] Laneclear Settings", "katarina.laneclear");
            {
                laneclear.AddItem(new MenuItem("laneclear.whitcount", "Minium W Hit Count")).SetValue(new Slider(3, 1, 10));
                laneclear.AddItem(new MenuItem("laneclear.useQ", "Use Q")).SetValue(true);
                laneclear.AddItem(new MenuItem("laneclear.useW", "Use W")).SetValue(true);
                laneclear.AddItem(new MenuItem("laneclear.useE", "Use E")).SetValue(false);
            }
            _config.AddSubMenu(laneclear);

            var jungleclear = new Menu("[Katarina] Jungleclear Settings", "katarina.jungleclear");
            {
                jungleclear.AddItem(new MenuItem("jungleclear.mode", "Mode")).SetValue(new StringList(new[] { "QEW", "EQW" }, 1));
                jungleclear.AddItem(new MenuItem("jungleclear.useQ", "Use Q")).SetValue(true);
                jungleclear.AddItem(new MenuItem("jungleclear.useW", "Use W")).SetValue(true);
                jungleclear.AddItem(new MenuItem("jungleclear.useE", "Use E")).SetValue(true);
            }
            _config.AddSubMenu(jungleclear);

            var wardjump = new Menu("[Katarina] Wardjump Settings", "katarina.wardjump");
            {
                var x = new MenuItem("wardjump.key", "Wardjump Key: ");

                //Wardjump event
                x.ValueChanged += (sender, args) => { if (args.GetNewValue<KeyBind>().Active) WardJump(); };

                wardjump.AddItem(x.SetValue(new KeyBind("S".ToCharArray()[0], KeyBindType.Press)));
            }
            _config.AddSubMenu(wardjump);

            var flee = new Menu("[Katarina] Flee Settings", "katarina.flee");
            {
                flee.AddItem(new MenuItem("flee.key", "Flee Key: ").SetValue(new KeyBind("A".ToCharArray()[0], KeyBindType.Press)));
                flee.AddItem(new MenuItem("flee.mode", "Flee Mode:")).SetValue(new StringList(new[] {"To Mouse", "Auto"}));
                flee.AddItem(new MenuItem("flee.range", "Search Range")).SetValue(new Slider(300, 100, 1000));
                flee.AddItem(new MenuItem("flee.useWardJump", "Use WardJump")).SetValue(true);
                flee.AddItem(new MenuItem("flee.draw", "Draw Search Range")).SetValue(true);
                flee.AddItem(new MenuItem("flee.color", "Search Range Color")).SetValue(new Circle(true, Color.White));
            }
            _config.AddSubMenu(flee);

            var legit = new Menu("[Katarina] Legit Menu", "katarina.legit");
            {
                legit.AddItem(new MenuItem("legit.enabled", "Enable Legit Mode")).SetValue(true);
                legit.AddItem(new MenuItem("legit.delayE", "E Delay")).SetValue(new Slider(750, 0, 1000));
                legit.AddItem(new MenuItem("legit.random", "Random E Delay")).SetValue(true);
                legit.AddItem(new MenuItem("legit.random.min", "Minimum Random Delay")).SetValue(new Slider(100, 0, 1000));
                legit.AddItem(new MenuItem("legit.random.max", "Maximum Random Delay")).SetValue(new Slider(100, 0, 1000));              
            }
            _config.AddSubMenu(legit);

            var drawing = new Menu("[Katarina] Drawing Settings", "katarina.drawing");
            {
                drawing.AddItem(new MenuItem("drawing.enable", "Enable Drawing")).SetValue(true);
                drawing.AddItem(new MenuItem("drawing.readyColor", "Color of Ready Spells")).SetValue(new Circle(true, Color.White));
                drawing.AddItem(new MenuItem("drawing.cdColor", "Color of Spells on CD")).SetValue(new Circle(true, Color.Red));
                drawing.AddItem(new MenuItem("drawing.drawQ", "Draw Q Range")).SetValue(true);
                drawing.AddItem(new MenuItem("drawing.drawW", "Draw W Range")).SetValue(true);
                drawing.AddItem(new MenuItem("drawing.drawE", "Draw E Range")).SetValue(true);
                drawing.AddItem(new MenuItem("drawing.drawDamage.enabled", "Draw Damage").SetValue(true));
                drawing.AddItem(new MenuItem("drawing.drawDamage.fill", "Draw Damage Fill Color").SetValue(new Circle(true, Color.FromArgb(90, 255, 169, 4))));
            }
            _config.AddSubMenu(drawing);

            DamageIndicator.DamageToUnit = GetDamage;
            DamageIndicator.Enabled = _config.Item("drawing.drawDamage.enabled").GetValue<bool>();
            DamageIndicator.Fill = _config.Item("drawing.drawDamage.fill").GetValue<Circle>().Active;
            DamageIndicator.FillColor = _config.Item("drawing.drawDamage.fill").GetValue<Circle>().Color;

            _config.Item("drawing.drawDamage.enabled").ValueChanged +=
                delegate(object sender, OnValueChangeEventArgs eventArgs) { DamageIndicator.Enabled = eventArgs.GetNewValue<bool>(); };
            _config.Item("drawing.drawDamage.fill").ValueChanged +=
                delegate(object sender, OnValueChangeEventArgs eventArgs)
                {
                    DamageIndicator.Fill = eventArgs.GetNewValue<Circle>().Active;
                    DamageIndicator.FillColor = eventArgs.GetNewValue<Circle>().Color;
                };

            var resmanager = new Menu("[Katarina] Resource Manager", "katarina.resmanager");
            {
                resmanager.AddItem(new MenuItem("resmanager.enabled", "Resource Manager Enabled")).SetValue(true);
                resmanager.AddItem(new MenuItem("resmanager.hp.slider", "HP Pots HP %")).SetValue(new Slider(30, 1));
                resmanager.AddItem(new MenuItem("resmanager.counter", "Counter Ignite & Morde Ult")).SetValue(true);
            }
            _config.AddSubMenu(resmanager);

            var misc = new Menu("[Katarina] Misc Settings", "katarina.misc");
            {
                misc.AddItem(new MenuItem("misc.skinchanger.enable", "Use SkinChanger").SetValue(false));
                misc.AddItem(new MenuItem("misc.skinchanger.id", "Select skin:").SetValue(new StringList(new[] {"Classic", "Mercenary", "Red Card", "Bilgewater", "Kitty Cat", "High Command", "Darude Sandstorm", "Slay Belle", "Warring Kingdoms"})));
                misc.AddItem(new MenuItem("misc.ticklimiter.enable", "Enable Tick Limiter")).SetValue(true);
                misc.AddItem(new MenuItem("misc.ticklimiter.amount", "TickLimiter amount")).SetValue(new Slider(100, 0, 500));
            }
            _config.AddSubMenu(misc);

            _config.AddToMainMenu();
        }

        private static float GetMarkDamage()
        {
            return (float) (15*spells[Spells.Q].Level + 0.15);
        }

        private static float GetDamage(Obj_AI_Base target)
        {
            var dmg = 0f;

            if (spells[Spells.Q].IsReady())
            {
                dmg += spells[Spells.Q].GetDamage(target);
            }

            if (spells[Spells.W].IsReady())
            {
                dmg += spells[Spells.W].GetDamage(target);
            }

            if (spells[Spells.E].IsReady())
            {
                dmg += spells[Spells.E].GetDamage(target);
            }
            
            if (Player.Spellbook.GetSpell(SpellSlot.R).State.ToString() != "40" && spells[Spells.R].Level > 0)
            {
                dmg += spells[Spells.R].GetDamage(target) * 8;
            }

            var cutlass = ItemData.Bilgewater_Cutlass.GetItem();
            var hextech = ItemData.Hextech_Gunblade.GetItem();

            if (cutlass.IsReady() && cutlass.IsOwned(Player)) dmg += (float) Player.GetItemDamage(target, Damage.DamageItems.Bilgewater);

            if (hextech.IsReady() && hextech.IsOwned(Player) && hextech.IsInRange(target)) dmg += (float) Player.GetItemDamage(target, Damage.DamageItems.Hexgun);

            if (spells[Spells.Q].IsReady()) dmg += GetMarkDamage();

            if (_igniteSlot != SpellSlot.Unknown || Player.Spellbook.CanUseSpell(_igniteSlot) == SpellState.Ready) dmg += (float) Player.GetSummonerSpellDamage(target, Damage.SummonerSpell.Ignite);

            return dmg;
        }

        internal enum Spells
        {
            Q,
            W,
            E,
            R
        }

        #region Ultimate Block

        private static void OnAnimation(GameObject sender, GameObjectPlayAnimationEventArgs args)
        {
            if (!sender.IsMe) return;
            if (args.Animation == "Spell4")
            {
                _rBlock = true;
            }
            else if (args.Animation == "Run" || args.Animation == "Idle1" || args.Animation == "Attack2" ||
                     args.Animation == "Attack1")
            {
                _rBlock = false;
            }
        }

        private static void Obj_AI_Hero_OnIssueOrder(Obj_AI_Base sender, GameObjectIssueOrderEventArgs args)
        {
            if (sender.IsMe)
            {
                args.Process = !_rBlock;
            }
        }

        #endregion

        #region WardJump

        private static InventorySlot GetBestWardSlot()
        {
            var slot = Items.GetWardSlot();
            return slot == default(InventorySlot) ? null : slot;
        }

        private static void WardJump()
        {
            if (Environment.TickCount <= _lastPlaced + 3000 || !spells[Spells.E].IsReady()) return;

            var cursorPos = Game.CursorPos;
            var myPos = Player.ServerPosition;
            var delta = cursorPos - myPos;

            delta.Normalize();

            var wardPosition = myPos + delta*(600 - 5);
            var wardSlot = GetBestWardSlot();

            if (wardSlot == null) return;

            Items.UseItem((int) wardSlot.Id, wardPosition);
            _lastWardPos = wardPosition;
            _lastPlaced = Environment.TickCount;
        }

        private static void GameObject_OnCreate(GameObject sender, EventArgs args)
        {
            var minion = sender as Obj_AI_Minion;
            if (minion == null || !spells[Spells.E].IsReady() || Environment.TickCount >= _lastPlaced + 300)
                return;
            var ward = minion;
            if (ward.Name.ToLower().Contains("ward") && ward.Distance(_lastWardPos) < 500)
            {
                spells[Spells.E].CastOnUnit(ward);
            }             
        }

        #endregion
    }
}