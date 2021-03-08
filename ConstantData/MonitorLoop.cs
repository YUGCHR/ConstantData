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
        private readonly ICacheManageService _cache;
        private readonly ISettingConstantsService _constantService;
        private readonly CancellationToken _cancellationToken;
        private readonly string _guid;

        public MonitorLoop(
            ILogger<MonitorLoop> logger,
            ISharedDataAccess data,
            ICacheManageService cache,
            ISettingConstantsService constantService,
            IHostApplicationLifetime applicationLifetime)
        {
            _logger = logger;
            _data = data;
            _constantService = constantService;
            _cache = cache;
            _cancellationToken = applicationLifetime.ApplicationStopping;
        }

        private const string CheckToken = "tt-tt-tt";

        public void StartMonitorLoop()
        {
            _logger.LogInformation("ConstantsMountingMonitor Loop is starting.");

            // Run a console user input loop in a background thread
            Task.Run(ConstantsMountingMonitor, _cancellationToken);
        }

        public async Task ConstantsMountingMonitor()
        {
            EventKeyNames eventKeysSet = InitialiseEventKeyNames();

            _logger.LogInformation(10351, "1 ConstantCheck EventKeyFrontGivesTaskTimeDays = {0}.", eventKeysSet.EventKeyFrontGivesTaskTimeDays);

            (string startConstantKey, string startConstantField) = _data.FetchBaseConstants();
            _logger.LogInformation(10350, "ConstantData send constants {0} to SetStartConstants.", eventKeysSet, "constants");

            await _cache.SetStartConstants(eventKeysSet, startConstantKey, startConstantField);



            // можно загрузить константы обратно и проверить
            // а можно подписаться на ключ и следить, чтобы никто не лез в константы
            EventKeyNames eventKeysSetCheck = await _data.FetchAllConstants();
            //_logger.LogInformation(10360, "2 ConstantCheck EventKeyFromTimeDays = {0}.", eventKeysSetCheck.EventKeyFromTimeDays);
            //_logger.LogInformation(10361, "2 ConstantCheck EventKeyBackReadinessTimeDays = {0}.", eventKeysSetCheck.EventKeyBackReadinessTimeDays);
            _logger.LogInformation(10362, "2 ConstantCheck EventKeyFrontGivesTaskTimeDays = {0}.", eventKeysSetCheck.EventKeyFrontGivesTaskTimeDays);
            //_logger.LogInformation(10363, "2 ConstantCheck EventKeyBackServerMainTimeDays = {0}.", eventKeysSetCheck.EventKeyBackServerMainTimeDays);
            //_logger.LogInformation(10364, "2 ConstantCheck EventKeyBackServerAuxiliaryTimeDays = {0}.", eventKeysSetCheck.EventKeyBackServerAuxiliaryTimeDays);

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
                TaskDelayTimeInSeconds = _constantService.GetTaskDelayTimeInSeconds, // время задержки в секундах для эмулятора счета задачи
                BalanceOfTasksAndProcesses = _constantService.GetBalanceOfTasksAndProcesses, // соотношение количества задач и процессов для их выполнения на back-processes-servers (количества задач разделить на это число и сделать столько процессов)
                MaxProcessesCountOnServer = _constantService.GetMaxProcessesCountOnServer, // максимальное количество процессов на back-processes-servers (минимальное - 1)
                EventKeyFrom = _constantService.GetEventKeyFrom, // "subscribeOnFrom" - ключ для подписки на команду запуска эмулятора сервера
                EventFieldFrom = _constantService.GetEventFieldFrom, // "count" - поле для подписки на команду запуска эмулятора сервера
                EventCmd = KeyEvent.HashSet,
                EventKeyBackReadiness = _constantService.GetEventKeyBackReadiness, // ключ регистрации серверов
                EventFieldBack = _constantService.GetEventFieldBack,
                EventKeyFrontGivesTask = _constantService.GetEventKeyFrontGivesTask, // кафе выдачи задач
                PrefixRequest = _constantService.GetPrefixRequest, // request:guid
                PrefixPackage = _constantService.GetPrefixPackage, // package:guid
                PrefixTask = _constantService.GetPrefixTask, // task:guid
                PrefixBackServer = _constantService.GetPrefixBackServer, // backserver:guid
                BackServerGuid = _guid, // !!! this server guid
                BackServerPrefixGuid = $"{_constantService.GetPrefixBackServer}:{_guid}", // !!! backserver:(this server guid)
                PrefixProcessAdd = _constantService.GetPrefixProcessAdd, // process:add
                PrefixProcessCancel = _constantService.GetPrefixProcessCancel, // process:cancel
                PrefixProcessCount = _constantService.GetPrefixProcessCount, // process:count
                EventFieldFront = _constantService.GetEventFieldFront,
                EventKeyBacksTasksProceed = _constantService.GetEventKeyBacksTasksProceed, //  ключ выполняемых/выполненных задач                
                EventKeyFromTimeDays = _constantService.GetEventKeyFromTimeDays, // срок хранения ключа eventKeyFrom
                EventKeyBackReadinessTimeDays = _constantService.GetEventKeyBackReadinessTimeDays, // срок хранения 
                EventKeyFrontGivesTaskTimeDays = _constantService.GetEventKeyFrontGivesTaskTimeDays, // срок хранения ключа 
                EventKeyBackServerMainTimeDays = _constantService.GetEventKeyBackServerMainTimeDays, // срок хранения ключа 
                EventKeyBackServerAuxiliaryTimeDays = _constantService.GetEventKeyBackServerAuxiliaryTimeDays, // срок хранения ключа 
            };
        }
    }
}

