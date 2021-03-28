using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CachingFramework.Redis.Contracts;
using CachingFramework.Redis.Contracts.Providers;
using Microsoft.Extensions.Logging;
using Shared.Library.Models;

namespace BackgroundTasksQueue.Services
{
    public interface IOnKeysEventsSubscribeService
    {
        //public Task<string> FetchGuidFieldTaskRun(string eventKeyRun, string eventFieldRun); // NOT USED
        public void SubscribeOnEventRun(EventKeyNames eventKeysSet);
        //public void SubscribeOnEventServerGuid(EventKeyNames eventKeysSet); // NOT USED
        //public void SubscribeOnEventCheckPackageProgress(EventKeyNames eventKeysSet, string tasksPackageGuidField);
    }

    public class OnKeysEventsSubscribeService : IOnKeysEventsSubscribeService
    {
        private readonly IBackgroundTasksService _task2Queue;
        private readonly ILogger<OnKeysEventsSubscribeService> _logger;
        private readonly ICacheProviderAsync _cache;
        private readonly IKeyEventsProvider _keyEvents;
        private readonly ITasksPackageCaptureService _captures;
        private readonly ITasksBatchProcessingService _processing;
        private readonly ITasksProcessingControlService _control;

        public OnKeysEventsSubscribeService(
            ILogger<OnKeysEventsSubscribeService> logger,
            ICacheProviderAsync cache,
            IKeyEventsProvider keyEvents,
            IBackgroundTasksService task2Queue,
            ITasksPackageCaptureService captures,
            ITasksBatchProcessingService processing,
            ITasksProcessingControlService control)
        {
            _task2Queue = task2Queue;
            _logger = logger;
            _cache = cache;
            _keyEvents = keyEvents;
            _captures = captures;
            _processing = processing;
            _control = control;
        }

        private static Serilog.ILogger Logs => Serilog.Log.ForContext<OnKeysEventsSubscribeService>();

        private bool _flagToBlockEventRun;

        public async Task<string> FetchGuidFieldTaskRun(string eventKeyRun, string eventFieldRun) // NOT USED
        {
            string eventGuidFieldRun = await _cache.GetHashedAsync<string>(eventKeyRun, eventFieldRun); //получить guid поле для "task:run"

            return eventGuidFieldRun;
        }

        // подписываемся на ключ сообщения о появлении свободных задач
        public void SubscribeOnEventRun(EventKeyNames eventKeysSet)
        {
            string eventKeyFrontGivesTask = eventKeysSet.EventKeyFrontGivesTask;
            Logs.Here().Information("BackServer subscribed on {@E}.", new { EventKey = eventKeyFrontGivesTask });

            // блокировка множественной подписки до специального разрешения повторной подписки
            _flagToBlockEventRun = true;
            // 
            _keyEvents.Subscribe(eventKeyFrontGivesTask, async (string key, KeyEvent cmd) =>
            {
                if (cmd == eventKeysSet.EventCmd && _flagToBlockEventRun)
                {
                    // подписка заблокирована
                    _flagToBlockEventRun = false;
                    Logs.Here().Debug("Key {Key} with command {Cmd} was received, Event permit = {Flag}.", eventKeyFrontGivesTask, cmd, _flagToBlockEventRun);
                    // можно добавить счётчик событий для дебага
                    // _flagToBlockEventRun вернется true, только если задачу добыть не удалось
                    _flagToBlockEventRun = await FreshTaskPackageAppeared(eventKeysSet);
                    // просто сделать _flagToBlockEventRun true ничего не даёт - оставшиеся в ключе задачи не вызовут подписку
                    // если задача получена и пошла в работу, то вернётся false и на true ключ поменяют в другом месте (запутанно, но пока так)
                    // и там сначала ещё раз проверить ключ кафе задач и если ещё остались задачи, вызвать FreshTaskPackageAppeared
                }
                // другая подписка восстановит true в _flagToBlockEventRun, чтобы возобновить подписку 
                // или можно void Unsubscribe(string key), тогда без глобальной переменной
            });

            string eventKeyCommand = $"Key = {eventKeyFrontGivesTask}, Command = {eventKeysSet.EventCmd}";
            Logs.Here().Debug("You subscribed on {@EK}.", new{EventSet = eventKeyCommand});
        }

        private async Task<bool> FreshTaskPackageAppeared(EventKeyNames eventKeysSet) // Main of EventKeyFrontGivesTask key
        {
            // вернуть все подписки сюда
            // метод состоит из трёх частей -
            // 1 попытка захвата пакета задач, если ни один пакет захватить не удалось, возвращаемся обратно в эту подписку ждать следующих пакетов
            // 2 если пакет захвачен, подписываемся на его гуид
            // 3 начинаем обработку - регистрация, помещение задач в очередь и создание нужного количества процессов
            // если всё удачно, возвращаемся сюда, оставив подписку заблокированной

            string tasksPackageGuidField = await _captures.AttemptToCaptureTasksPackage(eventKeysSet);
            _logger.LogInformation(30410, "\n --- AttemptToCaptureTasksPackage finished and TaskPackageKey {0} was captured.", tasksPackageGuidField);

            // если flagToBlockEventRun null, сразу возвращаемся с true для возобновления подписки
            if (tasksPackageGuidField != null)
            {
                // вызывать подписку на tasksPackageGuidField прямо здесь, а не городить лишние ключи
                // подписка на ключ пакета задач для контроля выполнения - задачи должны сюда (или в ключ с префиксом) отчитываться о ходе выполнения
                SubscribeOnEventCheckPackageProgress(eventKeysSet, tasksPackageGuidField);
                SubscribeOnEventPackageCompleted(eventKeysSet, tasksPackageGuidField);

                bool flagToBlockEventRun = await _processing.WhenTasksPackageWasCaptured(eventKeysSet, tasksPackageGuidField);
                // всегда возвращаем false - задачи отправлены в работу и подписку восстановит модуль контроля завершения пакета
                // и ещё сначала проверит, не остались ли ещё других пакетов в кафе
                return flagToBlockEventRun; 
            }
            // возвращаем true, потому что задачу добыть не удалось, пакетов больше нет и надо ждать следующего вброса 
            return true;
        }

        // вызвать из монитора или откуда-то из сервиса?
        // точно не из монитора - там неизвестен гуид пакета
        // можно из первого места, где получаем гуид пакета
        // в мониторе подписываемся на ключ сервера и когда там появится номер пакета задач, подписываемся на него
        // нет, все подписки здесь

        private void SubscribeOnEventCheckPackageProgress(EventKeyNames eventKeysSet, string tasksPackageGuidField)
        {
            _logger.LogInformation(30510, "This BackServer subscribed on key {0}.", tasksPackageGuidField);

            // блокировка множественной подписки до специального разрешения повторной подписки
            bool flagToBlockEventCheckPackageProgress = true;
            // флаг блокировки повторного вызова обработчика
            //int knockingOnDoorWhileYouWereNotAtHome = 0;

            _keyEvents.Subscribe(tasksPackageGuidField, async (string key, KeyEvent cmd) =>
            {
                if (cmd == eventKeysSet.EventCmd && flagToBlockEventCheckPackageProgress)
                {
                    flagToBlockEventCheckPackageProgress = false;
                    _logger.LogInformation(30516, "\n --- Key {Key} with command {Cmd} was received, flagToBlockEventCheck = {Flag}.", tasksPackageGuidField, cmd, flagToBlockEventCheckPackageProgress);

                    // вернуть изменённое значение flagEvent из CheckingAllTasksCompletion для возобновления подписки
                    // проверяем текущее состояние пакета задач, если ещё выполняется, возобновляем подписку на ключ пакета
                    // если выполнение окончено, подписку возобновляем или нет? но тогда восстанавливаем ключ подписки на вброс пакетов задач
                    // возвращаем состояние выполнения - ещё выполняется или уже окончено
                    // если выполняется, то true и им же возобновляем эту подписку
                    bool allTasksCompleted = await _control.CheckingAllTasksCompletion(eventKeysSet, tasksPackageGuidField);
                    _logger.LogInformation(30519, "CheckingAllTasksCompletion finished with flag = {0}.", flagToBlockEventCheckPackageProgress);

                    // дополнительно проверять ключ окончания пакета и по нему полностью отменить подписку
                }
            });

            string eventKeyCommand = $"Key = {tasksPackageGuidField}, Command = {eventKeysSet.EventCmd}";
            Logs.Here().Debug("You subscribed on {@EK}.", new { EventSet = eventKeyCommand });
        }

        private void SubscribeOnEventPackageCompleted(EventKeyNames eventKeysSet, string tasksPackageGuidField)
        {
            string backServerPrefixGuid = eventKeysSet.BackServerPrefixGuid;
            _logger.LogInformation(30510, "This BackServer subscribed on key {0}.", backServerPrefixGuid);

            // блокировка множественной подписки до специального разрешения повторной подписки
            bool flagToBlockEventPackageCompleted = true;

            _keyEvents.Subscribe(backServerPrefixGuid, async (string key, KeyEvent cmd) =>
            {
                if (cmd == eventKeysSet.EventCmd && flagToBlockEventPackageCompleted)
                {
                    flagToBlockEventPackageCompleted = false;
                    _logger.LogInformation(30516, "\n --- Key {Key} with command {Cmd} was received, flagToBlockEventCheck = {Flag}.", backServerPrefixGuid, cmd, flagToBlockEventPackageCompleted);
                    // проверить значение в ключе сервера - если больше нуля, значит, ещё не закончено
                    flagToBlockEventPackageCompleted = await _control.CheckingATaskPackageCompletion(eventKeysSet, tasksPackageGuidField);
                    _logger.LogInformation(30519, "CheckingAllTasksCompletion finished with flag = {0}.", flagToBlockEventPackageCompleted);
                    // если пакет в работе, вернулось true и опять ждём подписку
                    // если пакет закончен, оставим навсегда false, этот пакет нас больше не интересует, но, перед восстановлением глобального ключа, ещё надо проверить кафе
                    // или может полностью отменить подписку? вроде бы разницы нет, всё равно сюда теперь попадём только по новой подписке на этот же ключ, поэтому отменять нет смысла

                    if (!flagToBlockEventPackageCompleted)
                    {
                        // перед восстановлением глобального ключа, надо ещё проверить кафе - стандартным способом
                        _flagToBlockEventRun = await FreshTaskPackageAppeared(eventKeysSet);
                        // если задач там больше нет, вернётся true, восстановим глобальную подписку и будем ждать 
                        // а если пакет есть, вернётся false и все пойдет привычным путём, а потом придёт опять сюда по новой подписке
                    }
                }
            });

            string eventKeyCommand = $"Key = {tasksPackageGuidField}, Command = {eventKeysSet.EventCmd}";
            Logs.Here().Debug("You subscribed on {@EK}.", new { EventSet = eventKeyCommand });
        }

        // по ключу сервера можно дополнительно контролировать окончание пакета, если удалять поле пакета после его окончания (но как?)
        public void SubscribeOnEventServerGuid(EventKeyNames eventKeysSet) // NOT USED
        {
            string backServerPrefixGuid = eventKeysSet.BackServerPrefixGuid;
            _logger.LogInformation(19701, "This BackServer subscribed on key {0}.", backServerPrefixGuid);

            // типовая блокировка множественной подписки до специального разрешения повторной подписки
            // здесь не надо блокировать - пока что
            //bool flagToBlockEventCheck = true;

            _keyEvents.Subscribe(backServerPrefixGuid, async (string key, KeyEvent cmd) =>
            {
                if (cmd == eventKeysSet.EventCmd) // && flagToBlockEventCheck)
                {
                    // временная защёлка, чтобы подписка выполнялась один раз - нет
                    //flagToBlockEventCheck = false;
                    _logger.LogInformation(19703, "Key {Key} with command {Cmd} was received.", backServerPrefixGuid, cmd); //, flagToBlockEventCheck = {Flag} , flagToBlockEventCheck);

                    // получить ключ guidField - это не так просто, если выполняется уже не первый пакет
                    // надо как-то получить последнее созданное поле
                    // пока в значение там int в процентах и в свежесозданном поле можно ставить -1
                    // но потом там будет класс/модель и будет сложнее
                    // можно использовать разные префиксы для хранения простых процентов и модели
                    // достать все поля, проверить, что размер словаря больше нуля
                    // (пока поля не удаляем, но кто знает, что будет дальше)
                    // перебрать словарь и найти значение меньше нуля - оно должно быть одно
                    // можно искать до первого отрицательного и выйти
                    // или можно перебрать все и, если отрицательное не одно, сообщить об ошибке
                    //string newlyPackageGuid = await FindFreshPackageGuidField(eventKeysSet);

                    // вернуть изменённое значение flagEvent из AttemptToCaptureTasksPackage для возобновления подписки - нет
                    //SubscribeOnEventCheck(eventKeysSet, newlyPackageGuid);

                    // что будет, если во время ожидания AttemptToCaptureTasksPackage придёт новое сообщение по подписке? проверить экспериментально
                    _logger.LogInformation(19705, "END - AttemptToCaptureTasksPackage finished and This BackServer waits the next event.");
                }
            });

            string eventKeyCommand = $"Key = {backServerPrefixGuid}, Command = {eventKeysSet.EventCmd}";
            Logs.Here().Debug("You subscribed on {@EK}.", new { EventSet = eventKeyCommand });
        }
    }
}
