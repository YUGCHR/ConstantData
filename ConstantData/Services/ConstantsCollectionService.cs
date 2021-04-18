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
        public Dictionary<string, string> MailSettings { get; }
    }

    public class ConstantsCollectionService : IConstantsCollectionService
    {
        public ConstantsCollectionService(IConfiguration configuration)
        {
            

            MailSettings = configuration.GetSection("MailSettings").GetChildren()
                .ToDictionary(x => x.Key, x => x.Value);

        }

        public Dictionary<string, string> MailSettings { get; private set; }
    }
}
