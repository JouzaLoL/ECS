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
            L.Log("MenuManager initialized!", L.Type.Info);
        }

        private static void InitMenu()
        {
            _config = new Menu("SmartKatarina Reborn", "skr", true);

            var combo = new Menu("[SKR] Combo", "skr.combo");
            {
                combo.AddItem(new MenuItem())
            }
        }
    }
}