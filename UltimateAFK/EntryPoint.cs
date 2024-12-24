using GameCore;
using PluginAPI.Core;
using PluginAPI.Core.Attributes;

namespace UltimateAFK
{
    /// <summary>
    /// Plugin main class
    /// </summary>
    public class EntryPoint
    {
        /// <summary>
        /// Gets the singleton instance of the plugin.
        /// </summary>
        public static EntryPoint Instance { get; private set; } = null!;

        /// <summary>
        /// Plugin config
        /// </summary>
        [PluginConfig]
        public Config Config = null!;

        /// <summary>
        /// Gets the plugin version.
        /// </summary>
        public const string Version = "7.0.6";

        /// <summary>
        /// Called when loading the plugin
        /// </summary>
        [PluginPriority(PluginAPI.Enums.LoadPriority.High)]
        [PluginEntryPoint("UltimateAFK", Version, "Plugin that checks if a player is afk for too long and if detected as afk will be replaced by a spectator.", "SrLicht")]
        private void OnLoad()
        {
            Instance = this;

            if (!Config.IsEnabled)
            {
                PluginAPI.Core.Log.Warning($"UltimateAfk was disabled through configuration.");
                return;
            }

            PluginAPI.Events.EventManager.RegisterEvents(Instance, new MainHandler());

            PluginAPI.Core.Log.Info($"UltimateAfk {Version} fully loaded.");
        }

        /// <summary>
        /// Called when unloading the plugin
        /// </summary>
        [PluginUnload]
        private void OnUnload()
        {
            PluginAPI.Events.EventManager.UnregisterEvents(Instance);
        }
    }
}
