using System.Collections.Generic;
using CachingFramework.Redis.Contracts;

namespace Shared.Library.Models
{
    public class IsomorphicJsonConstantsStructure
    {
        public List<ConstantNameValue> ConstantsNames { get; set; }
        public List<KeyNameValueTime> KeysNames { get; set; }
        public List<PrefixKeyNameValueTime> PrefixKeysNames { get; set; }
        public List<FieldNameValueTime> FieldsNames { get; set; }
    }

    public class ConstantNameValue
    {
        public string ConstantDescription { get; set; }
        public string ConstantName { get; set; }
        public string ConstantValue { get; set; }
    }

    public class KeyNameValueTime
    {
        public string KeyName { get; set; }
        public string KeyValue { get; set; }
        public string LifeTime { get; set; }
    }

    public class PrefixKeyNameValueTime
    {
        public string PrefixKeyName { get; set; }
        public string PrefixKeyValue { get; set; }
        public string LifeTime { get; set; }
    }

    public class FieldNameValueTime
    {
        public string FieldName { get; set; }
        public string FieldValue { get; set; }
        public string LifeTime { get; set; }
    }
}
