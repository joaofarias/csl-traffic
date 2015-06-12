using System;
using System.IO;
using ColossalFramework.Plugins;
using UnityEngine;

namespace CSL_Traffic
{
    static class Logger
    {
        private const string PREFIX = "Traffic++: ";
        private static readonly object SyncRoot = new object();
        private static readonly StreamWriter LogWriter = new StreamWriter(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Colossal Order\Cities_Skylines\Addons\Mods\Traffic++\DebugLog.log"), false);
        private static readonly bool InGameDebug = Environment.OSVersion.Platform != PlatformID.Unix;

        public static void LogInfo(string message, params object[] args)
        {
            string msg = PREFIX + String.Format(message, args);
            Debug.Log(msg);
            // FIXME: this is causing crashes for some reason
            //if (inGameDebug)
            //    DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, msg);
        }

        public static void LogWarning(string message, params object[] args)
        {
            string msg = PREFIX + String.Format(message, args);
            Debug.LogWarning(msg);
            if (InGameDebug)
                DebugOutputPanel.AddMessage(PluginManager.MessageType.Warning, msg);
        }

        public static void LogError(string message, params object[] args)
        {
            string msg = PREFIX + String.Format(message, args);
            Debug.LogError(msg);
            if (InGameDebug)
                DebugOutputPanel.AddMessage(PluginManager.MessageType.Error, msg);
        }

        public static void LogToFile(string message, params object[] args)
        {
            lock (SyncRoot)
            {
                string msg = "[" + Time.realtimeSinceStartup + "]" + PREFIX + String.Format(message, args);
                LogWriter.WriteLine(msg);
                //DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, msg);
                Debug.Log(msg);
                LogWriter.Flush();
            }
        }
    }
}
