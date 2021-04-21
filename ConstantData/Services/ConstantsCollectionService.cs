using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ConstantData.Services
{
    public interface IConstantsCollectionService
    {
        public Dictionary<string, int> Constants { get; }
        //public Dictionary<string, int> RedisKeysMain { get; }
        //public List<(string a, int b)> RedisKeysMain { get; }
        public List<IConfigurationSection> RedisKeysMain { get; }


        public Dictionary<string, int> RedisKeyPrefixes { get; }
        public Dictionary<string, int> RedisFields { get; }
    }

    public class ConstantsCollectionService : IConstantsCollectionService
    {
        public ConstantsCollectionService(IConfiguration configuration)
        {
            // https://stackoverflow.com/questions/42846296/how-to-load-appsetting-json-section-into-dictionary-in-net-core
            // https://github.com/dotnet/extensions/issues/782
            //var terminalSections = this.Configuration.GetSection("Terminals").GetChildren();

            RedisKeysMain = configuration.GetSection("SettingConstants").GetSection("Constants").GetChildren().ToList();
            A = RedisKeysMain.Select(x => x.Key).ToList();
            B = RedisKeysMain.Select(x => x.Value).ToList();

            Constants = configuration.GetSection("SettingConstants").GetSection("Constants").GetChildren().ToDictionary(x => x.Key, x => Convert.ToInt32(x.Value));
            RedisKeyPrefixes = configuration.GetSection("SettingConstants").GetSection("RedisKeyPrefixes").GetChildren().ToDictionary(x => x.Key, x => Convert.ToInt32(x.Value));
            RedisFields = configuration.GetSection("SettingConstants").GetSection("RedisFields").GetChildren().ToDictionary(x => x.Key, x => Convert.ToInt32(x.Value));

        }

        public Dictionary<string, int> Constants { get; }
        //public Dictionary<string, int> RedisKeysMain { get; }

        public List<IConfigurationSection> RedisKeysMain { get; set; }

        public static List<string> A { get; set; }

        public static List<string> B { get; set; }

        //public List<ConvertSectionToObject> RedisKeysMainConvert { get; set; }
        public Dictionary<string, int> RedisKeyPrefixes { get; }
        public Dictionary<string, int> RedisFields { get; }
    }
}
