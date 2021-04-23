using System.Collections.Generic;
using CachingFramework.Redis.Contracts;

namespace Shared.Library.Models
{
    public class IsomorphicJsonConstantsStructure
    {
        public List<ConstantNameValue> ConstantsList { get; set; }
        public List<KeyNameValueLifeTime> KeysList { get; set; }
    }

    public class ConstantNameValue
    {
        public string Description { get; set; }
        public string PropertyName { get; set; }
        public int Value { get; set; }
        public double LifeTime { get; set; }
    }

    public class KeyNameValueLifeTime
    {
        public string Description { get; set; }
        public string PropertyName { get; set; }
        public string Value { get; set; }
        public double LifeTime { get; set; }
    }
}
