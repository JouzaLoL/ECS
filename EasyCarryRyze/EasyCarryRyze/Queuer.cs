using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LeagueSharp;
using LeagueSharp.Common;

namespace EasyCarryRyze
{
    internal class Queuer
    {
        public static List<Program.Spells> Queue = new List<Program.Spells>();
        public static Obj_AI_Base Target;
        public static Obj_AI_Hero Player = ObjectManager.Player;

        public static bool NoCollision;

        public static void DoQueue()
        {
            if (Queue.Count == 0) return;

            switch (Queue[0])
            {
                case Program.Spells.Q:
                    CastQ();
                    break;
                case Program.Spells.W:
                    CastW();
                    break;
                case Program.Spells.E:
                    CastE();
                    break;
                case Program.Spells.R:
                    CastR();
                    break;
            }
        }

        public static void Add(Program.Spells spell, Obj_AI_Base target, bool nocol = false)
        {
            Target = target;
            Queue.Add(spell);

        }

        public static void Add(Program.Spells spell)
        {
            Queue.Add(spell);
        }

        public static void Remove(Program.Spells spell)
        {
            if (Queue.Count == 0 || Queue[0] != spell) return;

            Queue.RemoveAt(0);   
        }

        private static void CastQ()
        {
            if (!Program.spells[Program.Spells.Q].CanCast(Target))
            {
                Remove(Program.Spells.Q);
                return;
            }

            var qpred = Program.spells[Program.Spells.Q].GetPrediction(Target).CollisionObjects.Count;
            if (NoCollision)
            {
                Program.spells[Program.Spells.Q].Cast(Target.ServerPosition);
            }
            else if (qpred <= 0)
            {
                Program.spells[Program.Spells.Q].Cast(Target.ServerPosition);
            }
        }

        private static void CastW()
        {
            if (!Program.spells[Program.Spells.W].CanCast(Target))
            {
                Remove(Program.Spells.W);
                return;
            }

            Program.spells[Program.Spells.Q].Cast(Target);
        }

        private static void CastE()
        {
            if (!Program.spells[Program.Spells.E].CanCast(Target))
            {
                Remove(Program.Spells.E);
                return;
            }

            Program.spells[Program.Spells.E].Cast(Target);
        }

        private static void CastR()
        {
            if (!Program.spells[Program.Spells.R].IsReady())
            {
                Remove(Program.Spells.R);
                return;
            }

            Program.spells[Program.Spells.R].Cast();
        }
    }
}
