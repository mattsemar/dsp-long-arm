using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;

namespace LongArm.Util
{
    public static class Log
    {
        public static ManualLogSource logger;

        public static void Debug(string message)
        {
            logger.LogDebug($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
        }

        public static void Info(string message)
        {
            logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
        }

        public static void Warn(string message)
        {
            logger.LogWarning($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
        }

        public static void LogAndPopupMessage(string message)
        {
            UIRealtimeTip.Popup(message, new Vector2(Screen.currentResolution.width / 2, Screen.currentResolution.height / 2));
            logger.LogWarning($"Popped up message {message}");
        }


        private static Dictionary<string, DateTime> _lastPopupTime = new Dictionary<string, DateTime>();

        public static void LogPopupWithFrequency(string msgTemplate, params object[] args)
        {
            if (!_lastPopupTime.TryGetValue(msgTemplate, out DateTime lastTime))
                lastTime = DateTime.Now.Subtract(TimeSpan.FromSeconds(500));
            try
            {
                var msg = string.Format(msgTemplate, args);
                if ((DateTime.Now - lastTime).TotalMinutes < 2)
                {
                    Debug($"(Popup suppressed) {msg}");
                    return;
                }

                _lastPopupTime[msgTemplate] = DateTime.Now;
                LogAndPopupMessage(msg);
            }
            catch (Exception e)
            {
                Warn($"exception with popup: {e.Message}\r\n {e}\r\n{e.StackTrace}\r\n{msgTemplate}");
            }
        }     
        
        private static Dictionary<string, DateTime> _lastMessageTime = new Dictionary<string, DateTime>();

        public static void LogMessageWithFrequency(string msgTemplate, params object[] args)
        {
            if (!_lastMessageTime.TryGetValue(msgTemplate, out DateTime lastTime))
                lastTime = DateTime.Now.Subtract(TimeSpan.FromSeconds(500));
            try
            {
                var msg = string.Format(msgTemplate, args);
                if ((DateTime.Now - lastTime).TotalMinutes < 2)
                {
                    return;
                }

                _lastMessageTime[msgTemplate] = DateTime.Now;
                logger.LogWarning(msg);
            }
            catch (Exception e)
            {
                Warn($"exception with freq msg: {e.Message}\r\n {e}\r\n{e.StackTrace}\r\n{msgTemplate}");
            }
        }
    }
}