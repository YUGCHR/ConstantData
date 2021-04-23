using System.Collections.Generic;
using CachingFramework.Redis.Contracts;

namespace Shared.Library.Models
{
    public class IsomorphicJsonConstantsStructure
    {
        public List<ConstantNameValue> ConstantsList { get; set; }
    }

    public class ConstantNameValue
    {
        public string Description { get; set; }
        public string PropertyName { get; set; }
        public string Value { get; set; }
        public string LifeTime { get; set; }

    }
}
