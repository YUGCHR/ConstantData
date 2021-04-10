using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CachingFramework.Redis.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CachingFramework.Redis.Contracts.Providers;
using Shared.Library.Services;
using BackgroundTasksQueue.Models;
using Shared.Library.Models;

namespace BackgroundTasksQueue.Services
{
    public interface IQueuedHostedService
    {
        public Task<int> AddCarrierProcesses(EventKeyNames eventKeysSet, CancellationToken stoppingToken, int requiredProcessesCountToAdd);

        public Task<int> CancelCarrierProcesses(EventKeyNames eventKeysSet, CancellationToken stoppingToken, int requiredProcessesCountToAdd);

        public Task<int> CarrierProcessesCount(EventKeyNames eventKeysSet, CancellationToken stoppingToken, int requiredProcessesCountToAdd);

    }

    public class QueuedHostedService : BackgroundService, IQueuedHostedService
    {
        //private readonly ILogger<QueuedHostedService> _logger;
        private readonly ISharedDataAccess _data;
        private readonly ICacheProviderAsync _cache;
        private readonly IKeyEventsProvider _keyEvents;
        private readonly IOnKeysEventsSubscribeService _subscribe;
        private readonly string _guid;

        // заменить List на ключ с полями
        List<BackgroundProcessingTask> completingTasksProcesses = new List<BackgroundProcessingTask>();

        public QueuedHostedService(
            GenerateThisInstanceGuidService thisGuid,
            IBackgroundTaskQueue taskQueue,
            //ILogger<QueuedHostedService> logger,
            ISharedDataAccess data,
            ICacheProviderAsync cache,
            IKeyEventsProvider keyEvents, IOnKeysEventsSubscribeService subscribe)
        {
            TaskQueue = taskQueue;
            //_logger = logger;
            _data = data;
            _cache = cache;
            _keyEvents = keyEvents;
            _subscribe = subscribe;

            _guid = thisGuid.ThisBackServerGuid();
        }

        private static Serilog.ILogger Logs => Serilog.Log.ForContext<QueuedHostedService>();

        public IBackgroundTaskQueue TaskQueue { get; }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Logs.Here().Information("Queued Hosted Service is running. BackgroundProcessing will be called now.");
            await BackgroundProcessing(stoppingToken);
        }

        private async Task BackgroundProcessing(CancellationToken stoppingToken)
        {
            // все проверки и ожидание внутри метода, без констант не вернётся
            // но можно проверять на null, если как-то null, то что-то сделать (shutdown)
            EventKeyNames eventKeysSet = await ConstantInitializer(stoppingToken);
            // все названия ключей из префиксов перенести в инициализацию

            _ = RunSubscribe(eventKeysSet, stoppingToken);

            // тут нужны 3 метода
            // создание процессов
            // проверка количества процессов
            // удаление процессов

            // вся регулировка должна быть внутри класса, внешний метод сообщает только необходимое ему количество процессов
            // всё же процессы это поля в ключе и сделать полностью изолированными - желательно вынести в отдельный класс
            // или можно сообщать количество задач в пакете, а класс сам решит, сколько надо/можно пакетов - перенести метод подсчёта сюда
            // отдельные методы добавления и убавления, подписки не надо
            // главный метод, который считает и решает, ему приходит вызов от подписки с ключа сервера про задачу
            // надо ли ждать, когда все задачи загрузятся? особого смысла нет
            //EventKeyNames eventKeysSet = await _data.FetchAllConstants(stoppingToken, 770);
            //string backServerGuid = _guid ?? throw new ArgumentNullException(nameof(_guid));
            //eventKeysSet.BackServerGuid = backServerGuid;
            //string backServerPrefixGuid = $"{eventKeysSet.PrefixBackServer}:{backServerGuid}";
            //eventKeysSet.BackServerPrefixGuid = backServerPrefixGuid;

            //string eventKey = "task:add";
            //string cancelKey = "task:del";
            //int createdProcessesCount = 0;
            //string backServerGuid = eventKeysSet.BackServerGuid;
            //string backServerPrefixGuid = eventKeysSet.BackServerPrefixGuid;
            //string backServerPrefixGuid = $"{eventKeysSet.PrefixBackServer}:{_guid}"; // backserver:(this server guid)
            //Logs.Here().Information("Server Guid was fetched in QueuedHostedService. \n {@S}", new { ServerId = backServerPrefixGuid });

            // создать ключ для подписки из констант
            //string prefixProcessAdd = eventKeysSet.PrefixProcessAdd; // process:add
            //string processAddPrefixGuid = $"{prefixProcessAdd}:{backServerGuid}"; // process:add:(this server guid)
            //string processAddPrefixGuid = eventKeysSet.ProcessAddPrefixGuid;
            //string processCancelPrefixGuid = eventKeysSet.ProcessCancelPrefixGuid;
            // поле-пустышка, но одинаковое с тем, что создаётся в основном методе - чтобы достать значение
            //string eventFieldBack = eventKeysSet.EventFieldBack;
            //Logs.Here().Debug("Creation of the processes was subscribed on necessary count. \n {@K} / {@F}", new { Key = processAddPrefixGuid }, new { Field = eventFieldBack });
            // подписка на ключ добавления бэкграунд процессов(поле без разницы), в значении можно было бы ставить количество необходимых процессов
            // типовая блокировка множественной подписки до специального разрешения повторной подписки
            //bool flagToBlockEventAdd = true;
            //_keyEvents.Subscribe(processAddPrefixGuid, async (string key, KeyEvent cmd) =>
            //{
            //    if (cmd == KeyEvent.HashSet && flagToBlockEventAdd)
            //    {
            //        // временная защёлка, чтобы подписка выполнялась один раз
            //        flagToBlockEventAdd = false;

            //        // название поля тоже можно создать здесь и передать в метод
            //        // ещё лучше - достать нужное значение заранее и передать только его, тогда метод будет синхронный (наверное)
            //        // не лучше
            //        // лучше
            //        int requiredProcessesCount = await _cache.GetHashedAsync<int>(processAddPrefixGuid, eventFieldBack);
            //        Logs.Here().Debug("requiredProcessesCount {0} was fetched, Subscribe permit = {1} \n {@K} with {@C} was received.", requiredProcessesCount, flagToBlockEventAdd, new { Key = processAddPrefixGuid }, new { Command = cmd });

            //        if (requiredProcessesCount > 0)
            //        {
            //            //createdProcessesCount = await CarrierProcessesManager(stoppingToken, requiredProcessesCount);
            //            Logs.Here().Debug("CarrierProcessesManager created processes count {0}.", createdProcessesCount);

            //            if (createdProcessesCount > 0)
            //            {
            //                flagToBlockEventAdd = true;
            //            }
            //        }
            //        // если вызвали с неправильным значением в ключе, подписка навсегда останется заблокированной, где-то тут ее надо разблокировать
            //    }
            //});

            //string eventKeyCommand = $"Key {processAddPrefixGuid}, HashSet command";
            //Logs.Here().Debug("You subscribed on EventSet. \n {@ES}", new { EventSet = eventKeyCommand });


            //_keyEvents.Subscribe(processCancelPrefixGuid, (string key, KeyEvent cmd) =>
            //{
            //    if (cmd == KeyEvent.HashSet)
            //    {
            //        Logs.Here().Debug("Event cancelKey was happened, Subscribe permit = none \n {@K} with {@C} was received.", new { Key = processCancelPrefixGuid }, new { Command = cmd });

            //        if (createdProcessesCount > 0)
            //        {
            //            // останавливаем процесс
            //            var cts = completingTasksProcesses[createdProcessesCount - 1].CancellationTaskToken;
            //            cts.Cancel();

            //            completingTasksProcesses.RemoveAt(createdProcessesCount - 1);
            //            createdProcessesCount--;
            //            Logs.Here().Debug("One Task for Background Processes was removed, total count left {Count}.", createdProcessesCount);
            //        }
            //        else
            //        {
            //            Logs.Here().Debug("Task for Background Processes cannot be removed for some reason, total count is {Count}.", createdProcessesCount);
            //        }
            //    }
            //});

            //List<Task> processingTask = completingTasksProcesses.Select(t => t.ProcessingTask).ToList();

            //await Task.WhenAll(processingTask);

            //Logs.Here().Debug("All Background Processes were finished, total count was {Count}", processingTask.Count);
        }

        // подписка на ключ process:add:(this server guid), который создаётся при получении нового пакета задач
        // поле используется стандартная пустышка, а в значении будет ключ пакета
        // ключ надо удалить после коррекции процессов или после окончания пакета

        // --- больше не надо сообщать резолверу ключ пакета через подписку!

        //public void SubscribeOnEventNEwPackage(EventKeyNames eventKeysSet, CancellationToken stoppingToken)
        //{


        //    _keyEvents.Subscribe(processAddPrefixGuid, async (string key, KeyEvent cmd) =>
        //    {
        //        // блокировать подписку нет смысла, новое событие будет нескоро
        //        // но можно контролировать его - если случится не вовремя, то что-то пошло не так
        //        if (cmd == KeyEvent.HashSet)// && flagToBlockEventAdd)
        //        {
        //            string tasksPackageGuidField = await _cache.GetHashedAsync<string>(processAddPrefixGuid, eventFieldBack);
        //            Logs.Here().Debug("New package event was fetched. \n {@P}", new { Package = tasksPackageGuidField });

        //            //_ = CarrierProcessesSolver(tasksPackageGuidField, eventKeysSet, stoppingToken);
        //            Logs.Here().Debug("CarrierProcessesSolver was called and passed.");
        //        }
        //    });
        //}

        public async Task<int> AddCarrierProcesses(EventKeyNames eventKeysSet, CancellationToken stoppingToken, int requiredProcessesCountToAdd)
        {
            // здесь requiredProcessesCountToAdd заведомо больше нуля
            string processCountPrefixGuid = eventKeysSet.ProcessCountPrefixGuid;
            string processAddPrefixGuid = eventKeysSet.ProcessAddPrefixGuid;
            string eventFieldBack = eventKeysSet.EventFieldBack;
            int addedProcessesCount = 0;

            // тут надо проверить существование ключа и поля 
            int totalProcessesCount = await _cache.GetHashedAsync<int>(processAddPrefixGuid, eventFieldBack);
            Logs.Here().Debug("CarrierProcesses addition is started, required additional count = {0}, total count was {1}.", requiredProcessesCountToAdd, totalProcessesCount);

            while (addedProcessesCount < requiredProcessesCountToAdd && !stoppingToken.IsCancellationRequested)
            {
                string guid = Guid.NewGuid().ToString();
                CancellationTokenSource newCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                CancellationToken newToken = newCts.Token;
                Logs.Here().Debug("CarrierProcessesManager creates process No {0}.", totalProcessesCount);

                // создаём новый процесс, к гуид можно добавить какой-то префикс
                BackgroundProcessingTask newlyAddedProcess = new BackgroundProcessingTask()
                {
                    // можно поставить глобальный счётчик процессов в классе - лучше счётчик в выделенном поле этого же ключа
                    TaskId = totalProcessesCount,
                    ProcessingTaskId = guid,
                    // запускаем новый процесс
                    ProcessingTask = Task.Run(() => ProcessingTaskMethod(newToken), newToken),
                    CancellationTaskToken = newCts
                };

                // записываем гуид в поле ключа всех процессов
                await _cache.SetHashedAsync<BackgroundProcessingTask>(processCountPrefixGuid, guid, newlyAddedProcess, TimeSpan.FromDays(eventKeysSet.EventKeyBackReadinessTimeDays));
                // новое значение общего количества процессов и номер для следующего
                totalProcessesCount++;
                // счётчик добавленных процессов для while
                addedProcessesCount++;
                // записываем обновлённое общее количество процессов в поле
                await _cache.SetHashedAsync<int>(processAddPrefixGuid, eventFieldBack, totalProcessesCount, TimeSpan.FromDays(eventKeysSet.EventKeyBackReadinessTimeDays));
                Logs.Here().Debug("New Task for Background Processes was added, total count became {0}.", totalProcessesCount);
            }

            int checkedProcessesCount = await _cache.GetHashedAsync<int>(processAddPrefixGuid, eventFieldBack);
            Logs.Here().Debug("New processes count was checked, count++ = {0}, total count = {1}.", totalProcessesCount, checkedProcessesCount);

            return checkedProcessesCount;
        }

        public async Task<int> CancelCarrierProcesses(EventKeyNames eventKeysSet, CancellationToken stoppingToken, int requiredProcessesCountToCancel)
        {
            // здесь requiredProcessesCountToCancel заведомо больше нуля
            string processCountPrefixGuid = eventKeysSet.ProcessCountPrefixGuid;
            string processAddPrefixGuid = eventKeysSet.ProcessAddPrefixGuid;
            string eventFieldBack = eventKeysSet.EventFieldBack;
            int removedProcessesCount = 0;

            // тут надо проверить существование ключа и поля 
            int totalProcessesCount = await _cache.GetHashedAsync<int>(processAddPrefixGuid, eventFieldBack);
            Logs.Here().Debug("CarrierProcesses removing is started, necessary excess count = {0}, total count was {1}.", requiredProcessesCountToCancel, totalProcessesCount);

            // всё же надо проверить, что удаляем меньше, чем существует

            if (removedProcessesCount < totalProcessesCount)
            {
                IDictionary<string, BackgroundProcessingTask> existedProcesses = await _cache.GetHashedAllAsync<BackgroundProcessingTask>(processCountPrefixGuid);
                int existedProcessesCount = existedProcesses.Count;
                Logs.Here().Debug("Dictionary with existing processes labels was fetched, count = {0}.", existedProcessesCount);

                // не foreach и не while, а for - раз заранее известно точное количество проходов
                for (int i = 0; i < requiredProcessesCountToCancel; i++)
                {
                    if (stoppingToken.IsCancellationRequested)
                    {
                        return 0;
                    }
                    // и доступ к элементу по индексу
                    // кстати, строковое значение тоже нужно - чтобы удалить поле процесса из ключа
                    (string processToBeDecimatedGuidField, BackgroundProcessingTask processToBeDecimated) = existedProcesses.ElementAt(i);
                    CancellationTokenSource cts = processToBeDecimated.CancellationTaskToken;
                    // сливаем процесс
                    cts.Cancel();
                    totalProcessesCount--;
                    bool isDeleteSuccess = await _cache.RemoveHashedAsync(processCountPrefixGuid, processToBeDecimatedGuidField);
                    Logs.Here().Debug("Process liable to removing was deleted - {@D}, Cycle {0} from {1}, processes left {2}.", new { WasDecimated = isDeleteSuccess }, i, requiredProcessesCountToCancel, totalProcessesCount);
                }

                // записываем обновлённое общее количество процессов в поле
                await _cache.SetHashedAsync<int>(processAddPrefixGuid, eventFieldBack, totalProcessesCount, TimeSpan.FromDays(eventKeysSet.EventKeyBackReadinessTimeDays));
                Logs.Here().Debug("Some Background Processes was deleted, \n total count was {0}, deletion request was {1}, now count became {0}.", existedProcessesCount, requiredProcessesCountToCancel, totalProcessesCount);

                // проверяем, сколько реально осталось ярлычков на ключе
                IDictionary<string, BackgroundProcessingTask> leftProcesses = await _cache.GetHashedAllAsync<BackgroundProcessingTask>(processCountPrefixGuid);
                int leftProcessesCount = leftProcesses.Count;
                Logs.Here().Debug("Actual Processes labels count is {0}.", leftProcessesCount);

                return leftProcessesCount;
            }

            return 0;
        }

        public async Task<int> CarrierProcessesCount(EventKeyNames eventKeysSet, CancellationToken stoppingToken, int requiredProcessesCountToAdd)
        {
            string processCountPrefixGuid = eventKeysSet.ProcessCountPrefixGuid;
            string processAddPrefixGuid = eventKeysSet.ProcessAddPrefixGuid;
            string eventFieldBack = eventKeysSet.EventFieldBack;

            int totalProcessesCount = await _cache.GetHashedAsync<int>(processAddPrefixGuid, eventFieldBack);
            Logs.Here().Debug("CarrierProcesses total count is {1}.", totalProcessesCount);

            // проверяем, сколько реально осталось ярлычков на ключе
            IDictionary<string, BackgroundProcessingTask> existedProcesses = await _cache.GetHashedAllAsync<BackgroundProcessingTask>(processCountPrefixGuid);
            int existedProcessesCount = existedProcesses.Count;
            Logs.Here().Debug("Actual Processes labels (guids) count is {0}.", existedProcessesCount);

            if (totalProcessesCount != existedProcessesCount)
            {
                Logs.Here().Error("Processes count failed, on-field value = {0}, labels-guid count = {1}.", totalProcessesCount, existedProcessesCount);
            }

            return existedProcessesCount;
        }

        private async Task ProcessingTaskMethod(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var workItem = await TaskQueue.DequeueAsync(token);

                try
                {
                    await workItem(token);
                }
                catch (Exception ex)
                {
                    Logs.Here().Error("Error occurred executing {0}.", ex);
                }
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            Logs.Here().Information("Queued Hosted Service is stopping.");
            await base.StopAsync(stoppingToken);
        }

        private async Task<EventKeyNames> ConstantInitializer(CancellationToken stoppingToken)
        {
            EventKeyNames eventKeysSet = await _data.FetchAllConstants(stoppingToken, 750);

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
            string eventFieldBack = eventKeysSet.EventFieldBack;
            // инициализовать поле общего количества процессов при подписке - можно перенести в инициализацию, set "CurrentProcessesCount" in constants
            await _cache.SetHashedAsync<int>(processAddPrefixGuid, eventFieldBack, 0, TimeSpan.FromDays(eventKeysSet.EventKeyBackReadinessTimeDays));


            Logs.Here().Information("Server Guid was fetched and stored into EventKeyNames. \n {@S}", new { ServerId = backServerPrefixGuid });
            return eventKeysSet;
        }

        private async Task RunSubscribe(EventKeyNames eventKeysSet, CancellationToken stoppingToken)
        {
            await _cache.SetHashedAsync<string>(eventKeysSet.EventKeyBackReadiness, eventKeysSet.BackServerPrefixGuid, eventKeysSet.BackServerGuid, TimeSpan.FromDays(eventKeysSet.EventKeyBackReadinessTimeDays));

            // подписываемся на ключ сообщения о появлении свободных задач
            _subscribe.SubscribeOnEventRun(eventKeysSet, stoppingToken);
        }
    }
}

