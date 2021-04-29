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
        public Task SubscribeOnEventRun(CancellationToken stoppingToken);
    }

    public class OnKeysEventsSubscribeService : IOnKeysEventsSubscribeService
    {
        private readonly ISettingConstants _constants;
        private readonly ICacheProviderAsync _cache;
        private readonly IKeyEventsProvider _keyEvents;
        private readonly ITasksPackageCaptureService _captures;
        private readonly ITasksBatchProcessingService _processing;
        private readonly ITasksProcessingControlService _control;

        public OnKeysEventsSubscribeService(
            ISettingConstants constants,
            ICacheProviderAsync cache,
            IKeyEventsProvider keyEvents,
            ITasksPackageCaptureService captures,
            ITasksBatchProcessingService processing,
            ITasksProcessingControlService control)
        {
            _constants = constants;
            _cache = cache;
            _keyEvents = keyEvents;
            _captures = captures;
            _processing = processing;
            _control = control;
        }

        private static Serilog.ILogger Logs => Serilog.Log.ForContext<OnKeysEventsSubscribeService>();

        private bool _flagToBlockEventRun;
        //private bool _flagToBlockEventUpdate;
        private bool _flagToBlockEventCompleted;
        private int _callingNumOfCheckKeyFrontGivesTask;
        
        //private bool _eventCompletedTaskWasHappening;
        //private bool _processingEventCompletedTaskIsLaunched;
        //private string _tasksPackageGuidField;

        public async Task<string> FetchGuidFieldTaskRun(string eventKeyRun, string eventFieldRun) // NOT USED
        {
            string eventGuidFieldRun = await _cache.GetHashedAsync<string>(eventKeyRun, eventFieldRun); //получить guid поле для "task:run"

            return eventGuidFieldRun;
        }

        // можно при получении нового пакета на мгновение заглянуть в константы и посмотреть на флаг
        // перед пакетом!
        // по сработке подписки на ключ кафе, там другого места и нет
        // в константах подписку на ключ сервера сделать в самом начале и сразу проверить наличие констант на этом ключе, если есть, поднять флаг не в самой подписке, а ещё в подписке на подписку
        
        // подписываемся на ключ сообщения о появлении свободных задач
        public async Task SubscribeOnEventRun(CancellationToken stoppingToken)
        {
            // 
            ConstantsSet constantsSet = await _constants.ConstantInitializer(stoppingToken);

            // все проверки и ожидание внутри метода, без констант не вернётся
            // но можно проверять на null, если как-то null, то что-то сделать (shutdown)
            //EventKeyNames eventKeysSet = await _constants.ConstantInitializer(stoppingToken);

            string eventKeyFrontGivesTask = constantsSet.EventKeyFrontGivesTask.Value;
            Logs.Here().Information("BackServer subscribed on EventKey. \n {@E}", new { EventKey = eventKeyFrontGivesTask });
            Logs.Here().Information("Constants version is {0}:{1}.", constantsSet.ConstantsVersionBase.Value, constantsSet.ConstantsVersionNumber.Value);

            // блокировка множественной подписки до специального разрешения повторной подписки
            _flagToBlockEventRun = true;
            // на старте вывecти состояние всех глобальных флагов
            //Logs.Here().Debug("SubscribeOnEventRun started with the following flags: => \n {@F1} \n {@F2} \n {@F3}", new { FlagToBlockEventRun = _flagToBlockEventRun }, new { EventCompletedTaskWasHappening = _eventCompletedTaskWasHappening }, new { ProcessingEventCompletedTaskIsLaunched = _processingEventCompletedTaskIsLaunched });

            _keyEvents.Subscribe(eventKeyFrontGivesTask, async (string key, KeyEvent cmd) =>
            {
                // скажем, в подписке вызывается метод проверить наличие пакетов в кафе(CheckKeyFrontGivesTask), если пакеты есть, он возвращает true, и метод за ним (FreshTaskPackageHasAppeared) начинает захват пакета
                // void Unsubscribe(string key);
                // 
                // 
                if (cmd == constantsSet.EventCmd && _flagToBlockEventRun)
                {
                    // подписка заблокирована
                    // быструю блокировку оставить - когда ещё отпишемся, но можно сделать локальной?
                    
                    _flagToBlockEventRun = false;
                    Logs.Here().Debug("CheckKeyFrontGivesTask will be called No:{0}, Event permit = {Flag} \n {@K} with {@C} was received. \n", _callingNumOfCheckKeyFrontGivesTask, _flagToBlockEventRun, new { Key = eventKeyFrontGivesTask }, new { Command = cmd });
                    // можно добавить счётчик событий для дебага
                    _ = CheckKeyFrontGivesTask(stoppingToken, constantsSet);
                }
            });

            string eventKeyCommand = $"Key = {eventKeyFrontGivesTask}, Command = {constantsSet.EventCmd}";
            Logs.Here().Debug("You subscribed on EventSet. \n {@ES}", new { EventSet = eventKeyCommand });
        }

        private async Task<bool> CheckKeyFrontGivesTask(CancellationToken stoppingToken, ConstantsSet constantsSet) // Main of EventKeyFrontGivesTask key
        {
            _callingNumOfCheckKeyFrontGivesTask++;
            if (_callingNumOfCheckKeyFrontGivesTask > 1)
            {
                Logs.Here().Warning("CheckKeyFrontGivesTask was called more than once - Calling Count = {0}.", _callingNumOfCheckKeyFrontGivesTask);
            }

            // тут определить, надо ли обновить константы
            bool isExistUpdatedConstants = _constants.IsExistUpdatedConstants();
            Logs.Here().Information("Is Exist Updated Constants = {0}.", isExistUpdatedConstants);

            if (isExistUpdatedConstants)
            {
                constantsSet = await _constants.ConstantInitializer(stoppingToken); //EventKeyNames
                Logs.Here().Information("Updated Constant = {0}.", constantsSet.TaskEmulatorDelayTimeInMilliseconds.Value);
            }

            string eventKeyFrontGivesTask = constantsSet.EventKeyFrontGivesTask.Value;
            // проверить существование ключа - если ключ есть, надо идти добывать пакет
            Logs.Here().Debug("KeyFrontGivesTask will be checked now.");
            bool isExistEventKeyFrontGivesTask = await _cache.KeyExistsAsync(eventKeyFrontGivesTask);
            Logs.Here().Debug("KeyFrontGivesTask {@E}.", new { isExisted = isExistEventKeyFrontGivesTask });

            if (isExistEventKeyFrontGivesTask)
            {
                // отменить подписку глубже, когда получится захватить пакет?
                _ = FreshTaskPackageHasAppeared(constantsSet, stoppingToken);
                Logs.Here().Debug("FreshTaskPackageHasAppeared was passed, Subscribe permit = {Flag}.", _flagToBlockEventRun);

                Logs.Here().Debug("CheckKeyFrontGivesTask finished No:{0}.", _callingNumOfCheckKeyFrontGivesTask);
                _callingNumOfCheckKeyFrontGivesTask--;

                return false;
            }

            // всё_протухло - пакетов нет, восстановить подписку (also Subscribe to Update) и ждать погоду
            _flagToBlockEventRun = true;
            //_flagToBlockEventUpdate = true;
            Logs.Here().Information("This Server finished current work.\n {@S} \n Global {@PR} \n", new { Server = constantsSet.BackServerPrefixGuid.Value }, new { Permit = _flagToBlockEventRun });
            Logs.Here().Warning("Next package could not be obtained - there are no more packages in cafe.");
            string packageSeparator1 = new('Z', 80);
            Logs.Here().Warning("This Server waits new Task Package. \n {@S} \n {1} \n", new { Server = constantsSet.BackServerPrefixGuid.Value }, packageSeparator1);

            Logs.Here().Debug("CheckKeyFrontGivesTask finished No:{0}.", _callingNumOfCheckKeyFrontGivesTask);
            _callingNumOfCheckKeyFrontGivesTask--;

            return true;
        }

        private async Task<bool> FreshTaskPackageHasAppeared(ConstantsSet constantsSet, CancellationToken stoppingToken)
        {
            // вернуть все подписки сюда
            // метод состоит из трёх частей -
            // 1 попытка захвата пакета задач, если ни один пакет захватить не удалось, возвращаемся обратно в эту подписку ждать следующих пакетов
            // 2 если пакет захвачен, подписываемся на его гуид
            // 3 начинаем обработку - регистрация, помещение задач в очередь и создание нужного количества процессов
            // если всё удачно, возвращаемся сюда, оставив подписку заблокированной

            string tasksPackageGuidField = await _captures.AttemptToCaptureTasksPackage(constantsSet, stoppingToken);

            // если flagToBlockEventRun null, сразу возвращаемся с true для возобновления подписки
            if (tasksPackageGuidField != null)
            {
                Logs.Here().Information("AttemptToCaptureTasksPackage captured the TaskPackage. \n {@T}.", new { TaskPackage = tasksPackageGuidField });
                string packageSeparator0 = new('#', 90);
                Logs.Here().Warning("AttemptToCaptureTasksPackage captured new TaskPackage. \n {0} \n", packageSeparator0);

                // вызывать подписку на tasksPackageGuidField прямо здесь, а не городить лишние ключи
                // подписка на ключ пакета задач для контроля выполнения - задачи должны сюда (или в ключ с префиксом) отчитываться о ходе выполнения
                // убрать подписку на tasksPackageGuidField, запрашивать состояние выполнения из внешнего запроса
                //SubscribeOnEventCheckPackageProgress(eventKeysSet, tasksPackageGuidField);
                SubscribeOnEventPackageCompleted(constantsSet, tasksPackageGuidField, stoppingToken);
                //Logs.Here().Debug("SubscribeOnEventPackageCompleted subscribed, WhenTasksPackageWasCaptured called. \n {@K}", new { PackageKey = tasksPackageGuidField });
                //_tasksPackageGuidField = tasksPackageGuidField;
                _ = _processing.WhenTasksPackageWasCaptured(constantsSet, tasksPackageGuidField, stoppingToken);
                Logs.Here().Debug("WhenTasksPackageWasCaptured passed without awaiting.");

                // всегда возвращаем false - задачи отправлены в работу и подписку восстановит модуль контроля завершения пакета
                // и ещё сначала проверит, не остались ли ещё других пакетов в кафе
                return false;
            }

            // возвращаем true, потому что задачу добыть не удалось, пакетов больше нет и надо ждать следующего вброса
            _flagToBlockEventRun = true;

            Logs.Here().Information("This Server finished current work.\n {@S} \n Global {@PR} \n", new { Server = constantsSet.BackServerPrefixGuid.Value }, new { Permit = _flagToBlockEventRun });
            Logs.Here().Warning("Next package could not be obtained - there are no more packages in cafe.");
            string packageSeparator1 = new('-', 80);
            Logs.Here().Warning("This Server waits new Task Package. \n {@S} \n {1} \n", new { Server = constantsSet.BackServerPrefixGuid.Value }, packageSeparator1);

            return true;
        }

        private void SubscribeOnEventCheckPackageProgress(ConstantsSet constantsSet, string tasksPackageGuidField) // NOT USED
        {
            Logs.Here().Information("BackServer subscribed on {@E}.", new { EventKey = tasksPackageGuidField });

            // блокировка множественной подписки до специального разрешения повторной подписки
            bool flagToBlockEventCheckPackageProgress = true;
            // флаг блокировки повторного вызова обработчика
            //int knockingOnDoorWhileYouWereNotAtHome = 0;

            _keyEvents.Subscribe(tasksPackageGuidField, async (string key, KeyEvent cmd) =>
            {
                if (cmd == constantsSet.EventCmd && flagToBlockEventCheckPackageProgress)
                {
                    flagToBlockEventCheckPackageProgress = false;
                    Logs.Here().Debug("CheckingAllTasksCompletion called - Key {Key} with command {Cmd} was received, Event permit = {Flag}.", tasksPackageGuidField, cmd, flagToBlockEventCheckPackageProgress);

                    // вернуть изменённое значение flagEvent из CheckingAllTasksCompletion для возобновления подписки
                    // проверяем текущее состояние пакета задач, если ещё выполняется, возобновляем подписку на ключ пакета
                    // если выполнение окончено, подписку возобновляем или нет? но тогда восстанавливаем ключ подписки на вброс пакетов задач
                    // возвращаем состояние выполнения - ещё выполняется или уже окончено
                    // если выполняется, то true и им же возобновляем эту подписку
                    bool allTasksCompleted = await _control.CheckingAllTasksCompletion(constantsSet, tasksPackageGuidField);
                    Logs.Here().Debug("CheckingAllTasksCompletion returned Event permit = {Flag}.", flagToBlockEventCheckPackageProgress);




                    // тут подписка блокируется навсегда




                    // дополнительно проверять ключ окончания пакета и по нему полностью отменить подписку
                }
            });

            string eventKeyCommand = $"Key = {tasksPackageGuidField}, Command = {constantsSet.EventCmd}";
            Logs.Here().Debug("You subscribed on EventSet. \n {@ES}", new { EventSet = eventKeyCommand });
        }

        private void SubscribeOnEventPackageCompleted(ConstantsSet constantsSet, string tasksPackageGuidField, CancellationToken stoppingToken)
        {
            // подписка на окончание единичной задачи (для проверки, все ли задачи закончились)
            _flagToBlockEventCompleted = true;
            string backServerPrefixGuid = constantsSet.BackServerPrefixGuid.Value;
            string prefixPackageCompleted = constantsSet.PrefixPackageCompleted.Value;
            string prefixCompletedTasksPackageGuid = $"{prefixPackageCompleted}:{tasksPackageGuidField}";
            Logs.Here().Information("BackServer subscribed on EventKey Server Guid. \n {@E}", new { EventKey = prefixCompletedTasksPackageGuid });

            _keyEvents.Subscribe(prefixCompletedTasksPackageGuid, (string key, KeyEvent cmd) => // async before action
            {
                if (cmd == constantsSet.EventCmd && _flagToBlockEventCompleted)
                {
                    _flagToBlockEventCompleted = false;
                    // параллельно заканчивается много пакетов и по каждому окончанию бегаем проверять новые пакеты, а они давно кончились
                    // не очень понятно, каким образом пакеты выполняются параллельно - в этом надо разобраться
                    
                    Logs.Here().Debug("SubscribeOnEventPackageCompleted was called with event ---current_package_finished---.");
                    _ = CheckKeyFrontGivesTask(stoppingToken, constantsSet);
                    Logs.Here().Debug("CheckKeyFrontGivesTask was called and passed.");
                }
            });

            string eventKeyCommand = $"Key = {prefixCompletedTasksPackageGuid}, Command = {constantsSet.EventCmd}";
            Logs.Here().Debug("You subscribed on EventSet. \n {@ES}", new { EventSet = eventKeyCommand });
        }

        public async Task ProcessingEventCompletedTask(ConstantsSet constantsSet)//, string tasksPackageGuidField)
        {
            //bool unsolvedTasksStillLeft = false;
            // пока активно событие подписки на окончание задачи, проверяем общее состояние пакета
            //while (_eventCompletedTaskWasHappening)
            //{
            //Logs.Here().Debug("Processing Event_Completed_Task is launched.");
            // признак, что ещё есть нерешённые задачи

            // перед проверкой готовности задач сбрасываем защёлку подписки
            //_eventCompletedTaskWasHappening = false;
            //Logs.Here().Debug("Flag Event_Completed_Task was happening was reset.");
            // задержку попробовать поставить здесь
            // await Task.Delay(TimeSpan.FromSeconds(0.001)); //add cancellationToken
            // если до/во время проверки произойдёт новое событие, то сделаем ещё круг с повторной проверкой
            // проверить значение в ключе сервера - если больше нуля, значит, ещё не закончено
            //string tasksPackageGuidField = _tasksPackageGuidField;
            //int totalUnsolvedTasksLeft;
            //(unsolvedTasksStillLeft, totalUnsolvedTasksLeft) = await _control.CheckingPackageCompletion(eventKeysSet, tasksPackageGuidField);
            // наверное лучше инвертировать название unsolvedTasksStillLeft и его состояние
            //Logs.Here().Debug("Flag Event_Completed_Task was happening now - {@F}.", new { Flag = _eventCompletedTaskWasHappening });

            // totalUnsolvedTasksLeft получаем только для отладки
            //Logs.Here().Debug("CheckingPackageCompletion returned {@P}, {@L}.", new { Permit = unsolvedTasksStillLeft }, new { TasksLeft = totalUnsolvedTasksLeft });
            // выход по окончанию всех задач можно перенести сюда
            //}
            //if (unsolvedTasksStillLeft)
            //{
            //    // если задачи ещё есть, завершить обработчик
            //    // обработчик завершает работу, сбросить флаг
            //    _processingEventCompletedTaskIsLaunched = false;
            //    Logs.Here().Debug("ProcessingEventCompletedTask finishes work {@P}, {@L}.", new { Permit = unsolvedTasksStillLeft }, new { IsLaunched = _processingEventCompletedTaskIsLaunched });
            //    return;
            //}

            // если все задачи кончились, 
            //_ = FreshTaskPackageHasAppeared(eventKeysSet);

            // обработчик завершает работу, сбросить флаг для будущих поколений
            //_processingEventCompletedTaskIsLaunched = false;
            //Logs.Here().Debug("SubscribeOnEventRun finished current package with the following flags: => \n {@F1} \n {@F2} \n {@F3}", new { FlagToBlockEventRun = _flagToBlockEventRun }, new { EventCompletedTaskWasHappening = _eventCompletedTaskWasHappening }, new { ProcessingEventCompletedTaskIsLaunched = _processingEventCompletedTaskIsLaunched });
        }

        // по ключу сервера можно дополнительно контролировать окончание пакета, если удалять поле пакета после его окончания (но как?)
        public void SubscribeOnEventServerGuid(ConstantsSet constantsSet) // NOT USED
        {
            string backServerPrefixGuid = constantsSet.BackServerPrefixGuid.Value;
            Logs.Here().Information("BackServer subscribed on {@E}.", new { EventKey = backServerPrefixGuid });


            // типовая блокировка множественной подписки до специального разрешения повторной подписки
            // здесь не надо блокировать - пока что
            //bool flagToBlockEventCheck = true;

            _keyEvents.Subscribe(backServerPrefixGuid, async (string key, KeyEvent cmd) =>
            {
                if (cmd == constantsSet.EventCmd) // && flagToBlockEventCheck)
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

            string eventKeyCommand = $"Key = {backServerPrefixGuid}, Command = {constantsSet.EventCmd}";
            Logs.Here().Debug("You subscribed on EventSet. \n {@ES}", new { EventSet = eventKeyCommand });
        }

    }
}
