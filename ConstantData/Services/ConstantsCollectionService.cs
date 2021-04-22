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
        public IsomorphicJsonConstantsStructure SettingConstants { get; set; }        
    }

    public class ConstantsCollectionService : IConstantsCollectionService
    {
        public ConstantsCollectionService(IConfiguration configuration)
        {
            Configuration = configuration;

            SettingConstants = new IsomorphicJsonConstantsStructure();

            Configuration.GetSection("SettingConstants").Bind(SettingConstants);
        }

        private static Serilog.ILogger Logs => Serilog.Log.ForContext<ConstantsCollectionService>();

        private IConfiguration Configuration { get; }
        
        public IsomorphicJsonConstantsStructure SettingConstants { get; set; }
    }
}
