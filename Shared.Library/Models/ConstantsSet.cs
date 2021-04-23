using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Library.Models
{
    // former EventKeyNames
    public class ConstantsSet
    {
        // ConstantsList
        public ConstantType RecordActualityLevel { get; set; }
        public ConstantType TaskEmulatorDelayTimeInMilliseconds { get; set; }
        public ConstantType RandomRangeExtended { get; set; }
        public ConstantType BalanceOfTasksAndProcesses { get; set; }
        public ConstantType MaxProcessesCountOnServer { get; set; }
        public ConstantType MinBackProcessesServersCount { get; set; }

        // KeysList
        public KeyType EventKeyFrom { get; set; }
        public KeyType EventKeyBackReadiness { get; set; }
        public KeyType EventKeyFrontGivesTask { get; set; }
        public KeyType EventKeyUpdateConstants { get; set; }
        public KeyType EventKeyBacksTasksProceed { get; set; }
        public KeyType PrefixRequest { get; set; }
        public KeyType PrefixPackage { get; set; }
        public KeyType PrefixPackageControl { get; set; }
        public KeyType PrefixPackageCompleted { get; set; }
        public KeyType PrefixTask { get; set; }
        public KeyType PrefixBackServer { get; set; }
        public KeyType PrefixProcessAdd { get; set; }
        public KeyType PrefixProcessCancel { get; set; }
        public KeyType PrefixProcessCount { get; set; }
        public KeyType EventFieldFrom { get; set; }
        public KeyType EventFieldBack { get; set; }
        public KeyType EventFieldFront { get; set; }

        // LaterAssigned
    }

    public class ConstantType
    {
        public string Description { get; set; }
        public int Value { get; set; }
        public double LifeTime { get; set; }
    }

    public class KeyType
    {
        public string Description { get; set; }
        public string Value { get; set; }
        public double LifeTime { get; set; }
    }
}
