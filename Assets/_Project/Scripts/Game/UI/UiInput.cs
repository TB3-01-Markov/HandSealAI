using UnityEngine;
using UnityEngine.EventSystems;

namespace HandSeals.Game
{
    /// <summary>
    /// Touch and "back" input that works with whichever input backend the project uses:
    /// the built-in Input Manager, or the Input System package when only that one is active.
    /// </summary>
    internal static class UiInput
    {
        /// <summary>uGUI buttons need exactly one EventSystem in the scene; creates it when missing.</summary>
        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();

#if ENABLE_LEGACY_INPUT_MANAGER
            eventSystem.AddComponent<StandaloneInputModule>();
#elif HANDSEALS_INPUT_SYSTEM
            eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            Debug.LogWarning("HandSealAI: no input backend is available, buttons will not react.");
#endif
        }

        /// <summary>True in the frame the Android back button (Escape on a keyboard) was pressed.</summary>
        public static bool BackPressed()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#elif HANDSEALS_INPUT_SYSTEM
            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
#else
            return false;
#endif
        }
    }
}
