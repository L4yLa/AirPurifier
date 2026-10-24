using IPA;
using IPALogger = IPA.Logging.Logger;

namespace AirPurifier
{
    [Plugin(RuntimeOptions.SingleStartInit), NoEnableDisable]
    public class Plugin
    {
        internal static IPALogger Log { get; private set; }

        [Init]
        public Plugin(IPALogger logger)
        {
            Log = logger;
        }

        [OnStart]
        public void OnApplicationStart()
        {
            DustRemover.Install();
        }
    }
}
