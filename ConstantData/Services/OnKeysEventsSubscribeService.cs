using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
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
                constantsSet = UpdatedValueAssignsToProperty(constantsSet, key, value);// ?? constantsSet;
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

        private static object FetchValueOfProperty(object rrr, string propertyName)
        {
            return rrr.GetType().GetProperty(propertyName)?.GetValue(rrr);
        }

        public static ConstantsSet UpdatedValueAssignsToProperty(ConstantsSet constantsSet, string key, int value)
        {
            Logs.Here().Information("constantsSet.{0} will be updated with value = {1}.", key, value);

            //ConstantType classValue = new()
            //{
            //    Value = value
            //};

            object second = constantsSet.GetType().GetProperty(key)?.GetValue(constantsSet);
            second?.GetType().GetProperty("Value")?.SetValue(second, value);

            //var class1Type = typeof(ConstantsSet);
            //var classTypeProperty = class1Type.GetProperty(key);
            //if (classTypeProperty == null)
            //{
            //    return constantsSet;
            //}
            //classTypeProperty.SetValue(constantsSet, classValue);

            //typeof(ConstantsSet).GetProperty(key)?.SetValue(constantsSet, classValue);

            //var t = constantsSet.GetType();
            //var tt = t.GetProperty(key);
            //object? ttt = constantsSet.GetType().GetProperty(key, typeof(ConstantType))?.GetValue(constantsSet, null);
            //var ttt = FetchValueOfProperty(constantsSet, key);
            //if (ttt is ConstantType lll)
            //{
            //    Logs.Here().Information("Value: {value}", lll.Value);
            //}

            //var r = ttt.GetType();
            //var rr = r.GetProperty("Value");
            //var rrr = ttt.GetType().GetProperty("Value").GetValue(ttt, null);
            var rrr = FetchValueOfProperty(FetchValueOfProperty(constantsSet, key), "Value");

            // car.GetType().GetProperty(propertyName).GetValue(car, null);

            Logs.Here().Information("ttt {0} will.", rrr.ToString());

            // работающий вариант с Convert.ChangeType
            //PropertyInfo propertyInfo = constantsSet.GetType().GetProperty(key);
            //propertyInfo.SetValue(constantsSet, Convert.ChangeType(classValue, propertyInfo.PropertyType), null);

            //var newClassValue = propertyInfo.PropertyType.GetType();
            //

            Logs.Here().Information("TaskEmulatorDelayTimeInMilliseconds value now = {0}.", constantsSet.TaskEmulatorDelayTimeInMilliseconds.Value);

            // можно проверять предыдущее значение и, если новое такое же, не обновлять
            // но тогда надо проверять весь пакет и только если все не изменились, то не переписывать ключ
            // может быть когда-нибудь потом

            //switch (key)
            //{
            //    case "RecordActualityLevel":
            //        constantsSet.RecordActualityLevel.Value = value;
            //        Logs.Here().Information("Key = {0}, RecordActualityLevel was updated with value = {1}.", key, value);
            //        return constantsSet;
            //    case "TaskEmulatorDelayTimeInMilliseconds":
            //        constantsSet.TaskEmulatorDelayTimeInMilliseconds.Value = value;
            //        Logs.Here().Information("Key = {0}, TaskEmulatorDelayTimeInMilliseconds was updated with value = {1}.", key, value);
            //        return constantsSet;
            //    case "RandomRangeExtended":
            //        constantsSet.RandomRangeExtended.Value = value;
            //        Logs.Here().Information("Key = {0}, RandomRangeExtended was updated with value = {1}.", key, value);
            //        return constantsSet;
            //    case "BalanceOfTasksAndProcesses":
            //        constantsSet.BalanceOfTasksAndProcesses.Value = value;
            //        Logs.Here().Information("Key = {0}, BalanceOfTasksAndProcesses was updated with value = {1}.", key, value);
            //        return constantsSet;
            //    case "MaxProcessesCountOnServer":
            //        constantsSet.MaxProcessesCountOnServer.Value = value;
            //        Logs.Here().Information("Key = {0}, MaxProcessesCountOnServer was updated with value = {1}.", key, value);
            //        return constantsSet;
            //    case "MinBackProcessesServersCount":
            //        constantsSet.MinBackProcessesServersCount.Value = value; //Convert.ToInt32(value);
            //        Logs.Here().Information("Key = {0}, MinBackProcessesServersCount was updated with value = {1}.", key, value);
            //        return constantsSet;
            //}

            // удалять поле, с которого считано обновление

            // можно добавить сообщение, что модифицировать константу не удалось
            // ещё можно показать значения - бывшее и которое хотели обновить
            //Logs.Here().Error("Constant {@K} will be left unchanged", new { Key = key });

            return constantsSet;
        }
    }
}
