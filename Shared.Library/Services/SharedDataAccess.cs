using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using CachingFramework.Redis.Contracts;
using CachingFramework.Redis.Contracts.Providers;
using Serilog;
using Shared.Library.Models;

namespace Shared.Library.Services
{
    public interface ISharedDataAccess
    {
        public (string, string, string) FetchBaseConstants([CallerMemberName] string currentMethodNameName = "");
        public Task<EventKeyNames> DeliveryOfUpdatedConstants(CancellationToken cancellationToken);
        public bool IsExistUpdatedConstants();
    }

    public class SharedDataAccess : ISharedDataAccess
    {
        private readonly ICacheProviderAsync _cache;
        private readonly IKeyEventsProvider _keyEvents;

        public SharedDataAccess(
            ICacheProviderAsync cache,
            IKeyEventsProvider keyEvents)
        {
            _cache = cache;
            _keyEvents = keyEvents;
        }

        private static Serilog.ILogger Logs => Serilog.Log.ForContext<SharedDataAccess>();
        
        private const string StartConstantKey = "constants";
        private const string ConstantsStartLegacyField = "all";
        private const string ConstantsStartGuidField = "constantsGuidField";
        private const KeyEvent SubscribedKeyEvent = KeyEvent.HashSet;

        private bool _constantsUpdateIsAppeared = false;
        private bool _wasSubscribedOnConstantsUpdate = false;

        // метод только для сервера констант - чтобы он узнал базовые ключ и поле, куда класть текущий ключ констант
        public (string, string, string) FetchBaseConstants([CallerMemberName] string currentMethodNameName = "") // May be will cause problem with Docker
        {
            // if problem with Docker can use token
            const string actualMethodNameWhichCanCallThis = "ConstantsMountingMonitor";
            if (currentMethodNameName != actualMethodNameWhichCanCallThis)
            {
                //_logger.LogError(710070, "FetchBaseConstants was called by wrong method - {0}.", currentMethodNameName);
                Logs.Here().Error("FetchBaseConstants was called by wrong method {@M}.", new { Method = currentMethodNameName });
                return (null, null, null);
            }
            return (StartConstantKey, ConstantsStartLegacyField, ConstantsStartGuidField);
        }

        // этот код - первое, что выполняется на старте - отсюда должны вернуться с константами

        // если констант нет, подписаться и ждать, если подписка здесь, то это общий код
        // можно вернуться без констант, но с ключом для подписки и подписаться в классе подписок - чтобы всё было в одном месте
        // кроме того, это позволит использовать один и тот же универсальный обработчик для всех подписок
        // наверное, разрывать процесс получения констант нехорошо - придётся у всех потребителей повторять подписку в своих классах
        // поэтому подписку оставляем здесь
        // тут должно быть законченное решение не только первоначального получения констант, но и их обновления
        // в подписке ниже поднимем флаг, что надо проверить обновление
        // и когда (если) приложение заглянет сюда проверить константы, запустить получение обновлённого ключа
        // по флагу ничего не проверять, только брать ключ и из него константы
        // сбросить флаг в начале проверки - в цикле while(этот флаг) и если за время проверки подписка опять сработает, то взять константы ещё раз
        
        // стартовый метод (местный main)
        public async Task<EventKeyNames> DeliveryOfUpdatedConstants(CancellationToken cancellationToken)
        {
            // проверить наличие базового ключа, проверить наличие поля обновлений, можно в одном методе
            // можно в первом проверить - если ключ есть, вернуть - старое поле, если нет нового и новое поле, если оно есть
            // если ключа нет вообще - null
            // следующий шаг - подписаться на ключ в любом варианте или можно подписаться в первую очередь
            // если ключ есть - достать значение поля - это будет или набор или строка
            // если набор - вернуть его и отменить подписку (значит, работает старый вариант констант)
            // если строка, использовать её как ключ (или поле?) и достать обновляемый набор

            // если ещё не подписаны (первый вызов) - подписаться
            // можно получать информацию о первом вызове от вызывающего метода - пусть проверит наличие констант и скажет
            // только накладные будут выше, наверное - зато без лишнего глобального флага

            // ограничим использование глобальных констант
            string startConstantKey = StartConstantKey;
            string constantsStartLegacyField = ConstantsStartLegacyField;
            string constantsStartGuidField = ConstantsStartGuidField;
            KeyEvent eventToSubscribe = SubscribedKeyEvent;

            // проверить, есть ли ключ вообще
            bool isExistStartConstantKey = await _cache.KeyExistsAsync(startConstantKey);

            if (!_wasSubscribedOnConstantsUpdate)
            {
                SubscribeOnAllConstantsEvent(startConstantKey, eventToSubscribe, isExistStartConstantKey);
            }

            EventKeyNames eventKeysSet = await FetchAllConstants(cancellationToken, isExistStartConstantKey, startConstantKey, constantsStartLegacyField, constantsStartGuidField);

            return eventKeysSet;
        }

        public bool IsExistUpdatedConstants()
        {
            return _constantsUpdateIsAppeared;
        }
        
        private async Task<EventKeyNames> FetchAllConstants(CancellationToken cancellationToken, bool isExistStartConstantKey, string startConstantKey, string constantsStartLegacyField, string constantsStartGuidField)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (isExistStartConstantKey)
                {
                    // если ключ есть, то есть ли поле обновляемых констант (и в нем поле гуид)
                    string dataServerPrefixGuid = await _cache.GetHashedAsync<string>(startConstantKey, constantsStartGuidField);

                    if (dataServerPrefixGuid == null)
                    {
                        // обновляемых констант нет в этой версии (или ещё нет), достаём старые и возвращаемся
                        return await _cache.GetHashedAsync<EventKeyNames>(startConstantKey, constantsStartLegacyField);
                    }

                    // есть обновлённые константы, достаём их, сбрасываем флаг наличия обновления и возвращаемся
                    EventKeyNames eventKeyNames = await _cache.GetHashedAsync<EventKeyNames>(dataServerPrefixGuid, constantsStartGuidField);
                    _constantsUpdateIsAppeared = false;
                    return eventKeyNames;
                }

                double timeToWaitTheConstants = 1;
                Logs.Here().Warning("SharedDataAccess cannot find constants and still waits them {0} sec more.", timeToWaitTheConstants);
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(timeToWaitTheConstants), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Prevent throwing if the Delay is cancelled
                }
            }
            // сюда можем попасть только при завершении сервера, константы уже никому не нужны
            return null;
        }

        // в этой подписке выставить флаг класса, что надо проверить обновление
        private void SubscribeOnAllConstantsEvent(string startConstantKey, KeyEvent eventToSubscribe, bool isExistStartConstantKey)
        {
            _wasSubscribedOnConstantsUpdate = true;
            Logs.Here().Information("SharedDataAccess will be subscribed on key {0}.", startConstantKey);

            // в константах подписку на ключ сервера сделать в самом начале и сразу проверить наличие констант на этом ключе, если есть, поднять флаг не в самой подписке, а ещё в подписке на подписку
            if (isExistStartConstantKey)
            {
                _constantsUpdateIsAppeared = true;
            }

            _keyEvents.Subscribe(startConstantKey, (string key, KeyEvent cmd) =>
            {
                if (cmd == eventToSubscribe)
                {
                    Logs.Here().Debug("Key {Key} with command {Cmd} was received.", StartConstantKey, cmd);

                    _constantsUpdateIsAppeared = true;

                    Logs.Here().Debug("Constants Update is appeared = {0}.", _constantsUpdateIsAppeared);
                }
            });
            Logs.Here().Information("SharedDataAccess was subscribed on key {0}.", startConstantKey);
        }
    }

    public static class LoggerExtensions
    {
        // https://stackoverflow.com/questions/29470863/serilog-output-enrich-all-messages-with-methodname-from-which-log-entry-was-ca/46905798

        public static ILogger Here(this ILogger logger, [CallerMemberName] string memberName = "", [CallerLineNumber] int sourceLineNumber = 0)
        //[CallerFilePath] string sourceFilePath = "",
        {
            return logger.ForContext("MemberName", memberName).ForContext("LineNumber", sourceLineNumber);
            //.ForContext("FilePath", sourceFilePath)
        }
    }
}