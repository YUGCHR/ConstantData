using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CachingFramework.Redis.Contracts.Providers;
using Microsoft.Extensions.Logging;
using Shared.Library.Models;

namespace BackgroundTasksQueue.Services
{
    public interface ITasksBatchProcessingService
    {
        public Task WhenTasksPackageWasCaptured(EventKeyNames eventKeysSet, string tasksPackageGuidField);
    }

    public class TasksBatchProcessingService : ITasksBatchProcessingService
    {
        private readonly IBackgroundTasksService _task2Queue;
        private readonly ILogger<TasksBatchProcessingService> _logger;
        private readonly ICacheProviderAsync _cache;

        public TasksBatchProcessingService(
            ILogger<TasksBatchProcessingService> logger,
            ICacheProviderAsync cache,
            IBackgroundTasksService task2Queue)
        {
            _task2Queue = task2Queue;
            _logger = logger;
            _cache = cache;
        }

        private static Serilog.ILogger Logs => Serilog.Log.ForContext<TasksBatchProcessingService>();

        public async Task WhenTasksPackageWasCaptured(EventKeyNames eventKeysSet, string tasksPackageGuidField) // Main for Processing
        {
            string backServerPrefixGuid = eventKeysSet.BackServerPrefixGuid;
            Logs.Here().Debug("This BackServer fetched Task Package successfully. \n {@P}", new { Package = tasksPackageGuidField });

            // регистрируем полученный пакет задач на ключе выполняемых/выполненных задач и на ключе сервера
            // ключ выполняемых задач надо переделать - в значении класть модель, в которой указан номер сервера и состояние задачи
            // скажем, List of TaskDescriptionAndProgress и в нём дополнительное поле номера сервера и состояния всего пакета

            // перенесли RegisterTasksPackageGuid после TasksFromKeysToQueue, чтобы получить taskPackageCount для регистрации 

            // тут подписаться (SubscribeOnEventCheck) на ключ пакета задач для контроля выполнения, но будет много событий
            // каждая задача будет записывать в этот ключ своё состояние каждый цикл - надо ли так делать?

            // и по завершению выполнения задач хорошо бы удалить процессы
            // нужен внутрисерверный ключ (константа), где каждый из сервисов (каждого) сервера может узнать номер сервера, на котором запущен - чтобы правильно подписаться на событие
            // сервера одинаковые и жёлуди у них тоже одинаковые, разница только в номере, который сервер генерирует при своём старте
            // вот этот номер нужен сервисам, чтобы подписаться на события своего сервера, а не соседнего                    

            // складываем задачи во внутреннюю очередь сервера
            // tasksPakageGuidValue больше не нужно передавать, вместо нее tasksPackageGuidField

            Logs.Here().Verbose("TasksFromKeysToQueue called.");
            int taskPackageCount = await TasksFromKeysToQueue(eventKeysSet, tasksPackageGuidField);
            Logs.Here().Verbose("TasksFromKeysToQueue finished with Task Count = {0}.", taskPackageCount);

            Logs.Here().Verbose("RegisterTasksPackageGuid called.");
            await RegisterTasksPackageGuid(eventKeysSet, tasksPackageGuidField, taskPackageCount);
            Logs.Here().Verbose("RegisterTasksPackageGuid finished.");

            // здесь подходящее место, чтобы определить количество процессов, выполняющих задачи из пакета - в зависимости от количества задач, но не более максимума из константы
            // PrefixProcessAdd - префикс ключа (+ backServerGuid) управления добавлением процессов
            // PrefixProcessCancel - префикс ключа (+ backServerGuid) управления удалением процессов
            // в значение положить требуемое количество процессов
            // имя поля должно быть общим для считывания значения
            // PrefixProcessCount - 
            // не забыть обнулить (или удалить) ключ после считывания и добавления процессов - можно и не удалять, всё равно, пока его не перепишут, он больше никого не интересует
            // можно в качестве поля использовать гуид пакета задач, но, наверное, это лишние сложности, всё равно процессы общие

            Logs.Here().Verbose("AddProcessesToPerformingTasks called.");
            int addProcessesCount = await AddProcessesToPerformingTasks(eventKeysSet, taskPackageCount);
            Logs.Here().Verbose("AddProcessesToPerformingTasks finished, return false from WhenTasksPackageWasCaptured.");

            // тут ждать, пока не будут посчитаны всё задачи пакета
            // теперь не будем ждать, вернём false и пусть ждут другую подписку, которая поменяет на true
            int completionPercentage = 1; // await CheckingAllTasksCompletion(tasksPackageGuidField);

            // тут удалить все процессы (потом)
            // процессы тоже не здесь удаляем - перенести их отсюда
            //int cancelExistingProcesses = await CancelExistingProcesses(eventKeysSet, addProcessesCount, completionPercentage);
            // выйти из цикла можем только когда не останется задач в ключе кафе
        }

        private async Task<bool> RegisterTasksPackageGuid(EventKeyNames eventKeysSet, string tasksPackageGuidField, int taskPackageCount)
        {
            string backServerPrefixGuid = eventKeysSet.BackServerPrefixGuid;
            //string eventKeyFrontGivesTask = eventKeysSet.EventKeyFrontGivesTask;
            string eventKeyBacksTasksProceed = eventKeysSet.EventKeyBacksTasksProceed;
            
            // следующие две регистрации пока непонятно, зачем нужны - доступ к состоянию пакета задач всё равно по ключу пакета
            // очень нужен - для контроля окончания выполнения задачи и пакета

            // регистрируем полученную задачу на ключе выполняемых/выполненных задач
            // поле - исходный ключ пакета (известный контроллеру, по нему он найдёт сервер, выполняющий его задание)
            // пока что поле задачи в кафе и ключ самой задачи совпадают, поэтому контроллер может напрямую читать состояние пакета задач по известному ему ключу
            // ключ выполняемых задач надо переделать - в значении класть модель, в которой указан номер сервера и состояние задачи
            // скажем, List of TaskDescriptionAndProgress и в нём дополнительное поле номера сервера и состояния всего пакета

            await _cache.SetHashedAsync(eventKeyBacksTasksProceed, tasksPackageGuidField, backServerPrefixGuid, TimeSpan.FromDays(eventKeysSet.EventKeyBackServerAuxiliaryTimeDays)); // lifetime!
            Logs.Here().Debug("Tasks package was registered. \n {@P} \n {@E}", new{Package = tasksPackageGuidField }, new{ EventKey = eventKeyBacksTasksProceed });

            // регистрируем исходный ключ и ключ пакета задач на ключе сервера - чтобы не разорвать цепочку
            // цепочка уже не актуальна, можно этот ключ использовать для контроля состояния пакета задач
            // для этого в дальнейшем в значение можно класть общее состояние всех задач пакета в процентах
            // или не потом, а сейчас класть 0 - тип значения менять нельзя
            // сейчас в значение кладём количество задач в пакете, а про мере выполнения вычитаем по единичке, чтобы как ноль - пакет выполнен
            int packageStateInit = taskPackageCount;
            await _cache.SetHashedAsync(backServerPrefixGuid, tasksPackageGuidField, packageStateInit, TimeSpan.FromDays(eventKeysSet.EventKeyBackServerAuxiliaryTimeDays)); // lifetime!
            Logs.Here().Verbose("This BackServer registered task package and RegisterTasksPackageGuid returned true.");

            return true;
        }

        private async Task<int> AddProcessesToPerformingTasks(EventKeyNames eventKeysSet, int taskPackageCount)
        {
            string backServerPrefixGuid = eventKeysSet.BackServerPrefixGuid;
            string backServerGuid = eventKeysSet.BackServerGuid;
            string prefixProcessAdd = eventKeysSet.PrefixProcessAdd;
            string eventFieldBack = eventKeysSet.EventFieldBack;
            string eventKeyProcessAdd = $"{prefixProcessAdd}:{backServerGuid}"; // process:add:(this server guid) 

            // вычисляем нужное количество процессов - перенести вызов в основной поток для наглядности?
            int toAddProcessesCount = CalcAddProcessesCount(eventKeysSet, taskPackageCount);

            // создаём ключ добавления процессов и в значении нужное количество процессов
            await _cache.SetHashedAsync(eventKeyProcessAdd, eventFieldBack, toAddProcessesCount); // TimeSpan.FromDays - !!!
            Logs.Here().Information("This BackServer ask to start {0} processes. \n {@K} / {@F}", toAddProcessesCount, new { Key = eventKeyProcessAdd }, new { Field = eventFieldBack });
            return toAddProcessesCount;
        }

        private async Task<int> TasksFromKeysToQueue(EventKeyNames eventKeysSet, string tasksPackageGuidField)
        {
            IDictionary<string, TaskDescriptionAndProgress> taskPackage = await _cache.GetHashedAllAsync<TaskDescriptionAndProgress>(tasksPackageGuidField); // получили пакет заданий - id задачи и данные (int) для неё
            int taskPackageCount = taskPackage.Count;
            int sequentialSingleTaskNumber = 0;
            foreach (var t in taskPackage)
            {
                var (singleTaskGuid, taskDescription) = t;

                // регистрируем задачи на ключе контроля выполнения пакета (prefixControlTasksPackageGuid)
                string prefixControlTasksPackageGuid = $"{eventKeysSet.PrefixPackageControl}:{tasksPackageGuidField}";
                await _cache.SetHashedAsync(prefixControlTasksPackageGuid, singleTaskGuid, sequentialSingleTaskNumber, TimeSpan.FromDays(eventKeysSet.EventKeyBackServerAuxiliaryTimeDays)); // lifetime!
                Logs.Here().Debug("Single task {0} was registered on Completed Control Key. \n {@P} \n {@S}", sequentialSingleTaskNumber, new { PackageControl = prefixControlTasksPackageGuid }, new { SingleTask = singleTaskGuid });
                sequentialSingleTaskNumber++;

                // складываем задачи во внутреннюю очередь сервера
                _task2Queue.StartWorkItem(eventKeysSet, tasksPackageGuidField, singleTaskGuid, taskDescription);
                // создаём ключ для контроля выполнения задания из пакета - нет, создаём не тут и не такой (ключ)
                //await _cache.SetHashedAsync(backServerPrefixGuid, singleTaskGuid, assignmentTerms); 
                Logs.Here().Verbose("This BackServer sent Task to Queue. \n {@T}", new { Task = singleTaskGuid }, new { CyclesCount = taskDescription.TaskDescription.CycleCount });
            }
            Logs.Here().Verbose("This BackServer sent total {0} tasks to Queue.", taskPackageCount);
            return taskPackageCount;
        }

        private int CalcAddProcessesCount(EventKeyNames eventKeysSet, int taskPackageCount)
        {
            int balanceOfTasksAndProcesses = eventKeysSet.BalanceOfTasksAndProcesses;
            int maxProcessesCountOnServer = eventKeysSet.MaxProcessesCountOnServer;
            int toAddProcessesCount;

            switch (balanceOfTasksAndProcesses)
            {
                // 0 - автовыбор - создаём процессов по числу задач
                case 0:
                    toAddProcessesCount = taskPackageCount;
                    return toAddProcessesCount;
                // больше нуля - основной вариант - делим количество задач на эту константу и если она больше максимума, берём константу максимума
                case > 0:
                    int multiplier = 10000; // from constants
                    toAddProcessesCount = (taskPackageCount * multiplier / balanceOfTasksAndProcesses) / multiplier;
                    // если константа максимума неправильная - 0 или отрицательная, игнорируем ее
                    if (toAddProcessesCount > maxProcessesCountOnServer && maxProcessesCountOnServer > 0)
                    {
                        toAddProcessesCount = maxProcessesCountOnServer;
                    }
                    if (toAddProcessesCount < 1)
                    { toAddProcessesCount = 1; }
                    return toAddProcessesCount;
                // меньше нуля - тайный вариант для настройки - количество процессов равно константе (с обратным знаком, естественно)
                case < 0:
                    toAddProcessesCount = balanceOfTasksAndProcesses * -1;
                    Logs.Here().Information("CalcAddProcessesCount calculated total {1} processes are necessary.", toAddProcessesCount);
                    return toAddProcessesCount;
            }
        }

        private async Task<int> CancelExistingProcesses(EventKeyNames eventKeysSet, int toCancelProcessesCount, int completionPercentage)
        {
            string backServerPrefixGuid = eventKeysSet.BackServerPrefixGuid;
            string backServerGuid = eventKeysSet.BackServerGuid;
            string prefixProcessCancel = eventKeysSet.PrefixProcessCancel;
            string eventFieldBack = eventKeysSet.EventFieldBack;
            string eventKeyProcessCancel = $"{prefixProcessCancel}:{backServerGuid}"; // process:cancel:(this server guid)

            int cancelExistingProcesses = 0;

            // создаём ключ удаления процессов и в значении нужное количество процессов
            await _cache.SetHashedAsync(eventKeyProcessCancel, eventFieldBack, toCancelProcessesCount); // TimeSpan.FromDays - !!!
            Logs.Here().Information("This BackServer ask to CANCEL {0} processes. \n {@K} / {@F}", toCancelProcessesCount, new { Key = eventKeyProcessCancel }, new { Field = eventFieldBack });

            return cancelExistingProcesses;
        }
    }
}
