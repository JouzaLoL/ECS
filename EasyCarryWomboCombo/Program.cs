using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LeagueSharp;
using LeagueSharp.Common;

namespace EasyCarryWomboCombo
{
    class Program
    {
        private static Obj_AI_Hero _blitz;
        private static Obj_AI_Hero _kalista;
        private static Spell Q, R;
        private static Champ selected;
        private const int range = 2500;

        public enum Champ
        {
            Blitz,
            Kalista
        }

        private static List<Spell> _spells = new List<Spell>();

        static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += OnLoad;
        }

        private static void OnLoad(EventArgs args)
        {
            _blitz = ObjectManager.Get<Obj_AI_Hero>().FirstOrDefault(x => x.IsAlly && x.ChampionName == "Blitzcrank");
            _kalista = ObjectManager.Get<Obj_AI_Hero>().FirstOrDefault(x => x.IsAlly && x.ChampionName == "Kalista");

            if (_kalista == null)
            {
                Console.WriteLine("Kalista is not present.");
                return;
            }
                
            if (_blitz == null)
            {
                Console.WriteLine("Blitz is not present.");
                return;
            }
                

            if (_blitz.IsMe)
                Load(Champ.Blitz);
            
            else if (_kalista.IsMe)
                Load(Champ.Kalista);

            Game.OnUpdate += OnUpdate;
        }

        private static void OnUpdate(EventArgs args)
        {
            if (selected == Champ.Blitz)
            {
                var target = TargetSelector.GetTarget(range, TargetSelector.DamageType.Magical);
                if (target != null && Q.IsReady() && _kalista.Spellbook.GetSpell(SpellSlot.R).IsReady() && _kalista.Distance(target) < range && ObjectManager.Player.Distance(_kalista) < _kalista.Spellbook.GetSpell(SpellSlot.R).SData.CastRange)
                {
                    
                }
            }
        }

        private static void Load(Champ c)
        {
            switch (c)
            {
                case Champ.Kalista:
                    R = new Spell(SpellSlot.R, ObjectManager.Player.Spellbook.GetSpell(SpellSlot.R).SData.CastRange);
                    selected = Champ.Kalista;
                    break;
                case Champ.Blitz:
                    Q = new Spell(SpellSlot.Q, ObjectManager.Player.Spellbook.GetSpell(SpellSlot.Q).SData.CastRange);
                    selected = Champ.Blitz;
                    break;
            }
        }
    }
}
