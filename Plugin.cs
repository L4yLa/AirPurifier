using System;
using AirPurifier.Configuration;
using AirPurifier.UI;
using HarmonyLib;
using IPA;
using IPA.Config;
using IPA.Config.Stores;
using IPALogger = IPA.Logging.Logger;

namespace AirPurifier
{
    [Plugin(RuntimeOptions.SingleStartInit), NoEnableDisable]
    public class Plugin
    {
        private const string HarmonyId = "com.l4yla.AirPurifier";

        internal static IPALogger Log { get; private set; }

        [Init]
        public Plugin(IPALogger logger, Config config)
        {
            Log = logger;
            PluginConfig.Instance = config.Generated<PluginConfig>();
        }

        [OnStart]
        public void OnApplicationStart()
        {
            var harmony = new Harmony(HarmonyId);

            DustRemover.Install(harmony);

            try
            {
                EffectRemover.Install(harmony);
            }
            catch (Exception e)
            {
                Log.Error($"Failed to install effect patches: {e}");
            }

            SettingsHost.Install();
        }
    }
}
