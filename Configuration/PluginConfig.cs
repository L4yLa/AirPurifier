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

        public virtual bool RemoveNoteCutSparkles { get; set; } = false;
        public virtual bool RemoveNoteCutFlash { get; set; } = false;
        public virtual bool RemoveDebris { get; set; } = false;
        public virtual bool RemoveSaberClashSparkles { get; set; } = false;
        public virtual bool RemoveWallSparkles { get; set; } = false;
        public virtual bool HideWallCore { get; set; } = false;
    }
}
