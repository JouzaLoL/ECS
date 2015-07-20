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
//ToDo: Check if ItemData is working >.<
namespace EasyCarryAkali
{
    class Program
    {
        private static string _champName = "Akali";
        private static Orbwalking.Orbwalker _orbwalker;
        private static SpellSlot _igniteSlot;
        private static Menu _config;
        private static readonly Obj_AI_Hero Player = ObjectManager.Player;
        private static int _stacks;
        // ReSharper disable once InconsistentNaming
        private static readonly Dictionary<Spells, Spell> spells = new Dictionary<Spells, Spell>
        {
            {Spells.Q, new Spell(SpellSlot.Q, Player.Spellbook.GetSpell(SpellSlot.Q).SData.CastRange)},
            {Spells.W, new Spell(SpellSlot.W, Player.Spellbook.GetSpell(SpellSlot.W).SData.CastRange)},
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

            foreach (var s in spells.Values)
            {
                Console.WriteLine(s.Slot + ": " + s.Range);
            }
            Console.WriteLine(Player.Spellbook.GetSpell(SpellSlot.Q).SData.AfterEffectName);


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

                case Orbwalking.OrbwalkingMode.jungleclear:
                    jungleclear();
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

            var e = _config.Item("resmanager.enabled").GetValue<bool>();
            if (e) ResourceManager();

            _stacks = Player.GetBuff("AkaliRStack").Count;
        }

        private static void ResourceManager()
        {
            var hp = (Player.MaxHealth/Player.Health)*100;
            var limit = _config.Item("resmanager.hp.slider").GetValue<Slider>().Value;
            var counter = _config.Item("resmanager.counter").GetValue<bool>();
            var potion = ItemData.Health_Potion.GetItem();

            if (!potion.IsOwned(Player) || !potion.IsReady()) return;
            if (hp < limit || (counter && Player.HasBuff("SummonerIgnite")))
                potion.Cast();
        }

        private static void Combo()
        {
            var target = TargetSelector.GetTarget(spells[Spells.Q].Range, TargetSelector.DamageType.Magical);
            if (target == null) return;

            var useQ = _config.Item("combo.useQ").GetValue<bool>();
            var useE = _config.Item("combo.useE").GetValue<bool>();
            var useR = _config.Item("combo.useR").GetValue<bool>();
            var useItems = _config.Item("combo.useItems").GetValue<bool>();
            var rdist = _config.Item("r.distance").GetValue<Slider>().Value;

            if (useItems)
                UseItems(target);

            if (useQ && spells[Spells.Q].CanCast(target) && !target.HasBuff("AkaliQMark"))
                spells[Spells.Q].Cast(target);

            if (useE && spells[Spells.E].CanCast(target))
                spells[Spells.E].Cast();

            if (useR && spells[Spells.R].CanCast(target))
            {
                if (Player.Distance(target) > rdist)
                    spells[Spells.R].Cast(target);
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
                    if (spells[Spells.Q].CanCast(target)) spells[Spells.Q].Cast(target);
                    break;
                case 1:
                    if (spells[Spells.Q].CanCast(target)) spells[Spells.Q].Cast(target);
                    if (spells[Spells.E].CanCast(target)) spells[Spells.E].Cast();
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
                spells[Spells.Q].Cast(qtarget);
            }

            var rtarget = objAiHeroes.FirstOrDefault(y => spells[Spells.R].IsKillable(y));
            if (user && spells[Spells.R].CanCast(rtarget) && rtarget != null)
            {
                spells[Spells.R].Cast(rtarget);
            }

            var etarget = objAiHeroes.FirstOrDefault(y => spells[Spells.E].IsKillable(y));
            if (usee && spells[Spells.E].CanCast(etarget) && etarget != null)
            {
                spells[Spells.E].Cast();
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
            var enabled = _config.Item("autoharass.enabled").GetValue<KeyBind>().Active;
            if (!enabled) return;

            var useq = _config.Item("autoharass.useQ").GetValue<bool>();
            var usee = _config.Item("autoharass.useE").GetValue<bool>();
            var target = TargetSelector.GetTarget(spells[Spells.Q].Range, TargetSelector.DamageType.Magical);
            if (target == null) return;

            if (useq && spells[Spells.Q].CanCast(target)) spells[Spells.Q].Cast(target);
            if (usee && spells[Spells.E].CanCast(target)) spells[Spells.E].Cast();
        }

        private static void UseItems(Obj_AI_Base target)
        {
            var useHextech = _config.Item("combo.useItems").GetValue<bool>();
            if (!useHextech) return;
            var cutlass = ItemData.Bilgewater_Cutlass.GetItem();
            var hextech = ItemData.Hextech_Gunblade.GetItem();

            if (cutlass.IsReady() && cutlass.IsOwned(Player) && cutlass.IsInRange(target))
                cutlass.Cast(target);

            if (hextech.IsReady() && hextech.IsOwned(Player) && hextech.IsInRange(target))
                hextech.Cast(target);
        }

        private static void jungleclear()
        {
            var m = MinionManager.GetMinions(spells[Spells.Q].Range, MinionTypes.All, MinionTeam.Enemy, MinionOrderTypes.MaxHealth).FirstOrDefault();
            if (m == null) return;
            var stacks = _config.Item("misc.keepstacks").GetValue<Slider>().Value;
            var e = MinionManager.GetMinions(spells[Spells.E].Range).Count;
            var count = _config.Item("jungleclear.ehitcount").GetValue<Slider>().Value;
            var useQ = _config.Item("jungleclear.useQ").GetValue<bool>();
            var useR = _config.Item("jungleclear.useR").GetValue<bool>();
            var useE = _config.Item("jungleclear.useE").GetValue<bool>();

            if (useQ && spells[Spells.Q].CanCast(m)) spells[Spells.Q].CastOnUnit(m);
            if (useR && spells[Spells.R].CanCast(m) && !m.UnderTurret(true) && _stacks > stacks) spells[Spells.R].Cast(m);
            if (useE && spells[Spells.E].CanCast(m) && e >= count - 1) spells[Spells.E].Cast();
        }

        private static void Jungleclear()
        {
            var m = MinionManager.GetMinions(spells[Spells.Q].Range, MinionTypes.All, MinionTeam.Neutral, MinionOrderTypes.MaxHealth).FirstOrDefault();
            if (m == null) return;
            var stacks = _config.Item("misc.keepstacks").GetValue<Slider>().Value;
            var useq = _config.Item("jungleclear.useQ").GetValue<bool>();
            var user = _config.Item("jungleclear.useR").GetValue<bool>();
            var usee = _config.Item("jungleclear.useE").GetValue<bool>();

            if (useq && spells[Spells.Q].CanCast(m)) spells[Spells.Q].CastOnUnit(m);
            if (usee && spells[Spells.E].CanCast(m) && !m.HasBuff("AkaliQMark")) spells[Spells.E].CastOnUnit(m);
            if (user && spells[Spells.R].CanCast(m) && _stacks > stacks) spells[Spells.R].Cast(m);
        }

        private static void Lasthit()
        {
            var minions = MinionManager.GetMinions(spells[Spells.Q].Range);
            var stacks = _config.Item("misc.keepstacks").GetValue<Slider>().Value;

            foreach (var spell in spells.Values)
            {
                var m = minions.FirstOrDefault(x => spell.IsKillable(x));
                var e = _config.Item("farm.use" + spell.Slot).GetValue<bool>();
                if (m == null || !e || Player.IsWindingUp) return;
                if (spell == spells[Spells.R] && _stacks > stacks)
                {
                    spells[Spells.R].Cast(m);
                }
                else
                    spell.CastOnUnit(m);
            }
        }

        private static void Flee()
        {
            var mode = _config.Item("flee.mode").GetValue<StringList>().SelectedIndex;

            switch (mode)
            {
                case 0: //To mouse
                    var m = MinionManager.GetMinions(Game.CursorPos, 300, MinionTypes.All, MinionTeam.All).FirstOrDefault(j => spells[Spells.R].CanCast(j));
                    var h = ObjectManager.Get<Obj_AI_Hero>().FirstOrDefault(x => x.IsTargetable && x.IsEnemy && spells[Spells.W].CanCast(x));
                    if (h != null) spells[Spells.W].Cast();
                    if (m != null) spells[Spells.R].Cast(m);
                    Player.IssueOrder(GameObjectOrder.MoveTo, Game.CursorPos);
                    break;
                case 1: //Auto
                    var minion = MinionManager.GetMinions(spells[Spells.R].Range);
                    var enemies = HeroManager.Enemies.Where(e => e.IsVisible);
                    var best = minion.OrderByDescending(l => enemies.OrderByDescending(e => e.Distance(l.Position)).FirstOrDefault().Distance(l.Position)).FirstOrDefault();
                    if (best != null && spells[Spells.R].CanCast(best))
                        spells[Spells.R].Cast(best);
                    break;
            }
        }

        private static void Drawings(EventArgs args)
        {
            var enabled = _config.Item("drawing.enable").GetValue<bool>();
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
                combo.AddItem(new MenuItem("combo.useItems", "Use Items")).SetValue(true);
                combo.AddItem(new MenuItem("combo.useQ", "Use Q")).SetValue(true);
                combo.AddItem(new MenuItem("combo.useW", "Use W")).SetValue(true);
                combo.AddItem(new MenuItem("combo.useE", "Use E")).SetValue(true);
                combo.AddItem(new MenuItem("combo.useR", "Use R")).SetValue(true);
                var rmenu = new Menu("R Settings", "combo.r");
                rmenu.AddItem(new MenuItem("r.dist", "Minimum distance to target to cast R")).SetValue(true);
            }
            _config.AddSubMenu(combo);

            var killsteal = new Menu("[" + _champName + "] Killsteal Settings", _champName + ".killsteal");
            {
                killsteal.AddItem(new MenuItem("killsteal.enabled", "Killsteal Enabled")).SetValue(true);
                killsteal.AddItem(new MenuItem("killsteal.useQ", "Use Q")).SetValue(true);
                killsteal.AddItem(new MenuItem("killsteal.useW", "Use W")).SetValue(true);
                killsteal.AddItem(new MenuItem("killsteal.useE", "Use E")).SetValue(true);
                killsteal.AddItem(new MenuItem("killsteal.useIgnite", "Use Ignite")).SetValue(true);
            }
            _config.AddSubMenu(killsteal);

            var harass = new Menu("[" + _champName + "] Harass Settings", _champName + ".harass");
            {
                harass.AddItem(new MenuItem("harass.mode", "Harass Mode: ").SetValue(new StringList(new[] {"Q only", "Q -> W", "Q -> E -> W"})));
                harass.AddItem(new MenuItem("placeholder", "======_AutoHarass_======"));
                harass.AddItem(new MenuItem("autoharass.enabled", "AutoHarass Enabled")).SetValue(new KeyBind("T".ToCharArray()[0], KeyBindType.Toggle));
                harass.AddItem(new MenuItem("autoharass.useQ", "Use Q")).SetValue(true);
                harass.AddItem(new MenuItem("autoharass.useW", "Use W")).SetValue(true);
            }
            _config.AddSubMenu(harass);

            var farm = new Menu("[" + _champName + "] Farm Settings", _champName + ".farm");
            {
                farm.AddItem(new MenuItem("farm.useQ", "Use Q")).SetValue(true);
                farm.AddItem(new MenuItem("farm.useE", "Use E")).SetValue(true);
                farm.AddItem(new MenuItem("farm.useR", "Use R")).SetValue(false);
            }
            _config.AddSubMenu(farm);

            var jungleclear = new Menu("[" + _champName + "] jungleclear Settings", _champName + ".jungleclear");
            {
                jungleclear.AddItem(new MenuItem("jungleclear.whitcount", "Minium W Hit Count")).SetValue(new Slider(3, 1, 10));
                jungleclear.AddItem(new MenuItem("jungleclear.useQ", "Use Q")).SetValue(true);
                jungleclear.AddItem(new MenuItem("jungleclear.useE", "Use E")).SetValue(true);
                jungleclear.AddItem(new MenuItem("jungleclear.useR", "Use R")).SetValue(false);
            }
            _config.AddSubMenu(jungleclear);

            var jungleclear = new Menu("[" + _champName + "] Jungleclear Settings", _champName + ".jungleclear");
            {
                jungleclear.AddItem(new MenuItem("jungleclear.useQ", "Use Q")).SetValue(true);
                jungleclear.AddItem(new MenuItem("jungleclear.useE", "Use E")).SetValue(true);
                jungleclear.AddItem(new MenuItem("jungleclear.useR", "Use R")).SetValue(true);
            }
            _config.AddSubMenu(jungleclear);

            var flee = new Menu("[" + _champName + "] Flee Settings", _champName + ".flee");
            {
                var y = flee.AddItem(new MenuItem("flee.key", "Flee Key: "));

                flee.AddItem(y.SetValue(new KeyBind("A".ToCharArray()[0], KeyBindType.Press)));
                flee.AddItem(new MenuItem("flee.mode", "Flee Mode:")).SetValue(new StringList(new[] {"To Mouse", "Auto"}));
            }
            _config.AddSubMenu(flee);

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
                delegate(object sender, OnValueChangeEventArgs eventArgs) { DamageIndicator.Enabled = eventArgs.GetNewValue<bool>(); };
            _config.Item("drawing.drawDamage.fill").ValueChanged +=
                delegate(object sender, OnValueChangeEventArgs eventArgs)
                {
                    DamageIndicator.Fill = eventArgs.GetNewValue<Circle>().Active;
                    DamageIndicator.FillColor = eventArgs.GetNewValue<Circle>().Color;
                };

            var resmanager = new Menu("[" + _champName + "] Resource Manager", _champName + ".resmanager");
            {
                resmanager.AddItem(new MenuItem("resmanager.enabled", "Resource Manager Enabled")).SetValue(true);
                resmanager.AddItem(new MenuItem("resmanager.hp.slider", "HP Pots HP %")).SetValue(new Slider(30, 1));
                resmanager.AddItem(new MenuItem("resmanager.counter", "Counter Ignite & Morde Ult")).SetValue(true);
            }
            _config.AddSubMenu(resmanager);

            var misc = new Menu("[" + _champName + "] Misc Settings", _champName + ".misc");
            {
                misc.AddItem(new MenuItem("misc.keepstacks", "Keep R Stacks")).SetValue(new Slider(1, 0, 3));
                misc.AddItem(new MenuItem("misc.skinchanger.enable", "Use SkinChanger").SetValue(false));
                misc.AddItem(new MenuItem("misc.skinchanger.id", "Select skin:").SetValue(new StringList(new[] {"Classic", "Mercenary", "Red Card", "Bilgewater", "Kitty Cat", "High Command", "Darude Sandstorm", "Slay Belle", "Warring Kingdoms"})));
            }
            _config.AddSubMenu(misc);

            _config.AddToMainMenu();
        }

        private static float GetMarkDamage()
        {
            return (float) (0.0);
        }

        private static float GetDamage(Obj_AI_Base target)
        {
            var dmg = spells.Values.Where(x => x.IsReady()).Aggregate<Spell, float>(0, (current, spell) => current + spell.GetDamage(target));

            var cutlass = ItemData.Bilgewater_Cutlass.GetItem();
            var hextech = ItemData.Hextech_Gunblade.GetItem();

            if (cutlass.IsReady() && cutlass.IsOwned(Player)) dmg += (float) Player.GetItemDamage(target, Damage.DamageItems.Bilgewater);

            if (hextech.IsReady() && hextech.IsOwned(Player) && hextech.IsInRange(target)) dmg += (float) Player.GetItemDamage(target, Damage.DamageItems.Hexgun);

            if (spells[Spells.Q].IsReady()) dmg += GetMarkDamage();

            if (_igniteSlot == SpellSlot.Unknown || Player.Spellbook.CanUseSpell(_igniteSlot) != SpellState.Ready) dmg += (float) Player.GetSummonerSpellDamage(target, Damage.SummonerSpell.Ignite);

            return dmg;
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