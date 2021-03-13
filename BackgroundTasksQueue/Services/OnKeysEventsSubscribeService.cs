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
        public Task<string> FetchGuidFieldTaskRun(string eventKeyRun, string eventFieldRun); // NOT USED
        public void SubscribeOnEventRun(EventKeyNames eventKeysSet);
        public void SubscribeOnEventServerGuid(EventKeyNames eventKeysSet); // NOT USED
        public void SubscribeOnEventCheck(EventKeyNames eventKeysSet, string tasksPackageGuidField);
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
            _logger.LogInformation(30201, "This BackServer subscribed on key {0}.", eventKeyFrontGivesTask);

            // блокировка множественной подписки до специального разрешения повторной подписки
            _flagToBlockEventRun = true;

            _keyEvents.Subscribe(eventKeyFrontGivesTask, async (string key, KeyEvent cmd) =>
            {
                if (cmd == eventKeysSet.EventCmd && _flagToBlockEventRun)
                {
                    // подписка заблокирована
                    _flagToBlockEventRun = false;
                    _logger.LogInformation(30310, "\n                      --- subscription blocked ---");
                    _logger.LogInformation(30311, "Key {Key} with command {Cmd} was received, flagToBlockEventRun = {Flag}.", eventKeyFrontGivesTask, cmd, _flagToBlockEventRun);

                    _flagToBlockEventRun = await FreshTaskPackageAppeared(eventKeysSet);
                }
                // другая подписка восстановит true в _flagToBlockEventRun, чтобы возобновить подписку 
                // или можно void Unsubscribe(string key), тогда без глобальной переменной
            });

            string eventKeyCommand = $"Key = {eventKeyFrontGivesTask}, Command = {eventKeysSet.EventCmd}";
            _logger.LogInformation(30320, "You subscribed on event - {EventKey}.", eventKeyCommand);
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
                SubscribeOnEventCheck(eventKeysSet, tasksPackageGuidField);

                bool flagToBlockEventRun = await _processing.WhenTasksPackageWasCaptured(eventKeysSet, tasksPackageGuidField);
                // возвращаем false
                return flagToBlockEventRun; // всё равно надо вернуть true, но мало ли - а вот
            }
            return true;
        }

        // вызвать из монитора или откуда-то из сервиса?
        // точно не из монитора - там неизвестен гуид пакета
        // можно из первого места, где получаем гуид пакета
        // в мониторе подписываемся на ключ сервера и когда там появится номер пакета задач, подписываемся на него
        // нет, все подписки здесь

        public void SubscribeOnEventCheck(EventKeyNames eventKeysSet, string tasksPackageGuidField)
        {
            _logger.LogInformation(30510, "This BackServer subscribed on key {0}.", tasksPackageGuidField);

            // блокировка множественной подписки до специального разрешения повторной подписки
            bool flagToBlockEventCheck = true;

            _keyEvents.Subscribe(tasksPackageGuidField, async (string key, KeyEvent cmd) =>
            {
                if (cmd == eventKeysSet.EventCmd && flagToBlockEventCheck)
                {
                    // временная защёлка, чтобы подписка выполнялась один раз
                    flagToBlockEventCheck = false;
                    _logger.LogInformation(30516, "\n --- Key {Key} with command {Cmd} was received, flagToBlockEventCheck = {Flag}.", tasksPackageGuidField, cmd, flagToBlockEventCheck);

                    // вернуть изменённое значение flagEvent из CheckingAllTasksCompletion для возобновления подписки
                    // проверяем текущее состояние пакета задач, если ещё выполняется, возобновляем подписку на ключ пакета
                    // если выполнение окончено, подписку возобновляем или нет? но тогда восстанавливаем ключ подписки на вброс пакетов задач
                    // возвращаем состояние выполнения - ещё выполняется или уже окончено
                    // если выполняется, то true и им же возобновляем эту подписку
                    int taskPackageStatePercentage = await _control.CheckingAllTasksCompletion(eventKeysSet, tasksPackageGuidField);
                    
                    if (taskPackageStatePercentage < 99)
                    {
                        flagToBlockEventCheck = true;
                    }

                    // если вернулось false, то восстанавливаем флаг прописки на ловлю задач
                    if (!flagToBlockEventCheck)
                    {
                        flagToBlockEventCheck = true;
                        _flagToBlockEventRun = true;
                    }
                    
                    _logger.LogInformation(30519, "CheckingAllTasksCompletion finished with flag = {0}.", flagToBlockEventCheck);
                }
            });

            string eventKeyCommand = $"Key = {tasksPackageGuidField}, Command = {eventKeysSet.EventCmd}";
            _logger.LogInformation(30526, "You subscribed on event - {EventKey}.", eventKeyCommand);
        }

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
            _logger.LogInformation(19707, "You subscribed on event - {EventKey}.", eventKeyCommand);
        }
    }
}
