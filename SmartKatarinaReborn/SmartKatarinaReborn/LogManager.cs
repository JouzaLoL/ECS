using LeagueSharp.Common;
using LeagueSharp;
using System;

namespace SmartKatarinaReborn
{
    public class LogManager
    {
        internal enum Type
        {
            Info,
            Error,
        }

        public static void Log(string text, Type type)
        {
            string t = "";
            switch (type)
            {
                case Type.Error:
                    t = "Error";
                    break;
                case Type.Info:
                    t = "Info";
                    break;
            }
            Console.WriteLine("[SKR] " + t + ": " + text);
        }
    }
}