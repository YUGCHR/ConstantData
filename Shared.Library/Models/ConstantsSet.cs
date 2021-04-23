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
        public ConstantNameValue RecordActualityLevel { get; set; }
        public ConstantNameValue TaskEmulatorDelayTimeInMilliseconds { get; set; }
        public ConstantNameValue RandomRangeExtended { get; set; }
        public ConstantNameValue BalanceOfTasksAndProcesses { get; set; }
        public ConstantNameValue MaxProcessesCountOnServer { get; set; }
        public ConstantNameValue MinBackProcessesServersCount { get; set; }

        // KeysList
        public KeyNameValueLifeTime EventKeyFrom { get; set; }
        public KeyNameValueLifeTime EventKeyBackReadiness { get; set; }
        public KeyNameValueLifeTime EventKeyFrontGivesTask { get; set; }
        public KeyNameValueLifeTime EventKeyUpdateConstants { get; set; }
        public KeyNameValueLifeTime EventKeyBacksTasksProceed { get; set; }
        public KeyNameValueLifeTime PrefixRequest { get; set; }
        public KeyNameValueLifeTime PrefixPackage { get; set; }
        public KeyNameValueLifeTime PrefixPackageControl { get; set; }
        public KeyNameValueLifeTime PrefixPackageCompleted { get; set; }
        public KeyNameValueLifeTime PrefixTask { get; set; }
        public KeyNameValueLifeTime PrefixBackServer { get; set; }
        public KeyNameValueLifeTime PrefixProcessAdd { get; set; }
        public KeyNameValueLifeTime PrefixProcessCancel { get; set; }
        public KeyNameValueLifeTime PrefixProcessCount { get; set; }
        public KeyNameValueLifeTime EventFieldFrom { get; set; }
        public KeyNameValueLifeTime EventFieldBack { get; set; }
        public KeyNameValueLifeTime EventFieldFront { get; set; }

        // LaterAssigned
    }
}
