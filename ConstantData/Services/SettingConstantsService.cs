using System;
using Microsoft.Extensions.Configuration;

namespace ConstantData.Services
{
    public interface ISettingConstantsService
    {
        public int GetRecordActualityLevel { get; }
        public int GetTaskEmulatorDelayTimeInMilliseconds { get; }
        public int GetBalanceOfTasksAndProcesses { get; }
        public int GetMaxProcessesCountOnServer { get; }
        public int GetMinBackProcessesServersCount { get; }
        public double GetEventKeyCommonKeyTimeDays { get; }
        public double GetEventKeyFromTimeDays { get; }
        public double GetEventKeyBackReadinessTimeDays { get; }
        public double GetEventKeyFrontGivesTaskTimeDays { get; }
        public double GetEventKeyBackServerMainTimeDays { get; }
        public double GetEventKeyBackServerAuxiliaryTimeDays { get; }
        public double GetPercentsKeysExistingTimeInMinutes { get; }
        public string GetEventKeyFrom { get; }
        public string GetEventFieldFrom { get; }
        public string GetEventKeyBackReadiness { get; }
        public string GetEventKeyFrontGivesTask { get; }
        public string GetEventKeyUpdateConstants { get; }
        public string GetPrefixRequest { get; }
        public string GetPrefixPackage { get; }
        public string GetPrefixPackageControl { get; }
        public string GetPrefixPackageCompleted { get; }
        public string GetPrefixTask { get; }
        public string GetPrefixBackServer { get; }
        public string GetPrefixProcessAdd { get; }
        public string GetPrefixProcessCancel { get; }
        public string GetPrefixProcessCount { get; }
        public string GetEventFieldBack { get; }
        public string GetEventFieldFront { get; }
        public string GetEventKeyBacksTasksProceed { get; }
        public int GetRandomRangeExtended { get; }
    }

    // сделать константы ключей в виде словаря - строка/время существования ключа
    // везде использовать имя ключа с типом словаря и только в последнем методе раскрывать и записывать

    public class SettingConstantsServiceService : ISettingConstantsService
    {
        public SettingConstantsServiceService(IConfiguration configuration)
        {
            Configuration = configuration;

            // https://stackoverflow.com/questions/15329601/how-to-get-all-the-values-from-appsettings-key-which-starts-with-specific-name-a/15329673
            //foreach (string key in ConfigurationManager.AppSettings)
            // https://weblog.west-wind.com/posts/2017/dec/12/easy-configuration-binding-in-aspnet-core-revisited
            // https://weblog.west-wind.com/posts/2016/may/23/strongly-typed-configuration-settings-in-aspnet-core

            // "Constants":
            string recordActualityLevel = Configuration.GetSection("SettingConstants").GetSection("Constants").GetSection("RecordActualityLevel").Value;
            GetRecordActualityLevel = Convert.ToInt32(recordActualityLevel);
            GetTaskEmulatorDelayTimeInMilliseconds = Convert.ToInt32(Configuration.GetSection("SettingConstants").GetSection("Constants").GetSection("TaskEmulatorDelayTimeInMilliseconds").Value);
            GetRandomRangeExtended = Convert.ToInt32(Configuration.GetSection("SettingConstants").GetSection("Constants").GetSection("RandomRangeExtended").Value);
            GetBalanceOfTasksAndProcesses = Convert.ToInt32(Configuration.GetSection("SettingConstants").GetSection("Constants").GetSection("BalanceOfTasksAndProcesses").Value);
            GetMaxProcessesCountOnServer = Convert.ToInt32(Configuration.GetSection("SettingConstants").GetSection("Constants").GetSection("MaxProcessesCountOnServer").Value);
            GetMinBackProcessesServersCount = Convert.ToInt32(Configuration.GetSection("SettingConstants").GetSection("Constants").GetSection("MinBackProcessesServersCount").Value);
            // "RedisKeysTimes":
            GetEventKeyCommonKeyTimeDays = Convert.ToInt32(Configuration.GetSection("SettingConstants").GetSection("RedisKeysTimes").GetSection("commonKeyTimeDays").Value);
            GetEventKeyFromTimeDays = Convert.ToInt32(Configuration.GetSection("SettingConstants").GetSection("RedisKeysTimes").GetSection("eventKeyFromTimeDays").Value);
            GetEventKeyBackReadinessTimeDays = Convert.ToInt32(Configuration.GetSection("SettingConstants").GetSection("RedisKeysTimes").GetSection("eventKeyBackReadinessTimeDays").Value);
            GetEventKeyFrontGivesTaskTimeDays = Convert.ToInt32(Configuration.GetSection("SettingConstants").GetSection("RedisKeysTimes").GetSection("eventKeyFrontGivesTaskTimeDays").Value);//eventKeyFrontGivesTaskTimeDays
            GetEventKeyBackServerMainTimeDays = Convert.ToInt32(Configuration.GetSection("SettingConstants").GetSection("RedisKeysTimes").GetSection("eventKeyBackServerMainTimeDays").Value);
            GetEventKeyBackServerAuxiliaryTimeDays = Convert.ToInt32(Configuration.GetSection("SettingConstants").GetSection("RedisKeysTimes").GetSection("eventKeyBackServerAuxiliaryTimeDays").Value);
            GetPercentsKeysExistingTimeInMinutes = Convert.ToInt32(Configuration.GetSection("SettingConstants").GetSection("RedisKeysTimes").GetSection("PercentsKeysExistingTimeInMinutes").Value);
            // "RedisKeys":
            GetEventKeyFrom = Configuration.GetSection("SettingConstants").GetSection("RedisKeys").GetSection("eventKeyFrom").Value;
            GetEventFieldFrom = Configuration.GetSection("SettingConstants").GetSection("RedisKeys").GetSection("eventFieldFrom").Value;
            GetEventKeyBackReadiness = Configuration.GetSection("SettingConstants").GetSection("RedisKeys").GetSection("eventKeyBackReadiness").Value;
            GetEventKeyFrontGivesTask = Configuration.GetSection("SettingConstants").GetSection("RedisKeys").GetSection("eventKeyFrontGivesTask").Value;
            GetEventKeyUpdateConstants = Configuration.GetSection("SettingConstants").GetSection("RedisKeys").GetSection("eventKeyUpdateConstants").Value;

            GetPrefixRequest = Configuration.GetSection("SettingConstants").GetSection("RedisKeys").GetSection("prefixRequest").Value;
            GetPrefixPackage = Configuration.GetSection("SettingConstants").GetSection("RedisKeys").GetSection("prefixPackage").Value;
            GetPrefixPackageControl = Configuration.GetSection("SettingConstants").GetSection("RedisKeys").GetSection("prefixPackageControl").Value;
            GetPrefixPackageCompleted = Configuration.GetSection("SettingConstants").GetSection("RedisKeys").GetSection("prefixPackageCompleted").Value;
            GetPrefixTask = Configuration.GetSection("SettingConstants").GetSection("RedisKeys").GetSection("prefixTask").Value;
            GetPrefixBackServer = Configuration.GetSection("SettingConstants").GetSection("RedisKeys").GetSection("prefixBackServer").Value;

            GetPrefixProcessAdd = Configuration.GetSection("SettingConstants").GetSection("RedisKeys").GetSection("prefixProcessAdd").Value;
            GetPrefixProcessCancel = Configuration.GetSection("SettingConstants").GetSection("RedisKeys").GetSection("prefixProcessCancel").Value;
            GetPrefixProcessCount = Configuration.GetSection("SettingConstants").GetSection("RedisKeys").GetSection("prefixProcessCount").Value;

            GetEventFieldBack = Configuration.GetSection("SettingConstants").GetSection("RedisKeys").GetSection("eventFieldBack").Value;
            GetEventFieldFront = Configuration.GetSection("SettingConstants").GetSection("RedisKeys").GetSection("eventFieldFront").Value;
            GetEventKeyBacksTasksProceed = Configuration.GetSection("SettingConstants").GetSection("RedisKeys").GetSection("eventKeyBacksTasksProceed").Value;

        }

        private IConfiguration Configuration { get; }
        public int GetRecordActualityLevel { get; }
        public int GetTaskEmulatorDelayTimeInMilliseconds { get; }
        public int GetRandomRangeExtended { get; }
        public int GetBalanceOfTasksAndProcesses { get; }
        public int GetMaxProcessesCountOnServer { get; }
        public int GetMinBackProcessesServersCount { get; }
        public double GetEventKeyCommonKeyTimeDays { get; }
        public double GetEventKeyFromTimeDays { get; }
        public double GetEventKeyBackReadinessTimeDays { get; }
        public double GetEventKeyFrontGivesTaskTimeDays { get; }
        public double GetEventKeyBackServerMainTimeDays { get; }
        public double GetEventKeyBackServerAuxiliaryTimeDays { get; }
        public double GetPercentsKeysExistingTimeInMinutes { get; }
        public string GetEventKeyFrom { get; }
        public string GetEventFieldFrom { get; }
        public string GetEventKeyBackReadiness { get; }
        public string GetEventKeyFrontGivesTask { get; }
        public string GetEventKeyUpdateConstants { get; }
        public string GetPrefixRequest { get; }
        public string GetPrefixPackage { get; }
        public string GetPrefixPackageControl { get; }
        public string GetPrefixPackageCompleted { get; }
        public string GetPrefixTask { get; }
        public string GetPrefixBackServer { get; }
        public string GetPrefixProcessAdd { get; }
        public string GetPrefixProcessCancel { get; }
        public string GetPrefixProcessCount { get; }
        public string GetEventFieldBack { get; }
        public string GetEventFieldFront { get; }
        public string GetEventKeyBacksTasksProceed { get; }
    }
}