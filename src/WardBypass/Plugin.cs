using System;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace WardBypass
{
    [BepInPlugin(ModInfo.Guid, ModInfo.Name, ModInfo.Version)]
    public sealed class Plugin : BaseUnityPlugin
    {
        private ConfigEntry<bool> modEnabled;
        private ConfigEntry<bool> activeOnStart;
        private ConfigEntry<KeyboardShortcut> toggleKey;
        private ConfigEntry<bool> locatorOnStart;
        private ConfigEntry<KeyboardShortcut> locatorKey;
        private Harmony harmony;
        private bool failed;
        private bool locatorFailed;

        private void Awake()
        {
            modEnabled = Config.Bind("General", "Enabled", true, "Enable Ward Bypass. Restart after changing the configuration file.");
            activeOnStart = Config.Bind("General", "ActiveOnStart", true, "Whether the bypass is active when the game starts. The toggle key switches it during play.");
            toggleKey = Config.Bind("Keys", "Toggle", new KeyboardShortcut(KeyCode.F11), "Turn the bypass on or off during play.");
            locatorKey = Config.Bind("Keys", "Locator", new KeyboardShortcut(KeyCode.F10), "Show or hide the ward locator during play.");
            locatorOnStart = Config.Bind("Locator", "ActiveOnStart", false, "Whether the ward locator is shown when the game starts. The locator key switches it during play.");
            Locator.Bind(Config);
            if (!modEnabled.Value)
            {
                Logger.LogInfo($"{ModInfo.Name} {ModInfo.Version} is disabled in its configuration file.");
                return;
            }
            if (Application.isBatchMode)
            {
                // A dedicated server has no local player, so there is nothing to bypass there.
                Logger.LogInfo($"{ModInfo.Name} {ModInfo.Version} is client-only and does nothing on a dedicated server.");
                return;
            }

            Bypass.Active = activeOnStart.Value;
            harmony = new Harmony(ModInfo.Guid);
            harmony.PatchAll(typeof(Plugin).Assembly);
            if (Locator.Initialize(out string locatorError)) Locator.Active = locatorOnStart.Value;
            else
            {
                locatorFailed = true;
                Logger.LogError($"The ward locator is unavailable in this game version: {locatorError}");
            }
            Logger.LogInfo($"{ModInfo.Name} {ModInfo.Version} loaded. Bypass {(Bypass.Active ? "on" : "off")}; {toggleKey.Value} toggles it. " +
                $"Locator {(Locator.Active ? "on" : "off")}; {locatorKey.Value} toggles it.");
        }

        private void Update()
        {
            if (harmony == null) return;
            if (!failed)
            {
                try
                {
                    if (Pressed(toggleKey.Value) && CanTakeInput())
                    {
                        Bypass.Active = !Bypass.Active;
                        Player player = Player.m_localPlayer;
                        if (player) player.Message(MessageHud.MessageType.TopLeft, Bypass.Active ? "Ward bypass on" : "Ward bypass off");
                    }
                }
                catch (Exception error)
                {
                    failed = true;
                    Logger.LogError($"The Ward Bypass toggle key stopped working for this session: {error}");
                }
            }
            if (locatorFailed) return;
            try
            {
                if (Pressed(locatorKey.Value) && CanTakeInput())
                {
                    int wards = Locator.Toggle();
                    Player player = Player.m_localPlayer;
                    if (player)
                        player.Message(MessageHud.MessageType.TopLeft, Locator.Active
                            ? $"Ward locator on: {wards} ward{(wards == 1 ? "" : "s")} nearby"
                            : "Ward locator off");
                }
                Locator.Update();
            }
            catch (Exception error)
            {
                StopLocator(error);
            }
        }

        private void OnGUI()
        {
            if (harmony == null || locatorFailed || !Locator.Active) return;
            try
            {
                Locator.OnGUI();
            }
            catch (Exception error)
            {
                StopLocator(error);
            }
        }

        private void StopLocator(Exception error)
        {
            locatorFailed = true;
            Locator.Dispose();
            Logger.LogError($"The ward locator stopped working for this session: {error}");
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
            harmony = null;
            Bypass.Active = false;
            Locator.Dispose();
        }

        private static bool CanTakeInput()
        {
            Player player = Player.m_localPlayer;
            return player && !global::Console.IsVisible() && !(Chat.instance && Chat.instance.HasFocus()) &&
                !TextInput.IsVisible() && !Menu.IsVisible() && !InventoryGui.IsVisible() && !Minimap.IsOpen();
        }

        // Keys go through the game's input layer. A key without a counterpart there counts as not pressed.
        private static bool Pressed(KeyboardShortcut shortcut)
        {
            if (shortcut.MainKey == KeyCode.None) return false;
            try
            {
                if (!ZInput.GetKeyDown(shortcut.MainKey, false)) return false;
                foreach (KeyCode modifier in shortcut.Modifiers)
                    if (!ZInput.GetKey(modifier, false)) return false;
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
}
