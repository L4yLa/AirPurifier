using System;
using AirPurifier.Configuration;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Settings;
using BeatSaberMarkupLanguage.Util;

namespace AirPurifier.UI
{
    /// <summary>
    /// BSML の Mod Settings に表示する設定画面のホスト。
    /// 変更は次にゲームシーンを読み込んだときから反映される。
    /// </summary>
    internal class SettingsHost
    {
        private const string MenuName = "Air Purifier";
        private const string Resource = "AirPurifier.UI.settings.bsml";

        private static readonly SettingsHost s_instance = new SettingsHost();

        private static PluginConfig C => PluginConfig.Instance;

        /// <summary>
        /// メインメニューの初期化のたびに登録する。BSMLSettings はメニューの再構築で作り直されるため、
        /// 一度の登録では足りない。同名メニューの重複登録は BSML 側で無視される。
        /// </summary>
        internal static void Install()
        {
            MainMenuAwaiter.MainMenuInitializing += Register;
        }

        private static void Register()
        {
            try
            {
                BSMLSettings.Instance.AddSettingsMenu(MenuName, Resource, s_instance);
            }
            catch (Exception e)
            {
                Plugin.Log.Error($"Failed to register the settings menu: {e}");
            }
        }

        [UIValue("note-cut-sparkles")]
        public bool RemoveNoteCutSparkles { get => C.RemoveNoteCutSparkles; set => C.RemoveNoteCutSparkles = value; }

        [UIValue("note-cut-flash")]
        public bool RemoveNoteCutFlash { get => C.RemoveNoteCutFlash; set => C.RemoveNoteCutFlash = value; }

        [UIValue("debris")]
        public bool RemoveDebris { get => C.RemoveDebris; set => C.RemoveDebris = value; }

        [UIValue("saber-clash-sparkles")]
        public bool RemoveSaberClashSparkles { get => C.RemoveSaberClashSparkles; set => C.RemoveSaberClashSparkles = value; }

        [UIValue("wall-sparkles")]
        public bool RemoveWallSparkles { get => C.RemoveWallSparkles; set => C.RemoveWallSparkles = value; }

        [UIValue("wall-core")]
        public bool HideWallCore { get => C.HideWallCore; set => C.HideWallCore = value; }
    }
}
