#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LeagueSharp;
using LeagueSharp.Common;
using Color = System.Drawing.Color;
using ItemData = LeagueSharp.Common.Data.ItemData;

#endregion

namespace LiteKatarina
{
    internal class Program
    {
        private static Orbwalking.Orbwalker _orbwalker;
        private static Menu _config;
        private static Items.Item _cutlass;
        private static Items.Item _hextech;
        public static readonly Obj_AI_Hero Player = ObjectManager.Player;
        private static bool _rBlock;

        // ReSharper disable once InconsistentNaming
        public static readonly Dictionary<Spells, Spell> spells = new Dictionary<Spells, Spell>
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
            if (Player.CharData.BaseSkinName != "Katarina") return;


            spells[Spells.Q].SetTargetted((float)0.3, 400);
            spells[Spells.R].SetCharged("KatarinaR", "KatarinaR", 550, 550, 1.0f);

            _cutlass = ItemData.Bilgewater_Cutlass.GetItem();
            _hextech = ItemData.Hextech_Gunblade.GetItem();

            InitMenu();

            Game.OnUpdate += OnUpdate;
            Drawing.OnDraw += Drawings;

            Obj_AI_Base.OnPlayAnimation += OnAnimation;
            Obj_AI_Base.OnIssueOrder += Obj_AI_Hero_OnIssueOrder;

            Notifications.AddNotification("LiteKatarina Loaded", 5000);
            Notifications.AddNotification("Version: " + Assembly.GetExecutingAssembly().GetName().Version, 5000);

            Console.Clear();
        }

        private static void OnUpdate(EventArgs args)
        {
            if (Player.IsDead) return;

            if ((Player.IsChannelingImportantSpell() || _rBlock) && RHeroBlock() )
            {
                _orbwalker.SetAttack(false);
                _orbwalker.SetMovement(false);
            }
            else
            {
                _orbwalker.SetAttack(true);
                _orbwalker.SetMovement(true);
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

                    break;
            }
        }

        private static void Combo()
        {
            if (_rBlock && RHeroBlock()) return;

            var target = TargetSelector.GetTarget(spells[Spells.Q].Range, TargetSelector.DamageType.Magical);
            if (target == null) return;

            if (spells[Spells.Q].CanCast(target))
                spells[Spells.Q].CastOnUnit(target);
            if (spells[Spells.E].CanCast(target))
                spells[Spells.E].Cast(target);
            if (spells[Spells.W].CanCast(target))
                spells[Spells.W].Cast();

                UseItems(target);

            if (!spells[Spells.Q].IsReady() && !spells[Spells.W].IsReady() && !spells[Spells.E].IsReady() && spells[Spells.R].CanCast(target))
                spells[Spells.R].StartCharging();
        }

        private static void Harass()
        {
            var target = TargetSelector.GetTarget(spells[Spells.Q].Range, TargetSelector.DamageType.Magical);
            if (target == null || !target.IsValidTarget())
                return;

                    if (spells[Spells.Q].CanCast(target)) spells[Spells.Q].Cast(target);
                    if (spells[Spells.E].CanCast(target)) spells[Spells.E].Cast(target);
                    if (spells[Spells.W].CanCast(target)) spells[Spells.W].Cast();
        }

        private static void UseItems(Obj_AI_Base target)
        {
            if (_cutlass.IsReady() && _cutlass.IsOwned(Player) && _cutlass.IsInRange(target))
                _cutlass.Cast(target);

            if (_hextech.IsReady() && _hextech.IsOwned(Player) && _hextech.IsInRange(target))
                _hextech.Cast(target);
        }

        private static void Laneclear()
        {
            var m = MinionManager.GetMinions(spells[Spells.Q].Range, MinionTypes.All, MinionTeam.Enemy, MinionOrderTypes.MaxHealth).FirstOrDefault();
            if (m == null) return;

            if (spells[Spells.Q].CanCast(m)) spells[Spells.Q].CastOnUnit(m);
            if (spells[Spells.W].CanCast(m)) spells[Spells.W].Cast();
            if (spells[Spells.E].CanCast(m) && !m.UnderTurret(true)) spells[Spells.E].CastOnUnit(m);
        }

        private static void Jungleclear()
        {
            var m = MinionManager.GetMinions(spells[Spells.Q].Range, MinionTypes.All, MinionTeam.Neutral, MinionOrderTypes.MaxHealth).FirstOrDefault();
            if (m == null) return;

            if (spells[Spells.Q].CanCast(m)) spells[Spells.Q].CastOnUnit(m);
            if (spells[Spells.E].CanCast(m)) spells[Spells.E].CastOnUnit(m);
            if (spells[Spells.W].CanCast(m)) spells[Spells.W].Cast();
        }

        private static void Drawings(EventArgs args)
        {
            var enabled = _config.Item("drawing.enable").GetValue<bool>();
            if (!enabled) return;

            var readyColor = Color.White;
            var cdColor = Color.Red;

                if (spells[Spells.Q].Level > 0)
                    Render.Circle.DrawCircle(ObjectManager.Player.Position, spells[Spells.Q].Range, spells[Spells.Q].IsReady() ? readyColor : cdColor);
                if (spells[Spells.W].Level > 0)
                    Render.Circle.DrawCircle(ObjectManager.Player.Position, spells[Spells.W].Range, spells[Spells.W].IsReady() ? readyColor : cdColor);

                if (spells[Spells.E].Level > 0)
                    Render.Circle.DrawCircle(ObjectManager.Player.Position, spells[Spells.E].Range, spells[Spells.E].IsReady() ? readyColor : cdColor);
        }

        private static void InitMenu()
        {
            _config = new Menu("LiteKatarina", "ecs.katarina", true);

            _config.AddSubMenu(new Menu("[Katarina] Orbwalker", "ecs.orbwalker"));
            _orbwalker = new Orbwalking.Orbwalker(_config.SubMenu("ecs.orbwalker"));

            var tsMenu = new Menu("[Katarina] Target Selector", "ecs.targetselector");
            TargetSelector.AddToMenu(tsMenu);
            _config.AddSubMenu(tsMenu);

            var credits = new Menu("[Katarina] Credits", "katarina.credits");
            {
                credits.AddItem(new MenuItem("credits.by", "Made by Jouza"));
                credits.AddItem(new MenuItem("credits.series", "This script is part of the EasyCarry Series"));
                credits.AddItem(new MenuItem("credits.thanks", "A big thank you to the whole L# community"));
                credits.AddItem(new MenuItem("credits.donations", "If you want to make a donation, send me a PM on joduska.me"));
            }
            _config.AddSubMenu(credits);

            _config.AddToMainMenu();
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
                args.Process = !(_rBlock && RHeroBlock());
            }
        }

        public static bool RHeroBlock()
        {

            return HeroManager.Enemies.Any(y => y.Distance(Player) <= 550 && y.IsValidTarget());
        }

        #endregion
    }
}