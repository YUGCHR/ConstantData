using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Shared.Library.Models
{
    public class TaskDescriptionAndProgress
    {
        [JsonProperty(PropertyName = "tasksCount")]

        public int TasksCount { get; set; }

        public class TaskComplicatedDescription
        {
            [JsonProperty(PropertyName = "cycleCount")]
            public int CycleCount { get; set; }
        }

        public class TaskProgressState
        {
            [JsonProperty(PropertyName = "isTaskRunning")]
            public bool IsTaskRunning { get; set; }
        }
    }
}
