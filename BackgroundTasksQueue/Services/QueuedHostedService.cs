using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CachingFramework.Redis.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CachingFramework.Redis.Contracts.Providers;
using Shared.Library.Services;
using BackgroundTasksQueue.Models;
using Shared.Library.Models;

namespace BackgroundTasksQueue.Services
{
    public class QueuedHostedService : BackgroundService
    {
        //private readonly ILogger<QueuedHostedService> _logger;
        
        private readonly ICacheProviderAsync _cache;
        private readonly IKeyEventsProvider _keyEvents;
        private readonly ISettingConstants _constants;
        private readonly IOnKeysEventsSubscribeService _subscribe;
        private readonly string _guid;

        public QueuedHostedService(
            GenerateThisInstanceGuidService thisGuid,
            //IBackgroundTaskQueue taskQueue,
            //ILogger<QueuedHostedService> logger,
            //ISharedDataAccess data,
            ICacheProviderAsync cache,
            IKeyEventsProvider keyEvents, 
            IOnKeysEventsSubscribeService subscribe, 
            ISettingConstants constants)
        {
            //TaskQueue = taskQueue;
            //_logger = logger;
            //_data = data;
            _cache = cache;
            _keyEvents = keyEvents;
            _subscribe = subscribe;
            _constants = constants;

            _guid = thisGuid.ThisBackServerGuid();
        }

        private static Serilog.ILogger Logs => Serilog.Log.ForContext<QueuedHostedService>();

        //public IBackgroundTaskQueue TaskQueue { get; }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Logs.Here().Information("Queued Hosted Service is running. BackgroundProcessing will be called now.");
            await BackgroundProcessing(stoppingToken);
        }

        private async Task BackgroundProcessing(CancellationToken stoppingToken)
        {
            // все проверки и ожидание внутри метода, без констант не вернётся
            // но можно проверять на null, если как-то null, то что-то сделать (shutdown)
            EventKeyNames eventKeysSet = await _constants.ConstantInitializer(stoppingToken);
            // все названия ключей из префиксов перенести в инициализацию

            _ = RunSubscribe(eventKeysSet, stoppingToken);

            
           
        }

        private async Task RunSubscribe(EventKeyNames eventKeysSet, CancellationToken stoppingToken)
        {
            // это зачем здесь?
            await _cache.SetHashedAsync<string>(eventKeysSet.EventKeyBackReadiness, eventKeysSet.BackServerPrefixGuid, eventKeysSet.BackServerGuid, TimeSpan.FromDays(eventKeysSet.EventKeyBackReadinessTimeDays));

            // подписываемся на ключ сообщения о появлении свободных задач
            _subscribe.SubscribeOnEventRun(eventKeysSet, stoppingToken);
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            Logs.Here().Information("Queued Hosted Service is stopping.");
            await base.StopAsync(stoppingToken);
        }
    }
}

