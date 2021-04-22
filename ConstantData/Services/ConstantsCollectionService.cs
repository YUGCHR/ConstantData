using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Library.Models;

namespace ConstantData.Services
{
    public interface IConstantsCollectionService
    {
        //public Dictionary<string, int> Constants { get; }
        //public Dictionary<string, int> RedisKeysMain { get; }
        //public List<(string a, int b)> RedisKeysMain { get; }
        //public List<IConfigurationSection> RedisKeysMain { get; }
        public ConstantsLikeInJson SettingConstants { get; set; }

        //public Dictionary<string, int> RedisKeyPrefixes { get; }
        //public Dictionary<string, int> RedisFields { get; }
    }

    public class ConstantsCollectionService : IConstantsCollectionService
    {
        public ConstantsCollectionService(IConfiguration configuration)
        {
            Configuration = configuration;
            //IConfigurationRoot configurationRoot = configuration.Build();

            SettingConstants = new ConstantsLikeInJson();

            Configuration.GetSection("SettingConstants").Bind(SettingConstants);
            // https://stackoverflow.com/questions/42846296/how-to-load-appsetting-json-section-into-dictionary-in-net-core
            // https://github.com/dotnet/extensions/issues/782
            //var terminalSections = this.Configuration.GetSection("Terminals").GetChildren();

            //RedisKeysMain = configuration.GetSection("SettingConstants").GetSection("Constants").GetChildren().ToList();
            //A = RedisKeysMain.Select(x => x.Key).ToList();
            //B = RedisKeysMain.Select(x => x.Value).ToList();

            //List<(string, string)> list = Configuration.GetSection("SettingConstants").GetSection("Constant").GetChildren().ToList().Select(x => (x.Key, x.Value)).ToList();
            
            //int count = list.Count;
            ////ABL = list
            ////
            //for (int i = 0; i < count; i++)
            //{
            //    Logs.Here().Information("List  = {0}.", list[i]);
            //}


            //configuration.GetSection("SettingConstants").Bind(List1);

            //Constants = configuration.GetSection("SettingConstants").GetSection("Constants").GetChildren().ToDictionary(x => x.Key, x => Convert.ToInt32(x.Value));
            //RedisKeyPrefixes = configuration.GetSection("SettingConstants").GetSection("RedisKeyPrefixes").GetChildren().ToDictionary(x => x.Key, x => Convert.ToInt32(x.Value));
            //RedisFields = configuration.GetSection("SettingConstants").GetSection("RedisFields").GetChildren().ToDictionary(x => x.Key, x => Convert.ToInt32(x.Value));

        }

        private static Serilog.ILogger Logs => Serilog.Log.ForContext<ConstantsCollectionService>();

        private IConfiguration Configuration { get; }

        //public Dictionary<string, int> Constants { get; }
        //public Dictionary<string, int> RedisKeysMain { get; }

        //public List<IConfigurationSection> RedisKeysMain { get; set; }


        public ConstantsLikeInJson SettingConstants { get; set; }

        //public static List<AB> ABL { get; set; }


        //public List<ConvertSectionToObject> RedisKeysMainConvert { get; set; }
        //public Dictionary<string, int> RedisKeyPrefixes { get; }
        //public Dictionary<string, int> RedisFields { get; }
    }

    //public static class AB
    //{
    //    public static string A { get; set; }
    //    public static string B { get; set; }
    //}
}
