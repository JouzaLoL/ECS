using System;
using LeagueSharp.Common;
using LeagueSharp;
using L = SmartKatarinaReborn.LogManager;

namespace SmartKatarinaReborn
{
    public class MenuManager
    {
        private static Menu _config;

        public static T Get<T>(string item)
        {
            return _config.Item(item).GetValue<T>();
        }

        public static void OnLoad()
        {
            InitMenu();
            L.Log("MenuManager initialized!");
        }

        private static void InitMenu()
        {
            _config = new Menu("SmartKatarina Reborn", "skr", true);

            var combo = new Menu("[SKR] Combo", "skr.combo");
            {
                combo.AddItem(new MenuItem("combo.mode", "Combo Mode")).SetValue(new StringList(new[] {"QEWR", "EQWR"}));
            }
            _config.AddSubMenu(combo);

            var global = new Menu("[SKR] Global", "skr.global");
            {
                global.AddItem(new MenuItem("global.procQ", "Wait for Q to land")).SetValue(true);
                global.AddItem(new MenuItem("global.cancelkey", "Ultimate FORCE Cancel Key")).SetValue(new KeyBind("T".ToCharArray()[0], KeyBindType.Press));
            }
            _config.AddSubMenu(global);

            _config.AddSubMenu(new Menu("[SKR] Orbwalker", "skr.orbwalker"));
            LogicManager.Orbwalker = new Orbwalking.Orbwalker(_config.SubMenu("skr.orbwalker"));

            _config.AddToMainMenu();
        }
    }
}