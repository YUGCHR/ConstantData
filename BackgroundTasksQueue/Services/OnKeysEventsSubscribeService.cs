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
        //private readonly ILogger<OnKeysEventsSubscribeService> _logger;
        private readonly ICacheProviderAsync _cache;
        private readonly IKeyEventsProvider _keyEvents;
        private readonly ITasksPackageCaptureService _captures;
        private readonly ITasksBatchProcessingService _processing;
        private readonly ITasksProcessingControlService _control;

        public OnKeysEventsSubscribeService(
            //ILogger<OnKeysEventsSubscribeService> logger,
            ICacheProviderAsync cache,
            IKeyEventsProvider keyEvents,
            IBackgroundTasksService task2Queue,
            ITasksPackageCaptureService captures,
            ITasksBatchProcessingService processing,
            ITasksProcessingControlService control)
        {
            _task2Queue = task2Queue;
            //_logger = logger;
            _cache = cache;
            _keyEvents = keyEvents;
            _captures = captures;
            _processing = processing;
            _control = control;
        }

        private static Serilog.ILogger Logs => Serilog.Log.ForContext<OnKeysEventsSubscribeService>();

        private bool _flagToBlockEventRun;
        private bool _eventCompletedTaskWasHappening;
        private bool _processingEventCompletedTaskIsLaunched;

        public async Task<string> FetchGuidFieldTaskRun(string eventKeyRun, string eventFieldRun) // NOT USED
        {
            string eventGuidFieldRun = await _cache.GetHashedAsync<string>(eventKeyRun, eventFieldRun); //получить guid поле для "task:run"

            return eventGuidFieldRun;
        }

        // подписываемся на ключ сообщения о появлении свободных задач
        public void SubscribeOnEventRun(EventKeyNames eventKeysSet)
        {
            string eventKeyFrontGivesTask = eventKeysSet.EventKeyFrontGivesTask;
            Logs.Here().Information("BackServer subscribed on EventKey. \n {@E}", new { EventKey = eventKeyFrontGivesTask });

            // блокировка множественной подписки до специального разрешения повторной подписки
            _flagToBlockEventRun = true;
            // 
            _keyEvents.Subscribe(eventKeyFrontGivesTask, async (string key, KeyEvent cmd) =>
            {
                if (cmd == eventKeysSet.EventCmd && _flagToBlockEventRun)
                {
                    // подписка заблокирована
                    _flagToBlockEventRun = false;
                    Logs.Here().Debug("FreshTaskPackageAppeared called, Event permit = {Flag} \n {@K} with {@C} was received. \n", _flagToBlockEventRun, new{Key = eventKeyFrontGivesTask}, new{Command = cmd});
                    // можно добавить счётчик событий для дебага
                    // _flagToBlockEventRun вернется true, только если задачу добыть не удалось
                    _flagToBlockEventRun = await FreshTaskPackageAppeared(eventKeysSet);
                    Logs.Here().Debug("FreshTaskPackageAppeared returned Event permit = {Flag}.", _flagToBlockEventRun);

                    // просто сделать _flagToBlockEventRun true ничего не даёт - оставшиеся в ключе задачи не вызовут подписку
                    // если задача получена и пошла в работу, то вернётся false и на true ключ поменяют в другом месте (запутанно, но пока так)
                    // и там сначала ещё раз проверить ключ кафе задач и если ещё остались задачи, вызвать FreshTaskPackageAppeared
                }
                // другая подписка (SubscribeOnEventPackageCompleted) восстановит true в _flagToBlockEventRun, чтобы возобновить подписку 
                // или можно void Unsubscribe(string key), тогда без глобальной переменной
            });

            string eventKeyCommand = $"Key = {eventKeyFrontGivesTask}, Command = {eventKeysSet.EventCmd}";
            Logs.Here().Debug("You subscribed on EventSet. \n {@ES}", new { EventSet = eventKeyCommand });
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
            Logs.Here().Information("AttemptToCaptureTasksPackage captured the TaskPackage. \n {@T}.", new { TaskPackage = tasksPackageGuidField });

            // если flagToBlockEventRun null, сразу возвращаемся с true для возобновления подписки
            if (tasksPackageGuidField != null)
            {
                // вызывать подписку на tasksPackageGuidField прямо здесь, а не городить лишние ключи
                // подписка на ключ пакета задач для контроля выполнения - задачи должны сюда (или в ключ с префиксом) отчитываться о ходе выполнения
                // убрать подписку на tasksPackageGuidField, запрашивать состояние выполнения из внешнего запроса
                //SubscribeOnEventCheckPackageProgress(eventKeysSet, tasksPackageGuidField);
                SubscribeOnEventPackageCompleted(eventKeysSet, tasksPackageGuidField);
                Logs.Here().Debug("SubscribeOnEventPackageCompleted subscribed, WhenTasksPackageWasCaptured called. \n {@K}", new { PackageKey = tasksPackageGuidField });

                bool flagToBlockEventRun = await _processing.WhenTasksPackageWasCaptured(eventKeysSet, tasksPackageGuidField);
                Logs.Here().Debug("WhenTasksPackageWasCaptured returned Event permit = {Flag}.", flagToBlockEventRun);

                // всегда возвращаем false - задачи отправлены в работу и подписку восстановит модуль контроля завершения пакета
                // и ещё сначала проверит, не остались ли ещё других пакетов в кафе
                return flagToBlockEventRun;
            }
            // возвращаем true, потому что задачу добыть не удалось, пакетов больше нет и надо ждать следующего вброса
            Logs.Here().Information("This Server finished current work.\n {@S} \n Global {@PR} \n", new { Server = eventKeysSet.BackServerPrefixGuid }, new { Permit = _flagToBlockEventRun });
            Logs.Here().Warning("Next package could not be obtained - there are no more packages in cafe.");
            return true;
        }

        // вызвать из монитора или откуда-то из сервиса?
        // точно не из монитора - там неизвестен гуид пакета
        // можно из первого места, где получаем гуид пакета
        // в мониторе подписываемся на ключ сервера и когда там появится номер пакета задач, подписываемся на него
        // нет, все подписки здесь

        private void SubscribeOnEventCheckPackageProgress(EventKeyNames eventKeysSet, string tasksPackageGuidField) // NOT USED
        {
            Logs.Here().Information("BackServer subscribed on {@E}.", new { EventKey = tasksPackageGuidField });

            // блокировка множественной подписки до специального разрешения повторной подписки
            bool flagToBlockEventCheckPackageProgress = true;
            // флаг блокировки повторного вызова обработчика
            //int knockingOnDoorWhileYouWereNotAtHome = 0;

            _keyEvents.Subscribe(tasksPackageGuidField, async (string key, KeyEvent cmd) =>
            {
                if (cmd == eventKeysSet.EventCmd && flagToBlockEventCheckPackageProgress)
                {
                    flagToBlockEventCheckPackageProgress = false;
                    Logs.Here().Debug("CheckingAllTasksCompletion called - Key {Key} with command {Cmd} was received, Event permit = {Flag}.", tasksPackageGuidField, cmd, flagToBlockEventCheckPackageProgress);

                    // вернуть изменённое значение flagEvent из CheckingAllTasksCompletion для возобновления подписки
                    // проверяем текущее состояние пакета задач, если ещё выполняется, возобновляем подписку на ключ пакета
                    // если выполнение окончено, подписку возобновляем или нет? но тогда восстанавливаем ключ подписки на вброс пакетов задач
                    // возвращаем состояние выполнения - ещё выполняется или уже окончено
                    // если выполняется, то true и им же возобновляем эту подписку
                    bool allTasksCompleted = await _control.CheckingAllTasksCompletion(eventKeysSet, tasksPackageGuidField);
                    Logs.Here().Debug("CheckingAllTasksCompletion returned Event permit = {Flag}.", flagToBlockEventCheckPackageProgress);




                    // тут подписка блокируется навсегда




                    // дополнительно проверять ключ окончания пакета и по нему полностью отменить подписку
                }
            });

            string eventKeyCommand = $"Key = {tasksPackageGuidField}, Command = {eventKeysSet.EventCmd}";
            Logs.Here().Debug("You subscribed on EventSet. \n {@ES}", new { EventSet = eventKeyCommand });
        }

        private void SubscribeOnEventPackageCompleted(EventKeyNames eventKeysSet, string tasksPackageGuidField)
        {
            // подписка на окончание единичной задачи (для проверки, все ли задачи закончились)
            string backServerPrefixGuid = eventKeysSet.BackServerPrefixGuid;
            Logs.Here().Information("BackServer subscribed on EventKey Server Guid. \n {@E}", new { EventKey = backServerPrefixGuid });
            
            _keyEvents.Subscribe(backServerPrefixGuid, (string key, KeyEvent cmd) => // async before action
            {
                if (cmd == eventKeysSet.EventCmd)// && flagToBlockEventPackageCompleted)
                {
                    // защёлка _eventCompletedTaskWasHappening - если true, то событие было, пока были заняты
                    _eventCompletedTaskWasHappening = true;
                    // проверка, запущен ли обработчик, если _processingEventCompletedTaskIsLaunched = true, то запущен
                    if (!_processingEventCompletedTaskIsLaunched)
                    {
                        // ставим обработчик_запущен true и перезапускаем обработчик, без ожидания
                        _processingEventCompletedTaskIsLaunched = true;
                        _ = ProcessingEventCompletedTask(eventKeysSet, tasksPackageGuidField);
                        // когда обработчик завершит работу, он сбросит этот флаг внутри себя
                    }
                }
            });

            string eventKeyCommand = $"Key = {tasksPackageGuidField}, Command = {eventKeysSet.EventCmd}";
            Logs.Here().Debug("You subscribed on EventSet. \n {@ES}", new { EventSet = eventKeyCommand });
        }

        public async Task ProcessingEventCompletedTask(EventKeyNames eventKeysSet, string tasksPackageGuidField)
        {
            // пока активно событие подписки на окончание задачи, проверяем общее состояние пакета
            while (_eventCompletedTaskWasHappening)
            {
                Logs.Here().Debug("Processing Event_Completed_Task is launched.");
                // признак, что ещё есть нерешённые задачи
                bool unsolvedTasksStillLeft;
                int totalUnsolvedTasksLeft;
                // перед проверкой готовности задач сбрасываем защёлку подписки
                _eventCompletedTaskWasHappening = false;
                Logs.Here().Debug("Flag Event_Completed_Task was happening was reset.");
                // задержку попробовать поставить здесь
                await Task.Delay(TimeSpan.FromSeconds(0.001)); //add cancellationToken
                // если до/во время проверки произойдёт новое событие, то сделаем ещё круг с повторной проверкой
                // проверить значение в ключе сервера - если больше нуля, значит, ещё не закончено
                (unsolvedTasksStillLeft, totalUnsolvedTasksLeft) = await _control.CheckingPackageCompletion(eventKeysSet, tasksPackageGuidField);
                Logs.Here().Debug("Flag Event_Completed_Task was happening now - {@F}.", new{Flag = _eventCompletedTaskWasHappening });

                // totalUnsolvedTasksLeft получаем только для отладки
                Logs.Here().Debug("CheckingPackageCompletion returned {@P}, {@L}.", new {Permit = unsolvedTasksStillLeft }, new {TasksLeft = totalUnsolvedTasksLeft});
                
                if (unsolvedTasksStillLeft)
                {
                    // если задачи ещё есть, завершить обработчик
                    // обработчик завершает работу, сбросить флаг
                    _processingEventCompletedTaskIsLaunched = false;
                    return;
                }

                // если все задачи кончились, восстановить глобальный флаг подписки на кафе
                _flagToBlockEventRun = true;
                Logs.Here().Warning("This Server waits new Task Package. \n {@S}", new { Server = eventKeysSet.BackServerPrefixGuid });
            }
        }

        // по ключу сервера можно дополнительно контролировать окончание пакета, если удалять поле пакета после его окончания (но как?)
        public void SubscribeOnEventServerGuid(EventKeyNames eventKeysSet) // NOT USED
        {
            string backServerPrefixGuid = eventKeysSet.BackServerPrefixGuid;
            Logs.Here().Information("BackServer subscribed on {@E}.", new { EventKey = backServerPrefixGuid });


            // типовая блокировка множественной подписки до специального разрешения повторной подписки
            // здесь не надо блокировать - пока что
            //bool flagToBlockEventCheck = true;

            _keyEvents.Subscribe(backServerPrefixGuid, async (string key, KeyEvent cmd) =>
            {
                if (cmd == eventKeysSet.EventCmd) // && flagToBlockEventCheck)
                {
                    // временная защёлка, чтобы подписка выполнялась один раз - нет
                    //flagToBlockEventCheck = false;
                    Logs.Here().Debug("___SubscribeOnEventServerGuid called - Key {Key} with command {Cmd} was received, Event permit = {Flag}.", backServerPrefixGuid, cmd, false);

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
                    Logs.Here().Debug("___SubscribeOnEventServerGuid returned Event permit = {Flag}.", false);
                }
            });

            string eventKeyCommand = $"Key = {backServerPrefixGuid}, Command = {eventKeysSet.EventCmd}";
            Logs.Here().Debug("You subscribed on EventSet. \n {@ES}", new { EventSet = eventKeyCommand });
        }
    }
}
