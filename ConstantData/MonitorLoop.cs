using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using CachingFramework.Redis.Contracts;
using CachingFramework.Redis.Contracts.Providers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ConstantData.Services;
using BackgroundTasksQueue.Library.Models;
using BackgroundTasksQueue.Library.Services;

namespace ConstantData
{
    public class MonitorLoop
    {
        private readonly ILogger<MonitorLoop> _logger;
        private readonly ISharedDataAccess _data;
        private readonly ISettingConstants _constant;
        private readonly CancellationToken _cancellationToken;        
        private readonly string _guid;

        public MonitorLoop(            
            ILogger<MonitorLoop> logger,
            ISharedDataAccess data,
            ISettingConstants constant,
            IHostApplicationLifetime applicationLifetime)
        {
            _logger = logger;
            _data = data;
            _constant = constant;
            _cancellationToken = applicationLifetime.ApplicationStopping;            
        }

        private const string CheckToken = "tt-tt-tt";

        public void StartMonitorLoop()
        {
            _logger.LogInformation("Monitor Loop is starting.");

            // Run a console user input loop in a background thread
            Task.Run(Monitor, _cancellationToken);
        }

        public async Task Monitor()
        {
            EventKeyNames eventKeysSet = InitialiseEventKeyNames();

            _logger.LogInformation(10350, "ConstantData send constants {0} to SetStartConstants.", eventKeysSet, "constants");
            _logger.LogInformation(10351, "1 ConstantCheck EventKeyFrontGivesTaskTimeDays = {0}.", eventKeysSet.EventKeyFrontGivesTaskTimeDays);

            await _data.SetStartConstants(eventKeysSet, CheckToken);

            // можно загрузить константы обратно и проверить
            // а можно подписаться на ключ и следить, чтобы никто не лез в константы
            EventKeyNames eventKeysSetCheck = await _data.FetchAllConstants();
            _logger.LogInformation(10360, "2 ConstantCheck EventKeyFromTimeDays = {0}.", eventKeysSetCheck.EventKeyFromTimeDays);
            _logger.LogInformation(10361, "2 ConstantCheck EventKeyBackReadinessTimeDays = {0}.", eventKeysSetCheck.EventKeyBackReadinessTimeDays);
            _logger.LogInformation(10362, "2 ConstantCheck EventKeyFrontGivesTaskTimeDays = {0}.", eventKeysSetCheck.EventKeyFrontGivesTaskTimeDays);
            _logger.LogInformation(10363, "2 ConstantCheck EventKeyBackServerMainTimeDays = {0}.", eventKeysSetCheck.EventKeyBackServerMainTimeDays);
            _logger.LogInformation(10364, "2 ConstantCheck EventKeyBackServerAuxiliaryTimeDays = {0}.", eventKeysSetCheck.EventKeyBackServerAuxiliaryTimeDays);

            //_subscribe.SubscribeOnEventFrom(eventKeysSet);

            while (IsCancellationNotYet())
            {
                var keyStroke = Console.ReadKey();

                if (keyStroke.Key == ConsoleKey.W)
                {
                    _logger.LogInformation(10370, "ConsoleKey was received {KeyStroke}.", keyStroke.Key);
                }
            }
        }

        private bool IsCancellationNotYet()
        {
            return !_cancellationToken.IsCancellationRequested;
        }

        private EventKeyNames InitialiseEventKeyNames()
        {
            return new EventKeyNames
            {
                TaskDelayTimeInSeconds = _constant.GetTaskDelayTimeInSeconds, // время задержки в секундах для эмулятора счета задачи
                BalanceOfTasksAndProcesses = _constant.GetBalanceOfTasksAndProcesses, // соотношение количества задач и процессов для их выполнения на back-processes-servers (количества задач разделить на это число и сделать столько процессов)
                MaxProcessesCountOnServer = _constant.GetMaxProcessesCountOnServer, // максимальное количество процессов на back-processes-servers (минимальное - 1)
                EventKeyFrom = _constant.GetEventKeyFrom, // "subscribeOnFrom" - ключ для подписки на команду запуска эмулятора сервера
                EventFieldFrom = _constant.GetEventFieldFrom, // "count" - поле для подписки на команду запуска эмулятора сервера
                EventCmd = KeyEvent.HashSet,
                EventKeyBackReadiness = _constant.GetEventKeyBackReadiness, // ключ регистрации серверов
                EventFieldBack = _constant.GetEventFieldBack,
                EventKeyFrontGivesTask = _constant.GetEventKeyFrontGivesTask, // кафе выдачи задач
                PrefixRequest = _constant.GetPrefixRequest, // request:guid
                PrefixPackage = _constant.GetPrefixPackage, // package:guid
                PrefixTask = _constant.GetPrefixTask, // task:guid
                PrefixBackServer = _constant.GetPrefixBackServer, // backserver:guid
                BackServerGuid = _guid, // !!! this server guid
                BackServerPrefixGuid = $"{_constant.GetPrefixBackServer}:{_guid}", // !!! backserver:(this server guid)
                PrefixProcessAdd = _constant.GetPrefixProcessAdd, // process:add
                PrefixProcessCancel = _constant.GetPrefixProcessCancel, // process:cancel
                PrefixProcessCount = _constant.GetPrefixProcessCount, // process:count
                EventFieldFront = _constant.GetEventFieldFront,
                EventKeyBacksTasksProceed = _constant.GetEventKeyBacksTasksProceed, //  ключ выполняемых/выполненных задач                
                EventKeyFromTimeDays = _constant.GetEventKeyFromTimeDays, // срок хранения ключа eventKeyFrom
                EventKeyBackReadinessTimeDays = _constant.GetEventKeyBackReadinessTimeDays, // срок хранения 
                EventKeyFrontGivesTaskTimeDays = _constant.GetEventKeyFrontGivesTaskTimeDays, // срок хранения ключа 
                EventKeyBackServerMainTimeDays = _constant.GetEventKeyBackServerMainTimeDays, // срок хранения ключа 
                EventKeyBackServerAuxiliaryTimeDays = _constant.GetEventKeyBackServerAuxiliaryTimeDays, // срок хранения ключа 
            };
        }
    }
}

