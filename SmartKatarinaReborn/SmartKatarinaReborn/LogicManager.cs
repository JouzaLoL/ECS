using System;
using System.Runtime.InteropServices;
using LeagueSharp;
using LeagueSharp.Common;

namespace SmartKatarinaReborn
{
    public class LogicManager
    {
        private static Spell Q => SpellManager.spells[SpellManager.Spells.Q];
        private static Spell W => SpellManager.spells[SpellManager.Spells.W];

        private static Spell E => SpellManager.spells[SpellManager.Spells.E];
        private static Spell R => SpellManager.spells[SpellManager.Spells.R];

        public static Orbwalking.Orbwalker Orbwalker;

        private static Obj_AI_Hero Target = TargetSelector.GetTarget(1000, TargetSelector.DamageType.Magical);
        private static Obj_AI_Hero Player = ObjectManager.Player;
        private static int Mode => MenuManager.Get<StringList>("combo.mode").SelectedIndex;

        public static void OnLoad()
        {
            Game.OnUpdate += OnUpdate;
            Obj_AI_Base.OnIssueOrder += OnOrder;
            LogManager.Log("LogicManager initialized!");
        }

        private static void OnOrder(Obj_AI_Base sender, GameObjectIssueOrderEventArgs args)
        {
            if (!sender.IsMe) return;

            if (MenuManager.Get<KeyBind>("global.cancelkey").Active)
            {
                args.Process = true;
            }
            else if (Player.HasBuff("KatarinaR") || Player.IsCastingInterruptableSpell())
            {
                args.Process = false;
            }
        }

        private static void OnUpdate(EventArgs args)
        {
            switch (Orbwalker.ActiveMode)
            {
                case Orbwalking.OrbwalkingMode.Combo:
                    Combo();
                    break;

                case Orbwalking.OrbwalkingMode.LastHit:
                    Lasthit();
                    break;
            }
        }
        
        private static void Combo()
        {
            if (Target == null) return;

            switch (Mode)
            {
                case 0: //QEWR
                    if (Q.CanCast(Target))
                        Q.Cast(Target);
                    if (E.CanCast(Target) && Utilities.ProcQ(Target))
                        E.Cast(Target);
                    if (W.CanCast(Target))
                        W.Cast(Target);
                    if (!Q.IsReady() && !W.IsReady() && !E.IsReady() && R.CanCast(Target))
                        R.Cast();
                    break;

                case 1: //EQWR
                    if (E.CanCast(Target))
                        E.Cast(Target);
                    if (Q.CanCast(Target))
                        Q.Cast(Target);                    
                    if (W.CanCast(Target) && Utilities.ProcQ(Target))
                        W.Cast(Target);
                    if (!Q.IsReady() && !W.IsReady() && !E.IsReady() && R.CanCast(Target))
                        R.Cast();
                    break;
            }
        }

        private static void Lasthit()
        {
            var minions = MinionManager.GetMinions(Q.Range);
            var useE = MenuManager.Get<bool>("lasthit.useE");

            foreach (var m in minions)
            {
                if (W.IsKillable(m) && W.CanCast(m))
                    W.Cast();
                else if (Q.IsKillable(m) && Q.CanCast(m))
                    Q.Cast(m);
                else if (E.IsKillable(m) && E.CanCast(m) && useE && !m.UnderTurret(true))
                    E.Cast(m);
            }
        }
    }
}