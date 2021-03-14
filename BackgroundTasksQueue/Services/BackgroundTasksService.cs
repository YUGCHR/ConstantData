using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CachingFramework.Redis.Contracts.Providers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Shared.Library.Models;

namespace BackgroundTasksQueue.Services
{
    public interface IBackgroundTasksService
    {
        void StartWorkItem(string backServerPrefixGuid, string tasksPackageGuidField, string singleTaskGuid, TaskDescriptionAndProgress assignmentTerms);

    }

    public class BackgroundTasksService : IBackgroundTasksService
    {
        private readonly IBackgroundTaskQueue _taskQueue;
        private readonly ILogger<BackgroundTasksService> _logger;
        private readonly ICacheProviderAsync _cache;

        public BackgroundTasksService(
            IBackgroundTaskQueue taskQueue,
            ILogger<BackgroundTasksService> logger,
            ICacheProviderAsync cache
        )
        {
            _taskQueue = taskQueue;
            _logger = logger;
            _cache = cache;
        }

        public void StartWorkItem(string backServerPrefixGuid, string tasksPackageGuidField, string singleTaskGuid, TaskDescriptionAndProgress taskDescription)
        {
            _logger.LogInformation(2100, "TaskDescriptionAndProgress taskDescription TaskCompletedOnPercent = {0}.", taskDescription.TaskState.TaskCompletedOnPercent);

            // Enqueue a background work item
            _taskQueue.QueueBackgroundWorkItem(async token =>
            {
                // Simulate loopCount 3-second tasks to complete for each enqueued work item
                bool isTaskCompleted = await ActualTaskSolution(taskDescription, tasksPackageGuidField, singleTaskGuid, token);
                // если задача завершилась полностью, удалить поле регистрации из ключа сервера
                bool isTaskFinished = await ActualTaskCompletion(isTaskCompleted, backServerPrefixGuid, taskDescription, tasksPackageGuidField, singleTaskGuid, token);
                
            });
        }

        private async Task<bool> ActualTaskSolution(TaskDescriptionAndProgress taskDescription, string tasksPackageGuidField, string singleTaskGuid, CancellationToken cancellationToken)
        {
            int assignmentTerms = taskDescription.TaskDescription.CycleCount;
            int delayLoop = 0;
            int loopRemain = assignmentTerms;
            //var guid = Guid.NewGuid().ToString();

            _logger.LogInformation(2101, "Queued Background Task {Guid} is starting.", singleTaskGuid);
            taskDescription.TaskState.IsTaskRunning = true;
            while (!cancellationToken.IsCancellationRequested && delayLoop < assignmentTerms)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Prevent throwing if the Delay is cancelled
                }
                // здесь записать в ключ ??? и поле ??? номер текущего цикла и всего циклов, а также время и так далее (потом)
                // рассмотреть два варианта - ключ - сервер, поле - пакет, а в значении указать номер конкретной задачи и прочее в модели
                // второй вариант - ключ - пакет, поле - задача, а в значении сразу проценты (int)
                // ключ - сервер не имеет большого смысла, пакет и так не потеряется, а искать его будут именно по номеру пакета, поэтому пока второй вариант
                loopRemain--;

                double completionDouble = delayLoop * 100D / assignmentTerms;
                int completionTaskPercentage = (int)completionDouble;
                taskDescription.TaskState.TaskCompletedOnPercent = completionTaskPercentage;

                _logger.LogInformation("completionDouble {0}% = delayLoop {1} / assignmentTerms {2}, IsTaskRunning = {3}", completionDouble, delayLoop, assignmentTerms, taskDescription.TaskState.IsTaskRunning);

                // обновляем отчёт о прогрессе выполнения задания
                await _cache.SetHashedAsync(tasksPackageGuidField, singleTaskGuid, taskDescription); // TimeSpan.FromDays - !!!

                delayLoop++;
                _logger.LogInformation("Task {0} is running. Loop = {1} / Remaining = {2} - {3}%", singleTaskGuid, delayLoop, loopRemain, completionTaskPercentage);
            }
            return delayLoop == assignmentTerms;
        }

        private async Task<bool> ActualTaskCompletion(bool isTaskRunning, string backServerPrefixGuid, TaskDescriptionAndProgress taskDescription, string tasksPackageGuidField, string singleTaskGuid, CancellationToken cancellationToken)
        {
            if (isTaskRunning)
            {
                bool isDeletedSuccess = await _cache.RemoveHashedAsync(backServerPrefixGuid, singleTaskGuid); //HashExistsAsync
                _logger.LogInformation("Queued Background Task {Guid} is complete on Server No. {ServerNum} / isDeleteSuccess = {3}.", singleTaskGuid, backServerPrefixGuid, isDeletedSuccess);
                // тут записать в описание, что задача закончилась
                _logger.LogInformation(" --- BEFORE - Task {0} finished. IsTaskRunning still = {1}", singleTaskGuid, taskDescription.TaskState.IsTaskRunning);

                taskDescription.TaskState.IsTaskRunning = false;
                _logger.LogInformation(" --- AFTER - Task {0} finished. IsTaskRunning = {1} yet", singleTaskGuid, taskDescription.TaskState.IsTaskRunning);

                await _cache.SetHashedAsync(tasksPackageGuidField, singleTaskGuid, taskDescription); // TimeSpan.FromDays - in outside method
                return true;
            }
            else
            {
                // тут тоже что-то записать
                return false;
            }
        }
    }
}
