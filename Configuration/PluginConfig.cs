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

        /// <summary>同意済みなら、起動時の健康上の注意画面を飛ばすか。</summary>
        public virtual bool SkipHealthWarning { get; set; } = false;

        /// <summary>カット時に全方向へ飛ぶ火花の幅・長さ・時間を変更するか。OFF のときは何も書き換えない。</summary>
        public virtual bool ChangeNoteCutSparkles { get; set; } = false;

        /// <summary>全方向の火花の倍率（%、0–5000）。いずれかが 0 ならこの火花を表示しない。</summary>
        public virtual int CutSparkleWidthPercent { get; set; } = 0;
        public virtual int CutSparkleLengthPercent { get; set; } = 0;
        public virtual int CutSparkleDurationPercent { get; set; } = 0;

        /// <summary>全方向の火花の発生上限を 10 倍にするか（時間を伸ばしたときに火花が欠けないようにする）。</summary>
        public virtual bool CutSparkleLimit10x { get; set; } = false;

        /// <summary>全方向の火花に重力（地球の重力の 5%）を付けるか。</summary>
        public virtual bool CutSparkleGravity { get; set; } = false;

        public virtual bool RemoveDebris { get; set; } = false;
        public virtual bool RemoveSaberClashSparkles { get; set; } = false;
        public virtual bool RemoveWallSparkles { get; set; } = false;
        public virtual bool HideWallCore { get; set; } = false;

        /// <summary>カット時に斬撃方向へ伸びる火花の幅・長さ・時間を変更するか。OFF のときは何も書き換えない。</summary>
        public virtual bool ChangeSlashEffect { get; set; } = false;

        /// <summary>斬撃方向の火花の倍率（%、0–5000）。いずれかが 0 ならこの火花を表示しない。</summary>
        public virtual int SlashWidthPercent { get; set; } = 0;
        public virtual int SlashLengthPercent { get; set; } = 0;
        public virtual int SlashDurationPercent { get; set; } = 0;

        /// <summary>斬撃方向の火花の発生上限を 10 倍にするか。</summary>
        public virtual bool SlashLimit10x { get; set; } = false;
    }
}
