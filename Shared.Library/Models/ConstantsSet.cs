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
        public ConstantType RecordActualityLevel { get; init; }
        public ConstantType TaskEmulatorDelayTimeInMilliseconds { get; init; }
        public ConstantType RandomRangeExtended { get; init; }
        public ConstantType BalanceOfTasksAndProcesses { get; init; }
        public ConstantType MaxProcessesCountOnServer { get; init; }
        public ConstantType MinBackProcessesServersCount { get; init; }

        // KeysList
        public KeyType EventKeyFrom { get; init; }
        public KeyType EventKeyBackReadiness { get; init; }
        public KeyType EventKeyFrontGivesTask { get; init; }
        public KeyType EventKeyUpdateConstants { get; init; }
        public KeyType EventKeyBacksTasksProceed { get; init; }
        public KeyType PrefixRequest { get; init; }
        public KeyType PrefixPackage { get; init; }
        public KeyType PrefixPackageControl { get; init; }
        public KeyType PrefixPackageCompleted { get; init; }
        public KeyType PrefixTask { get; init; }
        public KeyType PrefixBackServer { get; init; }
        public KeyType PrefixProcessAdd { get; init; }
        public KeyType PrefixProcessCancel { get; init; }
        public KeyType PrefixProcessCount { get; init; }
        public KeyType EventFieldFrom { get; init; }
        public KeyType EventFieldBack { get; init; }
        public KeyType EventFieldFront { get; init; }

        // LaterAssigned
    }

    public class ConstantType
    {
        public string Description { get; init; }
        public int Value { get; init; }
        public double LifeTime { get; init; }
    }

    public class KeyType
    {
        public string Description { get; init; }
        public string Value { get; init; }
        public double LifeTime { get; init; }
    }
}
