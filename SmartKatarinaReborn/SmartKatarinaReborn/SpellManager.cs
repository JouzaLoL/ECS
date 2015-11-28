using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LeagueSharp;
using LeagueSharp.Common;
using L = SmartKatarinaReborn.LogManager;
using SharpDX;

namespace SmartKatarinaReborn
{
    internal class SpellManager
    {
        private static readonly Dictionary<Spells, Spell> spells = new Dictionary<Spells, Spell>
        {
            { Spells.Q, new Spell(SpellSlot.Q, 675)},
            { Spells.W, new Spell(SpellSlot.W, 375)},
            { Spells.E, new Spell(SpellSlot.E, 700)},
            { Spells.R, new Spell(SpellSlot.R, 550)}
        };

        internal enum Spells
        {
            Q,
            W,
            E,
            R
        }

        internal static void OnLoad()
        {
            spells[Spells.Q].SetTargetted((float)0.3, 400);
            spells[Spells.R].SetCharged("KatarinaR", "KatarinaR", 550, 550, 1.0f);
            L.Log("SpellManager initialized!");
        }
    }
}
