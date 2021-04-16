using System;
using System.Threading;
using System.Threading.Tasks;
using CachingFramework.Redis.Contracts.Providers;
using Microsoft.Extensions.Configuration;
using Shared.Library.Models;
using Shared.Library.Services;

namespace BackgroundTasksQueue.Services
{
    public interface ISettingConstants
    {
        public Task<EventKeyNames> ConstantInitializer(CancellationToken stoppingToken);
    }

    public class SettingConstantsService : ISettingConstants
    {
        private readonly ICacheProviderAsync _cache;
        private readonly ISharedDataAccess _data;
        private readonly string _guid;

        public SettingConstantsService(
            GenerateThisInstanceGuidService thisGuid,
            ISharedDataAccess data, 
            ICacheProviderAsync cache)
        {
            _data = data;
            _cache = cache;
            _guid = thisGuid.ThisBackServerGuid();
        }

        private static Serilog.ILogger Logs => Serilog.Log.ForContext<QueuedHostedService>();

        // разделить инициализацию констант внутри на два метода - проверка базового ключа и получение констант с текущего ключа
        // в первом проверить базовый ключ, если есть, взять из него текущий и сравнить его с предыдущим - если отличается, поставить признак необходимости обновления
        // второй, если есть необходимость, получает свежие константы
        // в универсальном обработчике вызываем инициализацию, а она сама разбирается с константами
        // в сервере констант составить таблицу обращения к константам - в поле имя и в значении новое значение
        // на ключ обновления констант подписка - в каком-то месте задержкой 1 сек, чтобы нельзя было слишком быстро менять
        // потом обычный обработчик-диспетчер, запись новых констант и генерация нового ключа для потребителей
        // оставить старые константы на поле legacy constants, а для ключа новых констант сделать новое поле

        public async Task<EventKeyNames> ConstantInitializer(CancellationToken stoppingToken)
        {
            EventKeyNames eventKeysSet = await _data.DeliveryOfUpdatedConstants(stoppingToken);

            if (eventKeysSet != null)
            {
                Logs.Here().Debug("EventKeyNames fetched constants in EventKeyNames - {@D}.", new { CycleDelay = eventKeysSet.TaskEmulatorDelayTimeInMilliseconds });
            }
            else
            {
                Logs.Here().Error("eventKeysSet CANNOT be Init.");
                return null;
            }

            string backServerGuid = _guid ?? throw new ArgumentNullException(nameof(_guid));
            eventKeysSet.BackServerGuid = backServerGuid;
            string backServerPrefixGuid = $"{eventKeysSet.PrefixBackServer}:{backServerGuid}";
            eventKeysSet.BackServerPrefixGuid = backServerPrefixGuid;
            string eventKeyBackReadiness = eventKeysSet.EventKeyBackReadiness;
            double eventKeyBackReadinessTimeDays = eventKeysSet.EventKeyBackReadinessTimeDays;

            // регистрируем сервер на общем ключе серверов
            await _cache.SetHashedAsync<string>(eventKeyBackReadiness, backServerPrefixGuid, backServerGuid, TimeSpan.FromDays(eventKeyBackReadinessTimeDays));
            
            string prefixProcessAdd = eventKeysSet.PrefixProcessAdd; // process:add
            string processAddPrefixGuid = $"{prefixProcessAdd}:{backServerGuid}"; // process:add:(this server guid)
            eventKeysSet.ProcessAddPrefixGuid = processAddPrefixGuid;

            string prefixProcessCancel = eventKeysSet.PrefixProcessCancel; // process:cancel
            string processCancelPrefixGuid = $"{prefixProcessCancel}:{backServerGuid}"; // process:cancel:(this server guid)
            eventKeysSet.ProcessCancelPrefixGuid = processCancelPrefixGuid;

            string prefixProcessCount = eventKeysSet.PrefixProcessCount; // process:count
            string processCountPrefixGuid = $"{prefixProcessCount}:{backServerGuid}"; // process:count:(this server guid)
            eventKeysSet.ProcessCountPrefixGuid = processCountPrefixGuid;

            //string processAddPrefixGuid = eventKeysSet.ProcessAddPrefixGuid;
            //string eventFieldBack = eventKeysSet.EventFieldBack;
            // инициализовать поле общего количества процессов при подписке - можно перенести в инициализацию, set "CurrentProcessesCount" in constants
            //await _cache.SetHashedAsync<int>(processAddPrefixGuid, eventFieldBack, 0, TimeSpan.FromDays(eventKeysSet.EventKeyBackReadinessTimeDays));

            Logs.Here().Information("Server Guid was fetched and stored into EventKeyNames. \n {@S}", new { ServerId = backServerPrefixGuid });
            return eventKeysSet;
        }

    }
}