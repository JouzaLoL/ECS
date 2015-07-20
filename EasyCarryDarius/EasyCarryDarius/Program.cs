#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LeagueSharp;
using LeagueSharp.Common;
using System.Drawing;
using ItemData = LeagueSharp.Common.Data.ItemData;

#endregion

namespace EasyCarryDarius
{
    class Program
    {
        private static string _champName = "Darius";
        private static Orbwalking.Orbwalker _orbwalker;
        private static SpellSlot _igniteSlot;
        private static Menu _config;
        private static readonly Obj_AI_Hero Player = ObjectManager.Player;

        //Items
        private static readonly Items.Item Cutlass = ItemData.Bilgewater_Cutlass.GetItem();
        private static readonly Items.Item Botrk = ItemData.Blade_of_the_Ruined_King.GetItem();
        private static readonly Items.Item Tiamat = ItemData.Tiamat_Melee_Only.GetItem();
        private static readonly Items.Item Hydra = ItemData.Ravenous_Hydra_Melee_Only.GetItem();


        // ReSharper disable once InconsistentNaming
        private static readonly Dictionary<Spells, Spell> spells = new Dictionary<Spells, Spell>
        {
            {Spells.Q, new Spell(SpellSlot.Q, Player.Spellbook.GetSpell(SpellSlot.Q).SData.CastRange)},
            {Spells.W, new Spell(SpellSlot.W, Player.AttackRange + 25)},
            {Spells.E, new Spell(SpellSlot.E, Player.Spellbook.GetSpell(SpellSlot.E).SData.CastRange)},
            {Spells.R, new Spell(SpellSlot.R, Player.Spellbook.GetSpell(SpellSlot.R).SData.CastRange)}
        };

        // ReSharper disable once UnusedParameter.Local
        private static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += Game_OnGameLoad;
        }

        private static void Game_OnGameLoad(EventArgs args)
        {
            if (Player.CharData.BaseSkinName != _champName) return;

            _igniteSlot = Player.GetSpellSlot("SummonerDot");

            InitMenu();

            Game.OnUpdate += OnUpdate;
            Drawing.OnDraw += Drawings;

            Notifications.AddNotification("EasyCarry - " + _champName + " Loaded", 5000);
            Notifications.AddNotification("Version: " + Assembly.GetExecutingAssembly().GetName().Version, 5000);
        }

        private static void OnUpdate(EventArgs args)
        {
            Player.SetSkin(Player.CharData.BaseSkinName, _config.Item("misc.skinchanger.enable").GetValue<bool>() ? _config.Item("misc.skinchanger.id").GetValue<StringList>().SelectedIndex : Player.BaseSkinId);

            if (Player.IsDead) return;

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

            var autoharass = _config.Item("autoharass.enabled").GetValue<KeyBind>().Active;
            if (autoharass) AutoHarass();

            var killsteal = _config.Item("killsteal.enabled").GetValue<bool>();
            if (killsteal) Killsteal();

            var e = _config.Item("resmanager.enabled").GetValue<bool>();
            if (e) ResourceManager();

        }

        private static void ResourceManager()
        {
            var hp = (Player.MaxHealth / Player.Health) * 100;
            var mp = (Player.MaxMana/Player.Mana)*100;
            var hlimit = _config.Item("resmanager.hp.slider").GetValue<Slider>().Value;
            var mlimit = _config.Item("resmanager.mp.slider").GetValue<Slider>().Value;
            var counter = _config.Item("resmanager.counter").GetValue<bool>();
            var hpotion = ItemData.Health_Potion.GetItem();
            var mpotion = ItemData.Mana_Potion.GetItem();
            var biscuit = ItemData.Total_Biscuit_of_Rejuvenation.GetItem();
            var flask = ItemData.Crystalline_Flask.GetItem();

            if (hpotion.IsOwned(Player) && hpotion.IsReady())
            {
                if ((hp < hlimit || (counter && Player.HasBuff("SummonerIgnite"))) && !Player.HasBuff("RegenerationPotion"))
                    hpotion.Cast();
            }
            else if (biscuit.IsOwned(Player) && biscuit.IsReady())
            {
                if ((hp < hlimit || (counter && Player.HasBuff("SummonerIgnite"))) && !Player.HasBuff("ItemMiniRegenPotion"))
                    biscuit.Cast();
            }
            else if (flask.IsOwned(Player) && flask.IsReady())
            {
                if ((hp < hlimit || (counter && Player.HasBuff("SummonerIgnite"))) && !Player.HasBuff("ItemCrystalFlask"))
                    flask.Cast();
            }

            if (mpotion.IsOwned(Player) && mpotion.IsReady())
            {
                if (mp < mlimit && !Player.HasBuff("ItemCrystalFlask") && !Player.HasBuff("Mana Potion"))
                    mpotion.Cast();

            }
            else if (flask.IsOwned(Player) && flask.IsReady())
            {
                if (mp < mlimit && !Player.HasBuff("ItemCrystalFlask") && !Player.HasBuff("Mana Potion"))
                    flask.Cast();
            }
        }

        private static void Combo()
        {
            var target = TargetSelector.GetTarget(spells[Spells.Q].Range, TargetSelector.DamageType.Magical);
            if (target == null) return;

            var useQ = _config.Item("combo.useQ").GetValue<bool>();
            var useW = _config.Item("combo.useW").GetValue<bool>();
            var useE = _config.Item("combo.useE").GetValue<bool>();
            var useR = _config.Item("combo.useR").GetValue<bool>();
            var useItems = _config.Item("combo.useItems").GetValue<bool>();
            var rMode = _config.Item("combo.rMode").GetValue<StringList>().SelectedIndex;

            if (useE && spells[Spells.E].CanCast(target) && !(Player.Distance(target) > Player.AttackRange)) spells[Spells.E].Cast(target.Position);

            if (useItems)
                UseItems(target);

            if (useQ && spells[Spells.Q].CanCast(target)) spells[Spells.Q].Cast();

            Orbwalking.AfterAttack += delegate(AttackableUnit args, AttackableUnit t)
            {
                if (!args.IsMe) return;
                if (useItems && Hydra.IsReady() && Hydra.IsOwned() && Hydra.IsInRange(target))
                {
                    Hydra.Cast();
                }
                else if (useW && spells[Spells.W].CanCast(target))
                {
                    spells[Spells.W].Cast();
                }
                Utility.DelayAction.Add(50, Orbwalking.ResetAutoAttackTimer);

            };

            if (useR && spells[Spells.R].CanCast(target))
            {
                switch (rMode)
                {
                    case 0: //Kill or Max Damage
                        if (GetRDamage(target) >= target.Health || (Player.MaxHealth/Player.Health) < 0.05)
                            spells[Spells.R].Cast(target);
                        break;
                    case 1: //Only kill
                        if (GetRDamage(target) >= target.Health)
                            spells[Spells.R].Cast(target);
                        break;
                }
            }
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
                    if (spells[Spells.Q].CanCast(target)) spells[Spells.Q].Cast();
                    break;
            }
        }

        private static void Killsteal()
        {
            var e = HeroManager.Enemies.Where(x => x.IsVisible && x.IsValidTarget());
            var useq = _config.Item("killsteal.useQ").GetValue<bool>();
            var user = _config.Item("killsteal.useR").GetValue<bool>();
            var usee = _config.Item("killsteal.useE").GetValue<bool>();

            var objAiHeroes = e as Obj_AI_Hero[] ?? e.ToArray();
            var qtarget = objAiHeroes.FirstOrDefault(y => spells[Spells.Q].IsKillable(y));
            if (useq && spells[Spells.Q].CanCast(qtarget) && qtarget != null)
            {
                spells[Spells.Q].Cast();
            }

            var rtarget = objAiHeroes.FirstOrDefault(y => spells[Spells.R].IsKillable(y));
            if (user && spells[Spells.R].CanCast(rtarget) && rtarget != null)
            {
                spells[Spells.R].Cast(rtarget);
            }

            var etarget = objAiHeroes.FirstOrDefault(y => spells[Spells.E].IsKillable(y));
            if (usee && spells[Spells.W].CanCast(etarget) && etarget != null)
            {
                spells[Spells.W].Cast();
            }

            var itarget = objAiHeroes.FirstOrDefault(y => Player.GetSpellDamage(y, _igniteSlot) < y.Health && y.Distance(Player) <= 600);
            if (Player.Spellbook.CanUseSpell(_igniteSlot) == SpellState.Ready && itarget != null)
            {
                Player.Spellbook.CastSpell(_igniteSlot, itarget);
            }
        }

        private static void AutoHarass()
        {
            if (_orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo) return;
            var target = TargetSelector.GetTarget(spells[Spells.Q].Range, TargetSelector.DamageType.Magical);
            if (target == null) return;

            var useq = _config.Item("autoharass.useQ").GetValue<bool>();

            if (useq && spells[Spells.Q].CanCast(target)) spells[Spells.Q].Cast();
        }

        private static void UseItems(Obj_AI_Base target)
        {

            if (Cutlass.IsReady() && Cutlass.IsOwned(Player) && Cutlass.IsInRange(target))
                Cutlass.Cast(target);

            if (Botrk.IsReady() && Botrk.IsOwned(Player) && Botrk.IsInRange(target))
                Botrk.Cast(target);

        }

        private static void Laneclear()
        {
            var m = MinionManager.GetMinions(spells[Spells.Q].Range, MinionTypes.All, MinionTeam.Enemy, MinionOrderTypes.MaxHealth).FirstOrDefault();
            if (m == null) return;
            
            var useQ = _config.Item("laneclear.useQ").GetValue<bool>();
            var useW = _config.Item("laneclear.useW").GetValue<bool>();
            var useItems = _config.Item("laneclear.useItems").GetValue<bool>();
            var qhitcount = _config.Item("laneclear.qhitcount").GetValue<Slider>().Value;
            var c = MinionManager.GetMinions(spells[Spells.Q].Range).Count;

            if (useQ && spells[Spells.Q].CanCast(m) && c >= qhitcount - 1) spells[Spells.Q].Cast();

            Orbwalking.AfterAttack += delegate(AttackableUnit args, AttackableUnit target)
            {
                if (!args.IsMe) return;
                if (useItems && Hydra.IsReady() && Hydra.IsOwned() && Hydra.IsInRange(m))
                {
                    Hydra.Cast();
                }
                else if (useW && spells[Spells.W].CanCast(m))
                {
                    spells[Spells.W].Cast();
                }
                Utility.DelayAction.Add(50, Orbwalking.ResetAutoAttackTimer);

            };
        }

        private static void Jungleclear()
        {
            var m = MinionManager.GetMinions(spells[Spells.Q].Range, MinionTypes.All, MinionTeam.Neutral, MinionOrderTypes.MaxHealth).FirstOrDefault();
            if (m == null) return;

            var useQ = _config.Item("jungleclear.useQ").GetValue<bool>();
            var useW = _config.Item("jungleclear.useW").GetValue<bool>();
            var useItems = _config.Item("jungleclear.useItems").GetValue<bool>();

            if (useQ && spells[Spells.Q].CanCast(m)) spells[Spells.Q].Cast();

            Orbwalking.AfterAttack += delegate(AttackableUnit args, AttackableUnit target)
            {
                if (!args.IsMe) return;
                if (useItems && Hydra.IsReady() && Hydra.IsOwned() && Hydra.IsInRange(m))
                {
                    Hydra.Cast();
                }
                else if (useW && spells[Spells.W].CanCast(m))
                {
                    spells[Spells.W].Cast();
                }
                Utility.DelayAction.Add(50, Orbwalking.ResetAutoAttackTimer);

            };

        }

        private static void Lasthit()
        {
            var minions = MinionManager.GetMinions(spells[Spells.Q].Range);
            var useQ = Get<bool>("farm.useQ");
            var useW = Get<bool>("farm.useW");
            var useItems = Get<bool>("farm.useItems");

            var qtarget = minions.FirstOrDefault(m => spells[Spells.Q].IsKillable(m) && spells[Spells.Q].CanCast(m));
            if (qtarget != null && useQ)

                spells[Spells.Q].Cast();

            var wtarget = minions.FirstOrDefault(m => spells[Spells.W].IsKillable(m) && spells[Spells.W].CanCast(m));
            if (wtarget != null && useW)
                spells[Spells.W].Cast();

            var htarget = minions.FirstOrDefault(m => Player.GetItemDamage(m, Damage.DamageItems.Hydra) > m.Health);
            if (htarget != null && Hydra.IsInRange(htarget) && useItems) 
                Hydra.Cast();

            var ttarget = minions.FirstOrDefault(m => Player.GetItemDamage(m, Damage.DamageItems.Tiamat) > m.Health);
            if (ttarget != null && Tiamat.IsInRange(ttarget) && useItems) 
                Tiamat.Cast();

        }

        private static void Drawings(EventArgs args)
        {
            var enabled = Get<bool>("drawing.enable");
            if (!enabled) return;

            var readyColor = _config.Item("drawing.readyColor").GetValue<Circle>().Color;
            var cdColor = _config.Item("drawing.cdColor").GetValue<Circle>().Color;

            foreach (var s in spells.Values.Where(k => k.Level > 0 && _config.Item("drawing.draw" + k.Slot.ToString().ToUpper()).GetValue<bool>()))
            {
                Render.Circle.DrawCircle(ObjectManager.Player.Position, s.Range, s.IsReady() ? readyColor : cdColor);
            }
        }

        private static void InitMenu()
        {
            _config = new Menu("[EasyCarry] - " + _champName, "ecs." + _champName, true);

            _config.AddSubMenu(new Menu("[" + _champName + "] Orbwalker", "ecs.orbwalker"));
            _orbwalker = new Orbwalking.Orbwalker(_config.SubMenu("ecs.orbwalker"));

            var tsMenu = new Menu("[" + _champName + "] Target Selector", "ecs.targetselector");
            TargetSelector.AddToMenu(tsMenu);
            _config.AddSubMenu(tsMenu);

            var combo = new Menu("[" + _champName + "] Combo Settings", _champName + ".combo");
            {
                combo.AddItem(new MenuItem("combo.rMode", "R Mode")).SetValue(new StringList(new[] {"Kill or Max Damage", "Only Kill"}));
                combo.AddItem(new MenuItem("combo.useItems", "Use Items")).SetValue(true);
                combo.AddItem(new MenuItem("combo.useQ", "Use Q")).SetValue(true);
                combo.AddItem(new MenuItem("combo.useW", "Use W")).SetValue(true);
                combo.AddItem(new MenuItem("combo.useE", "Use E")).SetValue(true);
                combo.AddItem(new MenuItem("combo.useR", "Use R")).SetValue(true);
            }
            _config.AddSubMenu(combo);

            var killsteal = new Menu("[" + _champName + "] Killsteal Settings", _champName + ".killsteal");
            {
                killsteal.AddItem(new MenuItem("killsteal.enabled", "Killsteal Enabled")).SetValue(true);
                killsteal.AddItem(new MenuItem("killsteal.useQ", "Use Q")).SetValue(true);
                killsteal.AddItem(new MenuItem("killsteal.useW", "Use W")).SetValue(true);
                killsteal.AddItem(new MenuItem("killsteal.useIgnite", "Use Ignite")).SetValue(true);
            }
            _config.AddSubMenu(killsteal);

            var harass = new Menu("[" + _champName + "] Harass Settings", _champName + ".harass");
            {
                harass.AddItem(new MenuItem("harass.mode", "Harass Mode: ").SetValue(new StringList(new[] {"Q only"})));
                harass.AddItem(new MenuItem("placeholder", "======_AutoHarass_======"));
                harass.AddItem(new MenuItem("autoharass.enabled", "AutoHarass Enabled")).SetValue(new KeyBind("T".ToCharArray()[0], KeyBindType.Toggle));
                harass.AddItem(new MenuItem("autoharass.useQ", "Use Q")).SetValue(true);
            }
            _config.AddSubMenu(harass);

            var farm = new Menu("[" + _champName + "] Farm Settings", _champName + ".farm");
            {
                farm.AddItem(new MenuItem("farm.useItems", "Use Items")).SetValue(true);
                farm.AddItem(new MenuItem("farm.useQ", "Use Q")).SetValue(true);
                farm.AddItem(new MenuItem("farm.useW", "Use W")).SetValue(true);
            }
            _config.AddSubMenu(farm);

            var laneclear = new Menu("[" + _champName + "] Laneclear Settings", _champName + ".laneclear");
            {
                laneclear.AddItem(new MenuItem("laneclear.qhitcount", "Minium Q Hit Count")).SetValue(new Slider(3, 1, 10));
                laneclear.AddItem(new MenuItem("laneclear.useItems", "Use Items")).SetValue(true);
                laneclear.AddItem(new MenuItem("laneclear.useQ", "Use Q")).SetValue(true);
                laneclear.AddItem(new MenuItem("laneclear.useW", "Use W")).SetValue(true);
            }
            _config.AddSubMenu(laneclear);

            var jungleclear = new Menu("[" + _champName + "] Jungleclear Settings", _champName + ".jungleclear");
            {
                jungleclear.AddItem(new MenuItem("jungleclear.useItems", "Use Items")).SetValue(true);
                jungleclear.AddItem(new MenuItem("jungleclear.useQ", "Use Q")).SetValue(true);
                jungleclear.AddItem(new MenuItem("jungleclear.useW", "Use W")).SetValue(true);
            }
            _config.AddSubMenu(jungleclear);

            var drawing = new Menu("[" + _champName + "] Drawing Settings", _champName + ".drawing");
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
                delegate(object sender, OnValueChangeEventArgs eventArgs)
                {
                    DamageIndicator.Enabled = eventArgs.GetNewValue<bool>();
                };

            _config.Item("drawing.drawDamage.fill").ValueChanged +=
                delegate(object sender, OnValueChangeEventArgs eventArgs)
                {
                    DamageIndicator.Fill = eventArgs.GetNewValue<Circle>().Active;
                    DamageIndicator.FillColor = eventArgs.GetNewValue<Circle>().Color;
                };

            var resmanager = new Menu("[" + _champName + "] Resource Manager Settings", _champName + ".resmanager");
            {
                resmanager.AddItem(new MenuItem("resmanager.enabled", "Resource Manager Enabled")).SetValue(true);
                resmanager.AddItem(new MenuItem("resmanager.hp.slider", "HP Pots HP %")).SetValue(new Slider(30, 1));
                resmanager.AddItem(new MenuItem("resmanager.mp.slider", "MP Pots MP %")).SetValue(new Slider(30, 1));
                resmanager.AddItem(new MenuItem("resmanager.counter", "Counter Ignite & Morde Ult")).SetValue(true);
            }
            _config.AddSubMenu(resmanager);

            var misc = new Menu("[" + _champName + "] Misc Settings", _champName + ".misc");
            {
                misc.AddItem(new MenuItem("misc.interrupt", "Interrupt with E")).SetValue(true);
                misc.AddItem(new MenuItem("misc.gapcloser", "Cancel Gapclosers with E")).SetValue(true);
                misc.AddItem(new MenuItem("misc.skinchanger.enable", "Use SkinChanger").SetValue(false));
                misc.AddItem(new MenuItem("misc.skinchanger.id", "Select skin:").SetValue(new StringList(new[] {"Classic", "Mercenary", "Red Card", "Bilgewater", "Kitty Cat", "High Command", "Darude Sandstorm", "Slay Belle", "Warring Kingdoms"})));
            }
            _config.AddSubMenu(misc);

            _config.AddToMainMenu();
        }

        private static float GetRDamage(Obj_AI_Base target)
        {
            var dmg = 0f;
            dmg += (float)Player.GetSpellDamage(target, SpellSlot.R);

            return dmg;
        }

        private static float GetDamage(Obj_AI_Hero target)
        {
            var dmg = 0f;
            if (spells[Spells.Q].IsReady())
                dmg += spells[Spells.Q].GetDamage(target);
            if (spells[Spells.W].IsReady())
                dmg += spells[Spells.W].GetDamage(target);

            var cutlass = ItemData.Bilgewater_Cutlass.GetItem();

            if (cutlass.IsReady() && cutlass.IsOwned(Player)) dmg += (float) Player.GetItemDamage(target, Damage.DamageItems.Bilgewater);

            if (spells[Spells.R].IsReady()) dmg += GetRDamage(target);

            if (_igniteSlot == SpellSlot.Unknown || Player.Spellbook.CanUseSpell(_igniteSlot) != SpellState.Ready) dmg += (float) Player.GetSummonerSpellDamage(target, Damage.SummonerSpell.Ignite);

            return dmg;
        }

        private static T Get<T>(string item)
        {
            return _config.Item(item).GetValue<T>();
        }

        internal enum Spells
        {
            Q,
            W,
            E,
            R
        }
    }
}