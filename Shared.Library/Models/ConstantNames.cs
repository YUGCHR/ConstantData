using System.Collections.Generic;
using CachingFramework.Redis.Contracts;

namespace Shared.Library.Models
{
    public class ConstantsLikeInJson
    {
        public List<ConstantNameValue> CNames { get; set; }
        public List<KeyNameTime> KNames { get; set; }
    }
    public class ConstantNameValue
    {
        public string ConstantName { get; set; }
        public string ConstantValue { get; set; }
    }
    public class KeyNameTime
    {
        public string KeyName { get; set; }
        public string LifeTime { get; set; }
    }
}
