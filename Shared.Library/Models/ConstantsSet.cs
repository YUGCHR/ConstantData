﻿using System;
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
