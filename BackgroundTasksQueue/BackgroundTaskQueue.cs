using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BackgroundTasksQueue.Models;
using CachingFramework.Redis.Contracts.Providers;
using Microsoft.Extensions.Logging;
using Shared.Library.Models;

namespace BackgroundTasksQueue
{
    public interface IBackgroundTaskQueue
    {
        void QueueBackgroundWorkItem(Func<CancellationToken, Task> workItem);

        Task<Func<CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken);

        public Task<int> AddCarrierProcesses(EventKeyNames eventKeysSet, CancellationToken stoppingToken, int requiredProcessesCountToAdd);

        public Task<int> CancelCarrierProcesses(EventKeyNames eventKeysSet, CancellationToken stoppingToken, int requiredProcessesCountToAdd);

        public Task<int> CarrierProcessesCount(EventKeyNames eventKeysSet, int requiredProcessesCountToAdd);
    }

    public class BackgroundTaskQueue : IBackgroundTaskQueue
    {
        private readonly ILogger<BackgroundTaskQueue> _logger;
        private readonly ICacheProviderAsync _cache;
        private ConcurrentQueue<Func<CancellationToken, Task>> _workItems = new ConcurrentQueue<Func<CancellationToken, Task>>();
        private SemaphoreSlim _signal = new SemaphoreSlim(0);

        List<BackgroundProcessingTask> _completingTasksProcesses = new List<BackgroundProcessingTask>();

        public BackgroundTaskQueue(
            ILogger<BackgroundTaskQueue> logger, 
            ICacheProviderAsync cache)
        {
            _logger = logger;
            _cache = cache;
        }

        private static Serilog.ILogger Logs => Serilog.Log.ForContext<BackgroundTaskQueue>();

        public void QueueBackgroundWorkItem(Func<CancellationToken, Task> workItem)
        {
            if (workItem == null)
            {
                throw new ArgumentNullException(nameof(workItem));
            }
            // Adds an object to the end of the System.Collections.Concurrent.ConcurrentQueue`1
            _workItems.Enqueue(workItem);
            Logs.Here().Verbose("Single Task placed in Concurrent Queue.");

            _signal.Release();
            Logs.Here().Verbose("Concurrent Queue is ready for new task.");

        }

        public async Task<Func<CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken)
        {
            await _signal.WaitAsync(cancellationToken);
            _workItems.TryDequeue(out var workItem);
            Logs.Here().Verbose("Single Task was dequeued from Concurrent Queue.");
            return workItem;
        }

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

        public async Task<int> CarrierProcessesCount(EventKeyNames eventKeysSet, int requiredProcessesCountToAdd)
        {
            string processCountPrefixGuid = eventKeysSet.ProcessCountPrefixGuid;
            string processAddPrefixGuid = eventKeysSet.ProcessAddPrefixGuid;
            string eventFieldBack = eventKeysSet.EventFieldBack;

            int totalProcessesCount = await _cache.GetHashedAsync<int>(processAddPrefixGuid, eventFieldBack);
            Logs.Here().Debug("CarrierProcesses total count is {0}.", totalProcessesCount);

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
                var workItem = await DequeueAsync(token); // was TaskQueue

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

    }
}

