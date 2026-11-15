using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace Pathologic2SmoothCombat
{
    // Pathologic 2 runs physics at a 0.033 s step (~30 Hz). Two combat visuals are tied to that step:
    // - the player's block stance: the BlockStance/Stamina animator parameters are advanced only in FixedUpdate
    //   (see BlockSmoothing);
    // - ragdolls (falling, knocked down and dead NPCs): bone rigidbodies have no interpolation, so the body pose
    //   changes only on physics steps (see RagdollInterpolation).
    // Both fixes only change what is drawn; gameplay values are still computed by the game on its physics step.
    [BepInPlugin(Guid, Name, Version)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "pathologic2.smoothcombat";
        public const string Name = "Pathologic 2 Smooth Combat";
        public const string Version = "1.0.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> SmoothBlock;
        internal static ConfigEntry<bool> SmoothRagdolls;
        internal static ConfigEntry<KeyboardShortcut> ToggleKey;
        internal static ConfigEntry<bool> ShowMessages;
        internal static ConfigEntry<bool> LogRagdollFrameTimes;

        internal static bool BlockActive => Enabled.Value && SmoothBlock.Value;
        internal static bool RagdollsActive => Enabled.Value && SmoothRagdolls.Value;

        private static string osdText;
        private static float osdUntil;
        private static GUIStyle osdStyle;

        private void Awake()
        {
            Log = Logger;
            Enabled = Config.Bind("General", "Enabled", true,
                "Master switch for all fixes below. Toggled in game with ToggleKey.");
            ToggleKey = Config.Bind("General", "ToggleKey", new KeyboardShortcut(KeyCode.End),
                "Key that toggles Enabled in game to compare with vanilla. The game itself does not use End.");
            ShowMessages = Config.Bind("General", "ShowMessages", true, "Show an on-screen message when toggling.");
            SmoothBlock = Config.Bind("Fixes", "SmoothBlock", true,
                "Raise and lower the player's block (fists, knife, scalpel, flashlight) every frame instead of in ~30 Hz steps. " +
                "Also smooths the low-stamina hand pose.");
            SmoothRagdolls = Config.Bind("Fixes", "SmoothRagdolls", true,
                "Interpolate ragdoll bones (falling, knocked down and dead NPCs) so the body moves every frame instead of ~30 times per second.");
            LogRagdollFrameTimes = Config.Bind("Diagnostics", "LogRagdollFrameTimes", false,
                "After each ragdoll activation, write the longest frame of the next second to BepInEx\\LogOutput.log, " +
                "to check that the ragdoll fix does not cause hitches.");

            try
            {
                new Harmony(Guid).PatchAll(typeof(Plugin).Assembly);
            }
            catch (Exception e)
            {
                Log.LogError("Failed to apply patches, mod disabled: " + e);
                enabled = false;
                return;
            }
            Log.LogInfo($"{Name} {Version}: Enabled={Enabled.Value}, SmoothBlock={SmoothBlock.Value}, " +
                        $"SmoothRagdolls={SmoothRagdolls.Value}, ToggleKey={ToggleKey.Value}, " +
                        $"fixedDeltaTime={Time.fixedDeltaTime:0.####} s");
        }

        private void Update()
        {
            if (IsPressed(ToggleKey.Value))
            {
                Enabled.Value = !Enabled.Value;
                RagdollInterpolation.ApplyToActive();
                string text = Enabled.Value ? "Smooth Combat: on" : "Smooth Combat: off (vanilla ~30 Hz)";
                Log.LogInfo(text);
                ShowMessage(text);
            }
            FrameMonitor.Tick();
        }

        // BepInEx's KeyboardShortcut.IsDown() does not fire while any unrelated key is held (WASD, mouse buttons).
        // Here unrelated keys do not interfere: only the main key and the modifiers listed in the shortcut matter.
        private static bool IsPressed(KeyboardShortcut shortcut)
        {
            if (shortcut.MainKey == KeyCode.None || !Input.GetKeyDown(shortcut.MainKey))
            {
                return false;
            }
            foreach (KeyCode modifier in shortcut.Modifiers)
            {
                if (!Input.GetKey(modifier))
                {
                    return false;
                }
            }
            return true;
        }

        private void OnGUI()
        {
            if (osdText == null || Time.unscaledTime > osdUntil || !ShowMessages.Value)
            {
                return;
            }
            GUI.depth = -1000;
            if (osdStyle == null)
            {
                osdStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            }
            var rect = new Rect(24, 104, 900, 40);
            var shadow = new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height);
            osdStyle.normal.textColor = Color.black;
            GUI.Label(shadow, osdText, osdStyle);
            osdStyle.normal.textColor = Color.white;
            GUI.Label(rect, osdText, osdStyle);
        }

        private static void ShowMessage(string text)
        {
            osdText = text;
            osdUntil = Time.unscaledTime + 2.5f;
        }
    }
}
