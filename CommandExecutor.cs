using System;
using HoldfastBridge;
using UnityEngine;

namespace CtF
{
    public static class CommandExecutor
    {
        private static IHoldfastGameMethods _gameMethods;
        public static bool IsServer;

        public static void Initialize(IHoldfastGameMethods holdfastGameMethods)
        {
            _gameMethods = holdfastGameMethods;
            if (_gameMethods == null)
            {
                Debug.LogError("[CtF] Console not found.");
                return;
            }

            Debug.Log("[CtF] Console found.");
        }

        public static void ExecuteCommand(string command)
        {
            if (_gameMethods == null)
            {
                CtFLogger.Error("Cannot execute command: CommandExecutor is not initialized.");
                return;
            }

            if (string.IsNullOrWhiteSpace(command))
            {
                CtFLogger.Warn("Cannot execute command: command was null or empty.");
                return;
            }

            if (!IsServer)
            {
                CtFLogger.Log($"Server side command fired: {command}");
                return;
            }

            _gameMethods.ExecuteConsoleCommand(command, out var output, out Exception exception);

            if (exception != null)
            {
                CtFLogger.Error($"Failed to execute command '{command}': {exception}");
                return;
            }
        }
    }
}