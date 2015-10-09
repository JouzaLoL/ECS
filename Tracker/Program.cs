#region

using System;
using System.Reflection;
using LeagueSharp.Common;

#endregion

namespace WardTracker
{
    internal class Program
    {
        public static Menu Config;

        private static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += Game_OnGameLoad;
        }

        private static void Game_OnGameLoad(EventArgs args)
        {
            Config = new Menu("[EasyCarry] - Ward Tracker", "ecs.wardtracker", true);
            Config.AddItem(new MenuItem("wardtracker.showmore", "Show more information").SetValue(new KeyBind(16, KeyBindType.Press)));
            Config.AddItem(new MenuItem("ecs.wardtracker.enabled", "Enabled").SetValue(true));
            Config.AddToMainMenu();

            Notifications.AddNotification("EasyCarry - Ward Tracker Loaded", 5000);
            Notifications.AddNotification("Version: " + Assembly.GetExecutingAssembly().GetName().Version, 5000);
        }
    }
}