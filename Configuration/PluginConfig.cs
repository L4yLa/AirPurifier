using System.Runtime.CompilerServices;
using IPA.Config.Stores;

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]

namespace AirPurifier.Configuration
{
    /// <summary>
    /// BSIPA Generated Config。実体は Beat Saber\UserData\AirPurifier.json に生成される。
    /// ダストの除去は常に有効で、ここには含めない。
    /// </summary>
    internal class PluginConfig
    {
        public static PluginConfig Instance { get; set; }

        /// <summary>カット時に全方向へ飛ぶ火花を消すか。斬撃方向の火花は Change Slash Effect で扱う。</summary>
        public virtual bool RemoveNoteCutSparkles { get; set; } = false;
        public virtual bool RemoveDebris { get; set; } = false;
        public virtual bool RemoveSaberClashSparkles { get; set; } = false;
        public virtual bool RemoveWallSparkles { get; set; } = false;
        public virtual bool HideWallCore { get; set; } = false;

        /// <summary>カット時に斬撃方向へ伸びる火花の幅・長さ・時間を変更するか。OFF のときは何も書き換えない。</summary>
        public virtual bool ChangeSlashEffect { get; set; } = false;

        /// <summary>斬撃方向の火花の倍率（%、0–300）。いずれかが 0 ならこの火花を表示しない。</summary>
        public virtual int SlashWidthPercent { get; set; } = 0;
        public virtual int SlashLengthPercent { get; set; } = 0;
        public virtual int SlashDurationPercent { get; set; } = 0;
    }
}
