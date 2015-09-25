using System;
using ColossalFramework.Plugins;
using UnityEngine;

namespace CSL_Traffic
{
    static class Logger
    {
        private static readonly string Prefix = "Traffic++: ";
        //private static readonly bool inGameDebug = Environment.OSVersion.Platform != PlatformID.Unix;

        public static void LogInfo(string message, params object[] args)
        {
            var msg = Prefix + String.Format(message, args);
            Debug.Log(msg);
            //if (inGameDebug)
            //    DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, msg);
        }

        public static void LogWarning(string message, params object[] args)
        {
            var msg = Prefix + String.Format(message, args);
            Debug.LogWarning(msg);
            //if (inGameDebug)
            //    DebugOutputPanel.AddMessage(PluginManager.MessageType.Warning, msg);
        }

        public static void LogError(string message, params object[] args)
        {
            var msg = Prefix + String.Format(message, args);
            Debug.LogError(msg);
            //if (inGameDebug)
            //    DebugOutputPanel.AddMessage(PluginManager.MessageType.Error, msg);
        }
    }
}
