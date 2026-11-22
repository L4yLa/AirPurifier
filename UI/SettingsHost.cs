using System;
using AirPurifier.Configuration;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Parser;
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
        private const string SlashTag = "slash";

        private static readonly SettingsHost s_instance = new SettingsHost();

        private static PluginConfig C => PluginConfig.Instance;

        [UIParams] private readonly BSMLParserParams _parserParams = null;

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

        [UIValue("debris")]
        public bool RemoveDebris { get => C.RemoveDebris; set => C.RemoveDebris = value; }

        [UIValue("saber-clash-sparkles")]
        public bool RemoveSaberClashSparkles { get => C.RemoveSaberClashSparkles; set => C.RemoveSaberClashSparkles = value; }

        [UIValue("wall-sparkles")]
        public bool RemoveWallSparkles { get => C.RemoveWallSparkles; set => C.RemoveWallSparkles = value; }

        [UIValue("wall-core")]
        public bool HideWallCore { get => C.HideWallCore; set => C.HideWallCore = value; }

        [UIValue("change-slash")]
        public bool ChangeSlashEffect { get => C.ChangeSlashEffect; set => C.ChangeSlashEffect = value; }

        [UIValue("slash-width")]
        public int SlashWidthPercent { get => C.SlashWidthPercent; set => C.SlashWidthPercent = value; }

        [UIValue("slash-length")]
        public int SlashLengthPercent { get => C.SlashLengthPercent; set => C.SlashLengthPercent = value; }

        [UIValue("slash-duration")]
        public int SlashDurationPercent { get => C.SlashDurationPercent; set => C.SlashDurationPercent = value; }

        [UIAction("percent-formatter")]
        public string PercentFormatter(object value) => $"{Convert.ToInt32(value)}%";

        [UIAction("#post-parse")]
        public void PostParse()
        {
            try
            {
                SetSlashSlidersVisible(C.ChangeSlashEffect);
            }
            catch (Exception e)
            {
                Plugin.Log.Error($"Settings post-parse failed: {e}");
            }
        }

        /// <summary>
        /// BSML のトグルは値を設定へ反映する前にこのハンドラを呼ぶため、設定値ではなく渡された値を使う。
        /// 例外を外へ出すと HMUI の入力処理が壊れるため、ここで止める。
        /// </summary>
        [UIAction("change-slash-changed")]
        public void OnChangeSlashChanged(object value)
        {
            try
            {
                SetSlashSlidersVisible(value is bool on ? on : C.ChangeSlashEffect);
            }
            catch (Exception e)
            {
                Plugin.Log.Error($"Failed to toggle slash settings: {e}");
            }
        }

        private void SetSlashSlidersVisible(bool visible)
        {
            var objects = _parserParams?.GetObjectsWithTag(SlashTag);
            if (objects == null)
            {
                return;
            }

            foreach (var obj in objects)
            {
                if (obj != null)
                {
                    obj.SetActive(visible);
                }
            }
        }
    }
}
