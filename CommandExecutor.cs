using UnityEngine;
using UnityEngine.UI;

// The CommandExecutor is solely responsible for finding and executing strings to the F1 console.

namespace CtF
{
    public static class CommandExecutor
    {
        private static InputField consoleInputField;

        // Locate the console input field in the UI
        public static void InitializeConsole()
        {
            CtFLogger.Log("Searching for Game Console Panel...");
            var canvases = Resources.FindObjectsOfTypeAll<Canvas>();
            foreach (var canvas in canvases)
            {
                if (canvas.name.Equals("Game Console Panel", System.StringComparison.OrdinalIgnoreCase))
                {
                    consoleInputField = canvas.GetComponentInChildren<InputField>(true);
                    if (consoleInputField != null)
                    {
                        CtFLogger.Log("Found Game Console Panel.");
                    }
                    else
                    {
                        CtFLogger.Error("Could not find the Game Console Panel input field.");
                    }
                    break;
                }
            }
        }

        // Execute a command in the game console
        public static void ExecuteCommand(string command)
        {
            if (consoleInputField == null)
            {
                CtFLogger.Error("Cannot execute command - Console Input Field is null.");
                return;
            }
            consoleInputField.onEndEdit.Invoke(command);
        }
    }
}