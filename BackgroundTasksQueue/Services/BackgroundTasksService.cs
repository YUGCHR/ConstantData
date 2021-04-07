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
        void StartWorkItem(EventKeyNames eventKeysSet, string tasksPackageGuidField, string singleTaskGuid, TaskDescriptionAndProgress assignmentTerms);

    }

    public class BackgroundTasksService : IBackgroundTasksService
    {
        private readonly IBackgroundTaskQueue _taskQueue;
        //private readonly ILogger<BackgroundTasksService> _logger;
        private readonly ICacheProviderAsync _cache;

        public BackgroundTasksService(
            IBackgroundTaskQueue taskQueue,
            //ILogger<BackgroundTasksService> logger,
            ICacheProviderAsync cache
        )
        {
            _taskQueue = taskQueue;
            //_logger = logger;
            _cache = cache;
        }

        private static Serilog.ILogger Logs => Serilog.Log.ForContext<BackgroundTasksService>();

        public void StartWorkItem(EventKeyNames eventKeysSet, string tasksPackageGuidField, string singleTaskGuid, TaskDescriptionAndProgress taskDescription)
        {
            Logs.Here().Debug("Single Task processing was started. \n {@P} \n {@S}", new { Package = tasksPackageGuidField }, new { Task = singleTaskGuid });
            // Enqueue a background work item
            _taskQueue.QueueBackgroundWorkItem(async token =>
            {
                // Simulate loopCount 3-second tasks to complete for each enqueued work item
                bool isTaskCompleted = await ActualTaskSolution(taskDescription, tasksPackageGuidField, singleTaskGuid, token);
                // если задача завершилась полностью, удалить поле регистрации из ключа сервера
                // пока (или совсем) не удаляем, а уменьшаем на единичку значение, пока не станет 0 - тогда выполнение пакета закончено
                bool isTaskFinished = await ActualTaskCompletion(eventKeysSet, isTaskCompleted, taskDescription, tasksPackageGuidField, singleTaskGuid, token);
            });
        }

        private async Task<bool> ActualTaskSolution(TaskDescriptionAndProgress taskDescription, string tasksPackageGuidField, string singleTaskGuid, CancellationToken cancellationToken)
        {
            int assignmentTerms = taskDescription.TaskDescription.CycleCount;
            double taskDelayTimeSpanFromMilliseconds = taskDescription.TaskDescription.TaskDelayTimeFromMilliSeconds / 1000D;
            int delayLoop = 0;
            int loopRemain = assignmentTerms;
            //var guid = Guid.NewGuid().ToString();

            Logs.Here().Debug("Queued Background Task is starting with {0} cycles. \n {@P} \n {@S}", assignmentTerms, new { Package = tasksPackageGuidField }, new { Task = singleTaskGuid });

            taskDescription.TaskState.IsTaskRunning = true;
            // заменить while на for в отдельном методе с выходом из цикла по условию и return
            // потом можно попробовать рекурсию
            while (!cancellationToken.IsCancellationRequested && delayLoop < assignmentTerms)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(taskDelayTimeSpanFromMilliseconds), cancellationToken);
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

                Logs.Here().Verbose("completionDouble {0}% = delayLoop {1} / assignmentTerms {2}, IsTaskRunning = {3}", completionDouble, delayLoop, assignmentTerms, taskDescription.TaskState.IsTaskRunning);

                // обновляем отчёт о прогрессе выполнения задания
                await _cache.SetHashedAsync(tasksPackageGuidField, singleTaskGuid, taskDescription); // TimeSpan.FromDays - !!!

                delayLoop++;
                Logs.Here().Verbose("Task {0} is running. Loop = {1} / Remaining = {2} - {3}%", singleTaskGuid, delayLoop, loopRemain, completionTaskPercentage);
            }
            // возвращаем true, если задача успешно завершилась
            // а если безуспешно, то вообще не возвращаемся (скорее всего)
            bool isTaskCompleted = delayLoop == assignmentTerms;
            Logs.Here().Debug("Background Task is completed. \n {@P} \n {@S} \n {@C}, {@R}, {@I}", new { Package = tasksPackageGuidField }, new { Task = singleTaskGuid }, new { CurrentState = delayLoop }, new { Remain = loopRemain }, new { TaskIsCompleted = isTaskCompleted });

            return isTaskCompleted;
        }

        private async Task<bool> ActualTaskCompletion(EventKeyNames eventKeysSet, bool isTaskCompleted, TaskDescriptionAndProgress taskDescription, string tasksPackageGuidField, string singleTaskGuid, CancellationToken cancellationToken)
        {
            string backServerPrefixGuid = eventKeysSet.BackServerPrefixGuid;
            string prefixPackageControl = eventKeysSet.PrefixPackageControl;
            string prefixPackageCompleted = eventKeysSet.PrefixPackageCompleted;
            Logs.Here().Debug("in PrefixPackageControl fetched {0}.", prefixPackageControl);

            // сюда попадаем только если isTaskCompleted true, поэтому if и передачу значения isTaskCompleted можно убрать
            if (isTaskCompleted)
            {
                // отдельные задачи ни в каком ключе, кроме ключа пакета, пока (или совсем) не регистрируем
                //bool isDeletedSuccess = await _cache.RemoveHashedAsync(backServerPrefixGuid, singleTaskGuid); //HashExistsAsync
                //_logger.LogInformation("Queued Background Task {Guid} is complete on Server No. {ServerNum} / isDeleteSuccess = {3}.", singleTaskGuid, backServerPrefixGuid, isDeletedSuccess);
                // тут записать в описание, что задача закончилась

                taskDescription.TaskState.IsTaskRunning = false;

                await _cache.SetHashedAsync(tasksPackageGuidField, singleTaskGuid, taskDescription); // TimeSpan.FromDays - in outside method

                // тут уменьшить на единичку значение ключа сервера и прочее пакета задач
                int oldValue = await _cache.GetHashedAsync<int>(backServerPrefixGuid, tasksPackageGuidField);
                int newValue = oldValue - 1;
                await _cache.SetHashedAsync(backServerPrefixGuid, tasksPackageGuidField, newValue); // TimeSpan.FromDays - in outside method
                
                // ещё можно при достижении нуля удалить поле пакета, а уже из этого делать выводы (это на потом)
                Logs.Here().Debug("One Task in the Package is completed, was = {0}, is = {1}. \n {@P} \n {@T}", oldValue, newValue, new{Package = tasksPackageGuidField}, new{Task = singleTaskGuid });

                string prefixControlTasksPackageGuid = $"{prefixPackageControl}:{tasksPackageGuidField}";
                int sequentialSingleTaskNumber = await _cache.GetHashedAsync<int>(prefixControlTasksPackageGuid, singleTaskGuid);
                Logs.Here().Debug("Completed Task {0} on the control package key. \n {@S} \n {@K}", sequentialSingleTaskNumber, new{SingleTask = singleTaskGuid }, new{ControlKey = prefixControlTasksPackageGuid });
                
                bool isDeleteSuccess = await _cache.RemoveHashedAsync(prefixControlTasksPackageGuid, singleTaskGuid);
                Logs.Here().Debug("Attempt to delete field was {0}. \n {@P} \n {@S}", isDeleteSuccess, new { Package = tasksPackageGuidField }, new { SingleTask = singleTaskGuid });

                if (isDeleteSuccess)
                {
                    bool isExistEventKeyFrontGivesTask = await _cache.KeyExistsAsync(prefixControlTasksPackageGuid);
                    Logs.Here().Debug("Check of control key existing was {0}. \n {@P} \n {@S}", isExistEventKeyFrontGivesTask, new { Package = tasksPackageGuidField }, new { SingleTask = singleTaskGuid });

                    if (isExistEventKeyFrontGivesTask)
                    {
                        Logs.Here().Debug("Completed Task {0} was not the last. \n {@P} \n {@S}", sequentialSingleTaskNumber, new { Package = tasksPackageGuidField }, new { SingleTask = singleTaskGuid });
                        return true;
                    }
                    // ключ исчез, значит задача была последняя и надо об этом сообщить
                    Logs.Here().Information("Completed Task {0} was the last. \n {@P} \n {@S}", sequentialSingleTaskNumber, new { Package = tasksPackageGuidField }, new { SingleTask = singleTaskGuid });

                    // вот здесь об этом и сообщаем
                    string prefixCompletedTasksPackageGuid = $"{prefixPackageCompleted}:{tasksPackageGuidField}";
                    await _cache.SetHashedAsync(prefixCompletedTasksPackageGuid, tasksPackageGuidField, sequentialSingleTaskNumber, TimeSpan.FromDays(eventKeysSet.EventKeyBackServerAuxiliaryTimeDays)); // lifetime!
                    Logs.Here().Information("Key was hashSet, Event was created. \n {@K} \n {@S}", new{KeyEvent = prefixCompletedTasksPackageGuid}, new { SingleTask = singleTaskGuid });

                    return true;
                }

                Logs.Here().Fatal("Something went wrong, it cannot be so");
                return true;
            }
            else
            {
                Logs.Here().Verbose("Task {0} is not completed", singleTaskGuid);

                // тут тоже что-то записать
                return false;
            }
        }
    }
}
