using LeagueSharp.Common;
using LeagueSharp;
using System;

namespace SmartKatarinaReborn
{
    public class LogManager
    {

        public static void Log(string text)
        {         
            Console.WriteLine("[SKR]" + ": " + text);
        }
    }
}