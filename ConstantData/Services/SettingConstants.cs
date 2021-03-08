﻿using System;
using Microsoft.Extensions.Configuration;

namespace ConstantData.Services
{
    public interface ISettingConstants
    {
        public int GetRecordActualityLevel { get; }
        public int GetTaskDelayTimeInSeconds { get; }
        public int GetBalanceOfTasksAndProcesses { get; }
        public int GetMaxProcessesCountOnServer { get; }
        public int GetMinBackProcessesServersCount { get; }
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
        public string GetPrefixRequest { get; }
        public string GetPrefixPackage { get; }
        public string GetPrefixTask { get; }
        public string GetPrefixBackServer { get; }
        public string GetPrefixProcessAdd { get; }
        public string GetPrefixProcessCancel { get; }
        public string GetPrefixProcessCount { get; }
        public string GetEventFieldBack { get; }
        public string GetEventFieldFront { get; }
        public string GetEventKeyBacksTasksProceed { get; }
    }

    public class SettingConstants : ISettingConstants
    {
        public SettingConstants(IConfiguration configuration)
        {
            Configuration = configuration;

            string recordActualityLevel = Configuration.GetSection("SettingConstants").GetSection("Constants").GetSection("RecordActualityLevel").Value;
            GetRecordActualityLevel = Convert.ToInt32(recordActualityLevel);
            GetTaskDelayTimeInSeconds = Convert.ToInt32(Configuration.GetSection("SettingConstants").GetSection("Constants").GetSection("TaskDelayTimeInSeconds").Value);
            GetBalanceOfTasksAndProcesses = Convert.ToInt32(Configuration.GetSection("SettingConstants").GetSection("Constants").GetSection("BalanceOfTasksAndProcesses").Value);
            GetMaxProcessesCountOnServer = Convert.ToInt32(Configuration.GetSection("SettingConstants").GetSection("Constants").GetSection("MaxProcessesCountOnServer").Value);
            GetMinBackProcessesServersCount = Convert.ToInt32(Configuration.GetSection("SettingConstants").GetSection("Constants").GetSection("MinBackProcessesServersCount").Value);

            GetEventKeyFromTimeDays = Convert.ToInt32(Configuration.GetSection("SettingConstants").GetSection("RedisKeysTimes").GetSection("eventKeyFromTimeDays").Value);
            GetEventKeyBackReadinessTimeDays = Convert.ToInt32(Configuration.GetSection("SettingConstants").GetSection("RedisKeysTimes").GetSection("eventKeyBackReadinessTimeDays").Value);
            GetEventKeyFrontGivesTaskTimeDays = Convert.ToInt32(Configuration.GetSection("SettingConstants").GetSection("RedisKeysTimes").GetSection("eventKeyFrontGivesTaskTimeDays").Value);//eventKeyFrontGivesTaskTimeDays
            GetEventKeyBackServerMainTimeDays = Convert.ToInt32(Configuration.GetSection("SettingConstants").GetSection("RedisKeysTimes").GetSection("eventKeyBackServerMainTimeDays").Value);
            GetEventKeyBackServerAuxiliaryTimeDays = Convert.ToInt32(Configuration.GetSection("SettingConstants").GetSection("RedisKeysTimes").GetSection("eventKeyBackServerAuxiliaryTimeDays").Value);
            GetPercentsKeysExistingTimeInMinutes = Convert.ToInt32(Configuration.GetSection("SettingConstants").GetSection("RedisKeysTimes").GetSection("PercentsKeysExistingTimeInMinutes").Value);


            GetEventKeyFrom = Configuration.GetSection("SettingConstants").GetSection("RedisKeys").GetSection("eventKeyFrom").Value;
            GetEventFieldFrom = Configuration.GetSection("SettingConstants").GetSection("RedisKeys").GetSection("eventFieldFrom").Value;
            GetEventKeyBackReadiness = Configuration.GetSection("SettingConstants").GetSection("RedisKeys").GetSection("eventKeyBackReadiness").Value;
            GetEventKeyFrontGivesTask = Configuration.GetSection("SettingConstants").GetSection("RedisKeys").GetSection("eventKeyFrontGivesTask").Value;

            GetPrefixRequest = Configuration.GetSection("SettingConstants").GetSection("RedisKeys").GetSection("prefixRequest").Value;
            GetPrefixPackage = Configuration.GetSection("SettingConstants").GetSection("RedisKeys").GetSection("prefixPackage").Value;
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

        public int GetTaskDelayTimeInSeconds { get; }

        public int GetBalanceOfTasksAndProcesses { get; }

        public int GetMaxProcessesCountOnServer { get; }

        public int GetMinBackProcessesServersCount { get; }

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

        public string GetPrefixRequest { get; }

        public string GetPrefixPackage { get; }

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