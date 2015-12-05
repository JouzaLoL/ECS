using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LeagueSharp;
using LeagueSharp.Common;

namespace SmartKatarinaReborn
{
    class Utilities
    {
        public static bool ProcQ(Obj_AI_Base target)
        {
            if (!MenuManager.Get<bool>("global.procQ")) //Ignore checks if specified
            {
                return true;
            }
            return target.HasBuff("KatarinaQMark") || (!QinAir() && !SpellManager.spells[SpellManager.Spells.Q].IsReady());
        }

        public static bool QinAir()
        {
            return ObjectManager.Get<MissileClient>().Any(missile => missile.SData.Name == "KatarinaQ" && missile.SpellCaster.IsMe);
        }

    }
}
