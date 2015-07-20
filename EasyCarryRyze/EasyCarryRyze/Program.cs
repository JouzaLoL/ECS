#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using LeagueSharp;
using LeagueSharp.Common;
using ItemData = LeagueSharp.Common.Data.ItemData;
#endregion

namespace EasyCarryRyze
{
    internal class Program
    {
        private static Orbwalking.Orbwalker _orbwalker;
        private static SpellSlot _igniteSlot;
        private static Menu _config;
        private static readonly Obj_AI_Hero Player = ObjectManager.Player;
        private static bool _passivecharged;
        private static int _stacks;

        internal enum Spells
        {
            Q,
            W,
            E,
            R
        }

        // ReSharper disable once InconsistentNaming
        public static readonly Dictionary<Spells, Spell> spells = new Dictionary<Spells, Spell>
        {
            {Spells.Q, new Spell(SpellSlot.Q, 865)},
            {Spells.W, new Spell(SpellSlot.W, 585)},
            {Spells.E, new Spell(SpellSlot.E, 585)},
            {Spells.R, new Spell(SpellSlot.R)}
        };

        private static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += Game_OnGameLoad;
        }

        private static void Game_OnGameLoad(EventArgs args)
        {
            if (Player.CharData.BaseSkinName != "Ryze") return;

            _igniteSlot = Player.GetSpellSlot("SummonerDot");

            spells[Spells.Q].SetSkillshot(0.26f, 50f, 1700f, true, SkillshotType.SkillshotLine);

            InitMenu();

            Game.OnUpdate += OnUpdate;
            Drawing.OnDraw += Drawings;

            AntiGapcloser.OnEnemyGapcloser += OnGapcloser;
            Orbwalking.BeforeAttack += BeforeAttack;

            Notifications.AddNotification("EasyCarry - Ryze Loaded", 5000);
            Notifications.AddNotification("Version: " + Assembly.GetExecutingAssembly().GetName().Version, 5000);
        }

        private static HitChance CustomHitChance
        {
            get { return GetHitchance(); }
        }

        private static HitChance GetHitchance()
        {
            switch (_config.Item("misc.hitchance").GetValue<StringList>().SelectedIndex)
            {
                case 0:
                    return HitChance.Low;
                case 1:
                    return HitChance.Medium;
                case 2:
                    return HitChance.High;
                case 3:
                    return HitChance.VeryHigh;
                default:
                    return HitChance.Medium;
            }
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

            var flee = _config.Item("flee.key").GetValue<KeyBind>().Active;
            if (flee) Flee();

            var autoharass = _config.Item("autoharass.enabled").GetValue<KeyBind>().Active;
            if (autoharass) AutoHarass();

            var killsteal = _config.Item("killsteal.enabled").GetValue<bool>();
            if (killsteal) Killsteal();

            var resmanager = _config.Item("resmanager.enabled").GetValue<bool>();
            if (resmanager) ResManager();

            PassiveControl();
        }

        private static void PassiveControl()
        {
            _passivecharged = Player.HasBuff("RyzePassiveCharged");
            var s = Player.Buffs.FirstOrDefault(b => b.DisplayName == "RyzePassiveStack");
            if (s != null) _stacks = s.Count;
        }

        private static void ResManager()
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
            var hp = _config.Item("rmenu.health").GetValue<Slider>().Value;

            if ((Player.Health/Player.MaxHealth)*100 < hp)
            {
                if (spells[Spells.R].IsReady())
                    spells[Spells.R].Cast();
            }

            if (_passivecharged)
            {
                if (useQ && spells[Spells.Q].CanCast(target)) spells[Spells.Q].Cast(target.ServerPosition);
                else if (useW && spells[Spells.W].CanCast(target) && !spells[Spells.Q].IsReady()) spells[Spells.W].CastOnUnit(target);
                else if (useE && spells[Spells.E].IsReady() && !spells[Spells.Q].IsReady()) spells[Spells.E].CastOnUnit(target);
            }
            else switch (_stacks)
            {
                case 4:
                    if (spells[Spells.R].IsReady() && useR)
                    {
                        spells[Spells.R].Cast();
                    }
                    else
                    {
                        if (useQ && spells[Spells.Q].IsReady() && spells[Spells.Q].GetPrediction(target).Hitchance >= CustomHitChance) spells[Spells.Q].Cast(target.Position);
                        if (useE && spells[Spells.E].CanCast(target)) spells[Spells.E].Cast(target);
                        if (useW && spells[Spells.W].CanCast(target)) spells[Spells.W].Cast(target);
                    }
                    break;
            }
        }

        private static void Harass()
        {
            var target = TargetSelector.GetTarget(spells[Spells.Q].Range, TargetSelector.DamageType.Magical);
            if (target == null || !target.IsValidTarget()) return;
            var qpred = spells[Spells.Q].GetPrediction(target);
            var mode = _config.Item("harass.mode").GetValue<StringList>().SelectedIndex;

            switch (mode)
            {
                case 0: //1st mode: Q only
                    if (spells[Spells.Q].IsReady() && qpred.Hitchance >= CustomHitChance)
                    {
                        spells[Spells.Q].Cast(qpred.CastPosition);
                    }
                    break;
                case 1: //2nd mode: Q and W
                        if (spells[Spells.Q].CanCast(target) && qpred.Hitchance >= CustomHitChance) spells[Spells.Q].Cast(qpred.CastPosition);
                        if (spells[Spells.W].CanCast(target)) spells[Spells.W].Cast(target);
                    break;
                case 2: //3rd mode: Q, E and W
                        if (spells[Spells.Q].CanCast(target) && qpred.Hitchance >= CustomHitChance) spells[Spells.Q].Cast(qpred.CastPosition);
                        if (spells[Spells.E].CanCast(target)) spells[Spells.E].Cast(target);
                        if (spells[Spells.W].CanCast(target)) spells[Spells.W].Cast(target);
                    break;
            }
        }

        private static void Killsteal()
        {
            var e = HeroManager.Enemies.Where(x => x.IsVisible && x.IsValidTarget());

            var useq = _config.Item("killsteal.useQ").GetValue<bool>();
            var usew = _config.Item("killsteal.useW").GetValue<bool>();
            var usee = _config.Item("killsteal.useE").GetValue<bool>();

            var objAiHeroes = e as Obj_AI_Hero[] ?? e.ToArray();

            var qtarget = objAiHeroes.FirstOrDefault(y => spells[Spells.Q].IsKillable(y));
            var qpred = spells[Spells.Q].GetPrediction(qtarget);
            if (useq && spells[Spells.Q].CanCast(qtarget) && qtarget != null && qpred.Hitchance >= CustomHitChance)
            {
                spells[Spells.Q].Cast(qpred.CastPosition);
            }

            var wtarget = objAiHeroes.FirstOrDefault(y => spells[Spells.W].IsKillable(y));
            if (usew && spells[Spells.W].CanCast(wtarget) && wtarget != null)
            {
                spells[Spells.W].Cast(wtarget);
            }

            var etarget = objAiHeroes.FirstOrDefault(y => spells[Spells.E].IsKillable(y));
            if (usee && spells[Spells.E].CanCast(etarget) && etarget != null)
            {
                spells[Spells.E].Cast(etarget);
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
            var usew = _config.Item("autoharass.useW").GetValue<bool>();
            var usee = _config.Item("autoharass.useE").GetValue<bool>();
            var qpred = spells[Spells.Q].GetPrediction(target);

            if (useq && spells[Spells.Q].CanCast(target) && qpred.Hitchance >= CustomHitChance) spells[Spells.Q].Cast(qpred.CastPosition);
            if (usew && spells[Spells.W].CanCast(target)) spells[Spells.W].Cast(target);
            if (usee && spells[Spells.E].CanCast(target)) spells[Spells.E].Cast(target);
        }

        private static void Laneclear()
        {
            var m = MinionManager.GetMinions(spells[Spells.Q].Range).FirstOrDefault();
            if (m == null) return;
            var useQ = _config.Item("laneclear.useQ").GetValue<bool>();
            var useW = _config.Item("laneclear.useW").GetValue<bool>();
            var useE = _config.Item("laneclear.useE").GetValue<bool>();

            if (_passivecharged)
            {
                if (useQ && spells[Spells.Q].CanCast(m)) spells[Spells.Q].Cast(m.ServerPosition);
                if (useW && spells[Spells.W].IsReady() && !spells[Spells.Q].IsReady()) spells[Spells.W].Cast(m);
                if (useE && spells[Spells.E].IsReady() && !spells[Spells.Q].IsReady()) spells[Spells.E].Cast(m);
            }
            else
            {
                if (useQ && spells[Spells.Q].CanCast(m) && spells[Spells.Q].GetPrediction(m).CollisionObjects.Count <= 0) spells[Spells.Q].Cast(m.Position);
                if (useE && spells[Spells.E].CanCast(m)) spells[Spells.E].Cast(m);
                if (useW && spells[Spells.W].CanCast(m)) spells[Spells.W].Cast(m);
            }
        }

        private static void Jungleclear()
        {
            var m = MinionManager.GetMinions(spells[Spells.Q].Range, MinionTypes.All, MinionTeam.Neutral, MinionOrderTypes.MaxHealth).FirstOrDefault();
            if (m == null) return;
            var useq = _config.Item("laneclear.useQ").GetValue<bool>();
            var usew = _config.Item("laneclear.useW").GetValue<bool>();
            var usee = _config.Item("laneclear.useE").GetValue<bool>();
            var qpred = spells[Spells.Q].GetPrediction(m).CollisionObjects.Count;

            if (_passivecharged)
            {
                if (useq && spells[Spells.Q].CanCast(m)) spells[Spells.Q].Cast(m.ServerPosition);
                if (usew && spells[Spells.W].IsReady() && !spells[Spells.Q].IsReady()) spells[Spells.W].Cast(m);
                if (usee && spells[Spells.E].IsReady() && !spells[Spells.Q].IsReady()) spells[Spells.E].Cast(m);
            }
            else
            {
                if (useq && spells[Spells.Q].CanCast(m) && qpred <= 0) spells[Spells.Q].Cast(m.Position);
                if (usee && spells[Spells.E].CanCast(m)) spells[Spells.E].CastOnUnit(m);
                if (usew && spells[Spells.W].CanCast(m)) spells[Spells.W].CastOnUnit(m);
            }
            
        }

        private static void Lasthit()
        {
            var minions = MinionManager.GetMinions(spells[Spells.W].Range);

            foreach (var spell in spells.Values)
            {
                var m = minions.FirstOrDefault(x => spell.IsKillable(x));
                var qpred = spells[Spells.Q].GetPrediction(m).CollisionObjects.Count;
                var e = _config.Item("farm.use" + spell.Slot).GetValue<bool>();
                if (m == null || !e) return;
                if (spell.IsSkillshot && qpred <= 0)
                    spell.Cast(m.Position);
                else
                    spell.Cast(m);              
            }
        }

        private static void Flee()
        {
            var h = ObjectManager.Get<Obj_AI_Hero>().FirstOrDefault(x => x.IsTargetable && x.IsEnemy && spells[Spells.W].CanCast(x));
            if (h != null) spells[Spells.W].Cast(h);
            Player.IssueOrder(GameObjectOrder.MoveTo, Game.CursorPos);
        }

        #region AA Block & Antigapcloser

        private static void BeforeAttack(Orbwalking.BeforeAttackEventArgs args)
        {
            if (!args.Unit.IsMe || _orbwalker.ActiveMode != Orbwalking.OrbwalkingMode.Combo) return;
            args.Process = !(spells[Spells.Q].IsReady() || spells[Spells.W].IsReady() || spells[Spells.E].IsReady());
        }

        private static void OnGapcloser(ActiveGapcloser gapcloser)
        {
            if (!spells[Spells.W].CanCast(gapcloser.Sender)) return;
            spells[Spells.W].Cast(gapcloser.Sender);
        }

        #endregion

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
        }

        private static void InitMenu()
        {
            _config = new Menu("[EasyCarry] - Ryze", "ecs.ryze", true);

            _config.AddSubMenu(new Menu("[Ryze] Orbwalker", "ecs.orbwalker"));
            _orbwalker = new Orbwalking.Orbwalker(_config.SubMenu("ecs.orbwalker"));

            var tsMenu = new Menu("[Ryze] Target Selector", "ecs.targetselector");
            TargetSelector.AddToMenu(tsMenu);
            _config.AddSubMenu(tsMenu);

            var combo = new Menu("[Ryze] Combo Settings", "ryze.combo");
            {
                combo.AddItem(new MenuItem("combo.useQ", "Use Q")).SetValue(true);
                combo.AddItem(new MenuItem("combo.useW", "Use W")).SetValue(true);
                combo.AddItem(new MenuItem("combo.useE", "Use E")).SetValue(true);
                combo.AddItem(new MenuItem("combo.useR", "Use R")).SetValue(true);
                var rmenu = combo.AddSubMenu(new Menu("R Settings", "combo.rmenu"));
                {
                    rmenu.AddItem(new MenuItem("rmenu.health", "Use R if HP drops below %")).SetValue(new Slider(30));
                }
            }
            _config.AddSubMenu(combo);

            var killsteal = new Menu("[Ryze] Killsteal Settings", "ryze.killsteal");
            {
                killsteal.AddItem(new MenuItem("killsteal.enabled", "Killsteal Enabled")).SetValue(true);
                killsteal.AddItem(new MenuItem("killsteal.useQ", "Use Q")).SetValue(true);
                killsteal.AddItem(new MenuItem("killsteal.useW", "Use W")).SetValue(true);
                killsteal.AddItem(new MenuItem("killsteal.useE", "Use E")).SetValue(true);
                killsteal.AddItem(new MenuItem("killsteal.useIgnite", "Use Ignite")).SetValue(true);
            }
            _config.AddSubMenu(killsteal);

            var harass = new Menu("[Ryze] Harass Settings", "ryze.harass");
            {
                harass.AddItem(new MenuItem("harass.mode", "Harass Mode: ").SetValue(new StringList(new[] {"Q only", "Q -> W", "Q -> E -> W"})));
                harass.AddItem(new MenuItem("autoharass.enabled", "AutoHarass Enabled")).SetValue(new KeyBind("T".ToCharArray()[0], KeyBindType.Toggle));
                harass.AddItem(new MenuItem("autoharass.useQ", "Use Q")).SetValue(true);
                harass.AddItem(new MenuItem("autoharass.useW", "Use W")).SetValue(true);
                harass.AddItem(new MenuItem("autoharass.useE", "Use E")).SetValue(true);
            }
            _config.AddSubMenu(harass);

            var farm = new Menu("[Ryze] Farm Settings", "ryze.farm");
            {
                farm.AddItem(new MenuItem("farm.useQ", "Use Q")).SetValue(true);
                farm.AddItem(new MenuItem("farm.useW", "Use W")).SetValue(true);
                farm.AddItem(new MenuItem("farm.useE", "Use E")).SetValue(false);
            }
            _config.AddSubMenu(farm);

            var laneclear = new Menu("[Ryze] Laneclear Settings", "ryze.laneclear");
            {
                laneclear.AddItem(new MenuItem("laneclear.useQ", "Use Q")).SetValue(true);
                laneclear.AddItem(new MenuItem("laneclear.useW", "Use W")).SetValue(true);
                laneclear.AddItem(new MenuItem("laneclear.useE", "Use E")).SetValue(false);
            }
            _config.AddSubMenu(laneclear);

            var jungleclear = new Menu("[Ryze] Jungleclear Settings", "ryze.jungleclear");
            {
                jungleclear.AddItem(new MenuItem("jungleclear.useQ", "Use Q")).SetValue(true);
                jungleclear.AddItem(new MenuItem("jungleclear.useW", "Use W")).SetValue(true);
                jungleclear.AddItem(new MenuItem("jungleclear.useE", "Use E")).SetValue(true);
            }
            _config.AddSubMenu(jungleclear);

            var flee = new Menu("[Ryze] Flee Settings", "ryze.flee");
            {
                flee.AddItem(new MenuItem("flee.key", "Flee Key: ")).SetValue(new KeyBind("A".ToCharArray()[0], KeyBindType.Press));
            }
            _config.AddSubMenu(flee);

            var drawing = new Menu("[Ryze] Drawing Settings", "ryze.drawing");
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

            var resmanager = new Menu("[Ryze] Resource Manager", "ryze.resmanager");
            {
                resmanager.AddItem(new MenuItem("resmanager.enabled", "Resource Manager Enabled")).SetValue(true);
                resmanager.AddItem(new MenuItem("resmanager.hp.slider", "HP Pots HP %")).SetValue(new Slider(30, 1));
                resmanager.AddItem(new MenuItem("resmanager.mp.slider", "MP Pots MP %")).SetValue(new Slider(30, 1));
                resmanager.AddItem(new MenuItem("resmanager.counter", "Counter Ignite & Morde Ult")).SetValue(true);
            }
            _config.AddSubMenu(resmanager);

            var misc = new Menu("[Ryze] Misc Settings", "ryze.misc");
            {
                misc.AddItem(new MenuItem("misc.gapcloser", "Enable Anti Gapcloser")).SetValue(true);
                misc.AddItem(new MenuItem("misc.skinchanger.enable", "Use SkinChanger").SetValue(false));
                misc.AddItem(new MenuItem("misc.skinchanger.id", "Select skin:").SetValue(new StringList(new[] {"Classic", "Human", "Tribal", "Uncle", "Triumphant", "Professor", "Zombie", "Dark Crystal", "Pirate"})));
                misc.AddItem(new MenuItem("misc.hitchance", "Q Hitchance").SetValue(new StringList(new[] { "Low", "Medium", "High", "Very High" }, 3)));
            }
            _config.AddSubMenu(misc);

            _config.AddToMainMenu();
        }

        private static float GetDamage(Obj_AI_Base target)
        {
            var dmg = 0f;
            if (spells[Spells.Q].CanCast(target))
                dmg += spells[Spells.Q].GetDamage(target) * 3;
            if (spells[Spells.W].CanCast(target))
                dmg += spells[Spells.W].GetDamage(target);
            if (spells[Spells.E].CanCast(target))
                dmg += spells[Spells.E].GetDamage(target);
            if (_igniteSlot == SpellSlot.Unknown || Player.Spellbook.CanUseSpell(_igniteSlot) != SpellState.Ready) dmg += (float) Player.GetSummonerSpellDamage(target, Damage.SummonerSpell.Ignite);

            return dmg;
        }
    }
}