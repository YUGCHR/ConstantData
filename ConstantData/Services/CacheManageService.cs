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
        public Task SetStartConstants(KeyType startConstantKey, string startConstantField, ConstantsSet constantsSet);
        public Task SetConstantsStartGuidKey(KeyType startConstantKey, string startConstantField, string constantsStartGuidKey);
        public Task<TV> FetchUpdatedConstant<TK, TV>(string key, TK field);
        public Task<bool> DeleteKeyIfCancelled(string startConstantKey);
    }

    public class CacheManageService : ICacheManageService
    {
        private readonly ICacheProviderAsync _cache;

        public CacheManageService(ICacheProviderAsync cache)
        {
            _cache = cache;
        }

        private static Serilog.ILogger Logs => Serilog.Log.ForContext<CacheManageService>();

        public async Task SetStartConstants(KeyType keyTime, string field, ConstantsSet constantsSet)
        {
            if (field == constantsSet.ConstantsVersionBaseField.Value)
            {
                // обновлять версию констант при записи в ключ гуид
                constantsSet.ConstantsVersionNumber.Value++;
                Logs.Here().Information("ConstantsVersionNumber was incremented and become {0}.", constantsSet.ConstantsVersionNumber.Value);
            }

            await _cache.SetHashedAsync<ConstantsSet>(keyTime.Value, field, constantsSet, SetLifeTimeFromKey(keyTime));
            Logs.Here().Information("SetStartConstants set constants (EventKeyFrom for example = {0}) in key {1}.", constantsSet.EventKeyFrom.Value, keyTime.Value);
        }

        public async Task SetConstantsStartGuidKey(KeyType keyTime, string field, string constantsStartGuidKey)
        {
            await _cache.SetHashedAsync<string>(keyTime.Value, field, constantsStartGuidKey, SetLifeTimeFromKey(keyTime));
            Logs.Here().Information("SetStartConstants set {@G}.", new { GuidKey = keyTime.Value });
        }

        private TimeSpan SetLifeTimeFromKey(KeyType time)
        {
            return TimeSpan.FromDays(time.LifeTime);
        }

        public async Task<TV> FetchUpdatedConstant<TK, TV>(string key, TK field)
        {
            return await _cache.GetHashedAsync<TK, TV>(key, field);
        }

        public async Task<bool> DeleteKeyIfCancelled(string startConstantKey)
        {
            return await _cache.RemoveAsync(startConstantKey);
        }
    }
}
