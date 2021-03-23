using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using CachingFramework.Redis.Contracts;
using CachingFramework.Redis.Contracts.Providers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ConstantData.Services;
using Shared.Library.Models;
using Shared.Library.Services;

namespace ConstantData
{
    public class MonitorLoop
    {
        private readonly ILogger<MonitorLoop> _logger;
        private readonly IInitConstantsService _init;
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
            IHostApplicationLifetime applicationLifetime, IInitConstantsService init)
        {
            _logger = logger;
            _data = data;
            _constantService = constantService;
            _init = init;
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
            EventKeyNames eventKeysSet = _init.InitialiseEventKeyNames();

            _logger.LogInformation(10351, "1 ConstantCheck EventKeyFrontGivesTaskTimeDays = {0}.", eventKeysSet.EventKeyFrontGivesTaskTimeDays);

            (string startConstantKey, string startConstantField) = _data.FetchBaseConstants();
            _logger.LogInformation(10350, "ConstantData send constants {0} to SetStartConstants.", eventKeysSet, "constants");

            await _cache.SetStartConstants(eventKeysSet, startConstantKey, startConstantField);

            // можно загрузить константы обратно и проверить
            // а можно подписаться на ключ и следить, чтобы никто не лез в константы
            //EventKeyNames eventKeysSetCheck = await _data.FetchAllConstants();
            //_logger.LogInformation(10362, "2 ConstantCheck EventKeyFrontGivesTaskTimeDays = {0}.", eventKeysSetCheck.EventKeyFrontGivesTaskTimeDays);
            
            //_subscribe.SubscribeOnEventFrom(eventKeysSet);

            while (true)
            {
                if (_cancellationToken.IsCancellationRequested)
                {
                    bool res = await _cache.DeleteKeyIfCancelled(startConstantKey, startConstantField);
                    _logger.LogInformation(310310, "_cancellationToken was received, key was removed = {KeyStroke}.", res);
                    return;
                }
                
                var keyStroke = Console.ReadKey();

                if (keyStroke.Key == ConsoleKey.W)
                {
                    _logger.LogInformation(10370, "ConsoleKey was received {KeyStroke}.", keyStroke.Key);
                }

                await Task.Delay(10, _cancellationToken);
            }
        }
    }
}
