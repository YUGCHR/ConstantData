using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
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
        private readonly IInitConstantsService _init;
        private readonly IConstantsCollectionService _collection;
        private readonly ISharedDataAccess _data;
        private readonly ICacheManageService _cache;
        private readonly ISettingConstantsService _constantService;
        private readonly CancellationToken _cancellationToken;
        private readonly IOnKeysEventsSubscribeService _subscribe;
        private readonly string _guid;

        public MonitorLoop(
            GenerateThisInstanceGuidService thisGuid,
            ISharedDataAccess data,
            ICacheManageService cache,
            ISettingConstantsService constantService,
            IHostApplicationLifetime applicationLifetime,
            IInitConstantsService init,
            IOnKeysEventsSubscribeService subscribe,
            IConstantsCollectionService collection)
        {
            _data = data;
            _constantService = constantService;
            _init = init;
            _subscribe = subscribe;
            _collection = collection;
            _cache = cache;
            _cancellationToken = applicationLifetime.ApplicationStopping;
            _guid = thisGuid.ThisBackServerGuid();
        }

        private static Serilog.ILogger Logs => Serilog.Log.ForContext<MonitorLoop>();

        private const string CheckToken = "tt-tt-tt";

        public void StartMonitorLoop()
        {
            //_logger.LogInformation("ConstantsMountingMonitor Loop is starting.");
            Logs.Here().Information("ConstantsMountingMonitor Loop is starting.");


            // Run a console user input loop in a background thread
            Task.Run(ConstantsMountingMonitor, _cancellationToken);
        }

        public async Task ConstantsMountingMonitor()
        {
            DictionaryTest();


            EventKeyNames eventKeysSet = _init.InitialiseEventKeyNames();

            string dataServerPrefixGuid = $"constant:data:{_guid}";

            //_logger.LogInformation(10351, "1 ConstantCheck EventKeyFrontGivesTaskTimeDays = {0}.", eventKeysSet.EventKeyFrontGivesTaskTimeDays);
            Logs.Here().Information("ConstantCheck EventKeyFrontGivesTaskTimeDays = {0}.", eventKeysSet.EventKeyFrontGivesTaskTimeDays);

            (string startConstantKey, string constantsStartLegacyField, string constantsStartGuidField) = _data.FetchBaseConstants();
            //_logger.LogInformation(10350, "ConstantData send constants {0} to SetStartConstants.", eventKeysSet, "constants");
            Logs.Here().Information("ConstantData send constants to SetStartConstants.");

            eventKeysSet.ConstantsVersionBase = startConstantKey;
            eventKeysSet.ConstantsVersionNumber = 0;

            // записываем константы в стартовый ключ и старое поле (для совместимости)
            await _cache.SetStartConstants(startConstantKey, constantsStartLegacyField, eventKeysSet);

            //сервер констант имеет свой гуид и это ключ обновляемых констант
            //его он пишет в поле для нового гуид-ключа для всех
            //на этот ключ уже можно подписаться, он стабильный на всё время существования сервера
            //если этот ключ исчезнет(сервер перезапустился), то надо перейти на базовый ключ и искать там
            //на этом ключе будут сменяемые поля с константами - новое появилась, старое удалили
            //тогда будет смысл в подписке
            //в подписке всё равно мало смысла, даже если есть известие от подписки, надо проверять наличие гуид-ключа -
            //может же сервер исчезнуть к этому времени, забрав с собой ключ
            //можно ключ не удалять, даже нужно - если сервер упадёт неожиданно, то ключи всё равно останутся
            //но ключ может и исчезнуть сам по себе, надо проверять
            //наверное, подписка имеет смысл для мгновенной реакции или для длительного ожидания
            //если сервер простаивает, то обновления констант ему всё равно не нужны
            //если, конечно, не обновятся какие-то базовые ключи, но это допускать нельзя
            //можно разделить набор на два - изменяемый и постоянный
            //постоянные инициализовать через инит, а остальные добавлять по ходу - по ключам изменения
            //поэтому сервер получит новые константы после захвата пакета

            // записываем в стартовый ключ и новое поле гуид-ключ обновляемых констант
            //string constantsStartGuidKey = Guid.NewGuid().ToString();
            await _cache.SetConstantsStartGuidKey(startConstantKey, constantsStartGuidField, dataServerPrefixGuid); //string startConstantKey, string startConstantField, string constantsStartGuidKey

            eventKeysSet.ConstantsVersionBase = dataServerPrefixGuid;
            eventKeysSet.ConstantsVersionNumber++;

            // записываем константы в новый гуид-ключ и новое поле (надо какое-то всем известное поле)
            // потом может быть будет поле-версия, а может будет меняться ключ
            await _cache.SetStartConstants(dataServerPrefixGuid, constantsStartGuidField, eventKeysSet);

            // подписываемся на ключ сообщения о необходимости обновления констант

            _subscribe.SubscribeOnEventUpdate(eventKeysSet, constantsStartGuidField, _cancellationToken);

            // можно загрузить константы обратно и проверить
            // а можно подписаться на ключ и следить, чтобы никто не лез в константы
            //EventKeyNames eventKeysSetCheck = await _data.FetchAllConstants();
            //_logger.LogInformation(10362, "2 ConstantCheck EventKeyFrontGivesTaskTimeDays = {0}.", eventKeysSetCheck.EventKeyFrontGivesTaskTimeDays);

            //_subscribe.SubscribeOnEventFrom(eventKeysSet);







            while (true)
            {
                if (_cancellationToken.IsCancellationRequested)
                {
                    bool res = await _cache.DeleteKeyIfCancelled(startConstantKey);
                    //_logger.LogInformation(310310, "_cancellationToken was received, key was removed = {KeyStroke}.", res);
                    Logs.Here().Warning("Cancellation Token was received, key was removed = {KeyStroke}.", res);

                    return;
                }

                var keyStroke = Console.ReadKey();

                if (keyStroke.Key == ConsoleKey.W)
                {
                    //_logger.LogInformation(10370, "ConsoleKey was received {KeyStroke}.", keyStroke.Key);
                    Logs.Here().Information("ConsoleKey was received {KeyStroke}.", keyStroke.Key);
                }

                await Task.Delay(10, _cancellationToken);
            }
        }

        public void DictionaryTest()
        {
            Logs.Here().Information("Constants in Dictionary Test started.");

            List<ConstantNameValue> aaa = _collection.SettingConstants.ConstantsList;
            List<KeyNameValueLifeTime> bbb = _collection.SettingConstants.KeysList;

            int count1 = aaa.Count;
            for (int n = 0; n < count1; n++)
            {
                Logs.Here().Information("Key No: {0}, description - {1}, \n property name = {2}, \n value = {3}, time = {4}.", n, aaa[n].Description, aaa[n].PropertyName, aaa[n].Value, aaa[n].LifeTime);
            }

            int count2 = bbb.Count;
            for (int n = 0; n < count2; n++)
            {
                Logs.Here().Information("Key No: {0}, description - {1}, \n property name = {2}, \n value = {3}, time = {4}.", n, bbb[n].Description, bbb[n].PropertyName, bbb[n].Value, bbb[n].LifeTime);
            }


        }
    }
}
