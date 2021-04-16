using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Shared.Library.Models;
using Shared.Library.Services;
using CachingFramework.Redis.Contracts.Providers;
using Microsoft.Extensions.Logging;

namespace ConstantData.Services
{
    public interface ICacheManageService
    {
        public Task SetStartConstants(string startConstantKey, string startConstantField, EventKeyNames eventKeysSet);
        public Task SetConstantsStartGuidKey(string startConstantKey, string startConstantField, string constantsStartGuidKey);
        public Task<bool> DeleteKeyIfCancelled(string startConstantKey);
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

        public async Task SetStartConstants(string startConstantKey, string startConstantField, EventKeyNames eventKeysSet)
        {
            // установить своё время для ключа, можно вместе с названием ключа
            await _cache.SetHashedAsync<EventKeyNames>(startConstantKey, startConstantField, eventKeysSet, _startConstantKeyLifeTime);

            _logger.LogInformation(55050, "SetStartConstants set constants (EventKeyFrom for example = {0}) in key {1}.", eventKeysSet.EventKeyFrom, "constants");
        }
        
        public async Task SetConstantsStartGuidKey(string startConstantKey, string startConstantField, string constantsStartGuidKey)
        {
            // установить своё время для ключа, можно вместе с названием ключа
            await _cache.SetHashedAsync<string>(startConstantKey, startConstantField, constantsStartGuidKey, _startConstantKeyLifeTime);

            _logger.LogInformation(55050, "SetStartConstants set constants Guid key {0}.", constantsStartGuidKey);
        }

        public async Task<bool> DeleteKeyIfCancelled(string startConstantKey)
        {
            return await _cache.RemoveAsync(startConstantKey);
        }
    }
}
