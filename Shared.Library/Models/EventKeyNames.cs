using CachingFramework.Redis.Contracts;

namespace Shared.Library.Models
{
    // common constants model for 3 solutions
    public class EventKeyNames
    {
        public int TaskEmulatorDelayTimeInMilliseconds { get; set; }
        public int RandomRangeExtended { get; set; }
        public int BalanceOfTasksAndProcesses { get; set; }
        public int MaxProcessesCountOnServer { get; set; }
        public int MinBackProcessesServersCount { get; set; } // for FrontEmulator only
        public string EventKeyFrom { get; set; }
        public string EventFieldFrom { get; set; }
        public KeyEvent EventCmd { get; set; }
        public string EventKeyBackReadiness { get; set; }
        public string EventFieldBack { get; set; }
        public string EventKeyFrontGivesTask { get; set; }
        public string PrefixRequest { get; set; }
        public string PrefixPackage { get; set; }
        public string PrefixPackageControl { get; set; }
        public string PrefixPackageCompleted { get; set; }
        public string PrefixTask { get; set; }
        public string PrefixBackServer { get; set; }
        public string BackServerGuid { get; set; }
        public string BackServerPrefixGuid { get; set; }
        public int RandomSeedFromGuid { get; set; } // NOT USED
        public string PrefixProcessAdd { get; set; }
        public string PrefixProcessCancel { get; set; }
        public string PrefixProcessCount { get; set; }
        public string EventFieldFront { get; set; }
        public string EventKeyBacksTasksProceed { get; set; }        
        public double EventKeyFromTimeDays { get; set; }
        public double EventKeyBackReadinessTimeDays { get; set; }
        public double EventKeyFrontGivesTaskTimeDays { get; set; }
        public double EventKeyBackServerMainTimeDays { get; set; }
        public double EventKeyBackServerAuxiliaryTimeDays { get; set; }
        public double PercentsKeysExistingTimeInMinutes { get; set; } // for Controller only
    }
}
