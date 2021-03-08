using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BackgroundTasksQueue.Library.Models;
using BackgroundTasksQueue.Library.Services;
using CachingFramework.Redis.Contracts.Providers;
using Microsoft.Extensions.Logging;

namespace ConstantData.Services
{
    public interface ICacheManageService
    {
        public Task SetStartConstants(EventKeyNames eventKeysSet, string startConstantKey, string startConstantField);

    }

    public class CacheManageService : ICacheManageService
    {
        private readonly ILogger<CacheManageService> _logger;
        private readonly ICacheProviderAsync _cache;
        private readonly IKeyEventsProvider _keyEvents;

        public CacheManageService(
            ILogger<CacheManageService> logger,
            ICacheProviderAsync cache,
            IKeyEventsProvider keyEvents)
        {
            _logger = logger;
            _cache = cache;
            _keyEvents = keyEvents;
        }

        private readonly TimeSpan _startConstantKeyLifeTime = TimeSpan.FromDays(1);

        public async Task SetStartConstants(EventKeyNames eventKeysSet, string startConstantKey, string startConstantField)
        {
            // установить своё время для ключа, можно вместе с названием ключа
            await _cache.SetHashedAsync<EventKeyNames>(startConstantKey, startConstantField, eventKeysSet, _startConstantKeyLifeTime);

            _logger.LogInformation(55050, "SetStartConstants set constants (EventKeyFrom for example = {0}) in key {1}.", eventKeysSet.EventKeyFrom, "constants");
        }
    }
}
