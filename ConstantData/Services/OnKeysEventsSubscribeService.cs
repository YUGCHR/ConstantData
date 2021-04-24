using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        // подписываемся на ключ сообщения о появлении свободных задач
        public void SubscribeOnEventUpdate(ConstantsSet constantsSet, string constantsStartGuidField, CancellationToken stoppingToken)
        {
            string eventKeyUpdateConstants = constantsSet.EventKeyUpdateConstants.Value;

            Logs.Here().Information("ConstantsData subscribed on EventKey. \n {@E}", new {EventKey = eventKeyUpdateConstants});
            Logs.Here().Information("Constants version is {0}:{1}.", constantsSet.ConstantsVersionBase.Value, constantsSet.ConstantsVersionNumber.Value);

            _flagToBlockEventUpdate = true;

            _keyEvents.Subscribe(eventKeyUpdateConstants, (string key, KeyEvent cmd) =>
            {
                if (cmd == constantsSet.EventCmd && _flagToBlockEventUpdate)
                {
                    _flagToBlockEventUpdate = false;
                    //Logs.Here().Debug("CheckKeyUpdateConstants will be called No:{0}, Event permit = {Flag} \n {@K} with {@C} was received. \n", _callingNumOfCheckKeyFrontGivesTask, _flagToBlockEventRun, new { Key = eventKeyFrontGivesTask }, new { Command = cmd });
                    _ = CheckKeyUpdateConstants(constantsSet, constantsStartGuidField, stoppingToken);
                }
            });
        }

        private async Task CheckKeyUpdateConstants(ConstantsSet constantsSet, string constantsStartGuidField, CancellationToken stoppingToken) // Main of EventKeyFrontGivesTask key
        {
            string eventKeyUpdateConstants = constantsSet.EventKeyUpdateConstants.Value;

            int updatedConstant01 = await _cache.FetchUpdatedConstant<int, int>(eventKeyUpdateConstants, 1);
            constantsSet.TaskEmulatorDelayTimeInMilliseconds.LifeTime = updatedConstant01;
            Logs.Here().Information("Constant update fetched and set = {0}.", updatedConstant01);

            string startConstantKey = constantsSet.ConstantsVersionBase.Value;
            Logs.Here().Information("Updated constant set on key {0}.", startConstantKey);

            await _cache.SetStartConstants(startConstantKey, constantsStartGuidField, constantsSet);

            double timeToWaitTheConstants = 1;
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(timeToWaitTheConstants), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Prevent throwing if the Delay is cancelled
            }

            _flagToBlockEventUpdate = true;
        }
    }
}
