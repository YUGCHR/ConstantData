using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CachingFramework.Redis.Contracts;
using CachingFramework.Redis.Contracts.Providers;
using Shared.Library.Models;

namespace ConstantData.Services
{
    public interface IOnKeysEventsSubscribeService
    {
        public void SubscribeOnEventUpdate(ConstantsSet constantsSet, string constantsStartGuidField, CancellationToken stoppingToken);
    }

    public class OnKeysEventsSubscribeService : IOnKeysEventsSubscribeService
    {
        private readonly ICacheManageService _cache;
        private readonly IKeyEventsProvider _keyEvents;

        public OnKeysEventsSubscribeService(
            IKeyEventsProvider keyEvents, ICacheManageService cache)
        {
            _keyEvents = keyEvents;
            _cache = cache;
        }

        private static Serilog.ILogger Logs => Serilog.Log.ForContext<OnKeysEventsSubscribeService>();

        private bool _flagToBlockEventUpdate;

        // подписываемся на ключ сообщения о появлении обновления констант
        public void SubscribeOnEventUpdate(ConstantsSet constantsSet, string constantsStartGuidField, CancellationToken stoppingToken)
        {
            string eventKeyUpdateConstants = constantsSet.EventKeyUpdateConstants.Value;

            Logs.Here().Information("ConstantsData subscribed on EventKey. \n {@E}", new { EventKey = eventKeyUpdateConstants });
            Logs.Here().Information("Constants version is {0}:{1}.", constantsSet.ConstantsVersionBase.Value, constantsSet.ConstantsVersionNumber.Value);

            _flagToBlockEventUpdate = true;

            _keyEvents.Subscribe(eventKeyUpdateConstants, async (string key, KeyEvent cmd) =>
            {
                if (cmd == constantsSet.EventCmd && _flagToBlockEventUpdate)
                {
                    _flagToBlockEventUpdate = false;
                    _ = CheckKeyUpdateConstants(constantsSet, constantsStartGuidField, stoppingToken);
                }
            });
        }

        private async Task CheckKeyUpdateConstants(ConstantsSet constantsSet, string constantsStartGuidField, CancellationToken stoppingToken) // Main of EventKeyFrontGivesTask key
        {
            // проверять, что константы может обновлять только админ

            string eventKeyUpdateConstants = constantsSet.EventKeyUpdateConstants.Value;
            Logs.Here().Information("CheckKeyUpdateConstants started with key {0}.", eventKeyUpdateConstants);

            IDictionary<string, int> updatedConstants = await _cache.FetchUpdatedConstants<string, int>(eventKeyUpdateConstants); ;
            int updatedConstantsCount = updatedConstants.Count;
            Logs.Here().Information("Fetched updated constants count = {0}.", updatedConstantsCount);

            // выбирать все поля, присваивать по таблице, при присваивании поле удалять
            // все обновляемые константы должны быть одного типа или разные типы на разных ключах
            foreach (KeyValuePair<string, int> updatedConstant in updatedConstants)
            {
                var (key, value) = updatedConstant;
                constantsSet = UpdatedValueAssignsToProperty(constantsSet, key, value) ?? constantsSet;
                // можно добавить сообщение, что модифицировать константу не удалось
                // но можно внутри свича сообщить
            }

            // версия констант обновится внутри SetStartConstants
            await _cache.SetStartConstants(constantsSet.ConstantsVersionBase, constantsStartGuidField, constantsSet);

            // задержка, определяющая максимальную частоту обновления констант
            double timeToWaitTheConstants = constantsSet.EventKeyUpdateConstants.LifeTime;
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(timeToWaitTheConstants), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Prevent throwing if the Delay is cancelled
            }
            // перед завершением обработчика разрешаем события подписки на обновления
            _flagToBlockEventUpdate = true;
        }

        public ConstantsSet UpdatedValueAssignsToProperty(ConstantsSet constantsSet, string key, int value)
        {
            switch (key)
            {
                case "RecordActualityLevel":
                    constantsSet.RecordActualityLevel.Value = value;
                    Logs.Here().Information("Key = {0}, RecordActualityLevel was updated with value = {1}.", key, value);
                    return constantsSet;
                case "TaskEmulatorDelayTimeInMilliseconds":
                    constantsSet.TaskEmulatorDelayTimeInMilliseconds.Value = value;
                    Logs.Here().Information("Key = {0}, TaskEmulatorDelayTimeInMilliseconds was updated with value = {1}.", key, value);
                    return constantsSet;
                case "RandomRangeExtended":
                    constantsSet.RandomRangeExtended.Value = value;
                    Logs.Here().Information("Key = {0}, RandomRangeExtended was updated with value = {1}.", key, value);
                    return constantsSet;
                case "BalanceOfTasksAndProcesses":
                    constantsSet.BalanceOfTasksAndProcesses.Value = value;
                    Logs.Here().Information("Key = {0}, BalanceOfTasksAndProcesses was updated with value = {1}.", key, value);
                    return constantsSet;
                case "MaxProcessesCountOnServer":
                    constantsSet.MaxProcessesCountOnServer.Value = value;
                    Logs.Here().Information("Key = {0}, MaxProcessesCountOnServer was updated with value = {1}.", key, value);
                    return constantsSet;
                case "MinBackProcessesServersCount":
                    constantsSet.MinBackProcessesServersCount.Value = value; //Convert.ToInt32(value);
                    Logs.Here().Information("Key = {0}, MinBackProcessesServersCount was updated with value = {1}.", key, value);
                    return constantsSet;
            }
            // можно добавить сообщение, что модифицировать константу не удалось
            return null;
        }
    }
}
