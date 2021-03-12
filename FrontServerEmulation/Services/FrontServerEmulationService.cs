using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CachingFramework.Redis.Contracts.Providers;
using Microsoft.Extensions.Logging;
using Shared.Library.Models;

namespace FrontServerEmulation.Services
{
    public interface IFrontServerEmulationService
    {
        public Task FrontServerEmulationCreateGuidField(string eventKeyRun, string eventFieldRun, TimeSpan ttl);
        public Task<int> FrontServerEmulationMain(EventKeyNames eventKeysSet); // все ключи положить в константы
    }

    public class FrontServerEmulationService : IFrontServerEmulationService
    {
        private readonly ILogger<FrontServerEmulationService> _logger;
        private readonly ICacheProviderAsync _cache;

        public FrontServerEmulationService(ILogger<FrontServerEmulationService> logger, ICacheProviderAsync cache)
        {
            _logger = logger;
            _cache = cache;
        }

        public async Task FrontServerEmulationCreateGuidField(string eventKeyRun, string eventFieldRun, TimeSpan ttl) // not used
        {
            string eventGuidFieldRun = Guid.NewGuid().ToString(); // 

            await _cache.SetHashedAsync<string>(eventKeyRun, eventFieldRun, eventGuidFieldRun, ttl); // создаём ключ ("task:run"), на который подписана очередь и в значении передаём имя ключа, содержащего пакет задач

            _logger.LogInformation("Guid Field {0} for key {1} was created and set.", eventGuidFieldRun, eventKeyRun);
        }

        public async Task<int> FrontServerEmulationMain(EventKeyNames eventKeysSet) // _logger = 300
        {
            // получаем условия задач по стартовому ключу 
            int tasksPackagesCount = await FrontServerFetchConditions(eventKeysSet.EventKeyFrom, eventKeysSet.EventFieldFrom);

            // начинаем цикл создания и размещения пакетов задач
            _logger.LogInformation(30010, " - Creation cycle of key EventKeyFrontGivesTask fields started with {1} steps.", tasksPackagesCount);

            for (int i = 0; i < tasksPackagesCount; i++)
            {
                // guid - главный номер задания, используемый в дальнейшем для доступа к результатам
                string taskPackageGuid = Guid.NewGuid().ToString();
                int tasksCount = Math.Abs(taskPackageGuid.GetHashCode()) % 10; // просто (псевдо)случайное число
                if (tasksCount < 3)
                {
                    tasksCount += 3;
                }
                // создаём пакет задач (в реальности, опять же, пакет задач положил отдельный контроллер)
                Dictionary<string, TaskDescriptionAndProgress> taskPackage = FrontServerCreateTasks(tasksCount, eventKeysSet);

                // при создании пакета сначала создаётся пакет задач в ключе, а потом этот номер создаётся в виде поля в подписном ключе

                // создаем ключ taskPackageGuid и кладем в него пакет 
                // записываем ключ taskPackageGuid пакета задач в поле ключа eventKeyFrontGivesTask и в значение ключа - тоже taskPackageGuid
                // дополняем taskPackageGuid префиксом PrefixPackage
                string taskPackagePrefixGuid = $"{eventKeysSet.PrefixPackage}:{taskPackageGuid}";
                int inPackageTaskCount = await FrontServerSetTasks(taskPackage, eventKeysSet, taskPackagePrefixGuid);
                // можно возвращать количество созданных задач и проверять, что не нуль - но это чтобы хоть что-то проверять (или проверять наличие созданных ключей)
                // на создание ключа с пакетом задач уйдёт заметное время, поэтому промежуточный ключ оправдан (наверное)
            }
            return tasksPackagesCount;
        }

        private async Task<int> FrontServerFetchConditions(string eventKeyFrom, string eventFieldFrom)
        {
            //получить число пакетов задач (по этому ключу метод вызвали)
            int tasksCount = await _cache.GetHashedAsync<int>(eventKeyFrom, eventFieldFrom);

            _logger.LogInformation(30020, "TaskCount = {TasksCount} from key {Key} was fetched.", tasksCount, eventKeyFrom);

            if (tasksCount < 3) tasksCount = 3;
            if (tasksCount > 50) tasksCount = 50;

            return tasksCount;
        }

        private Dictionary<string, TaskDescriptionAndProgress> FrontServerCreateTasks(int tasksCount, EventKeyNames eventKeysSet)
        {
            Dictionary<string, TaskDescriptionAndProgress> taskPackage = new Dictionary<string, TaskDescriptionAndProgress>();

            for (int i = 0; i < tasksCount; i++)
            {
                string guid = Guid.NewGuid().ToString();

                // инициализовать весь класс отдельным методом
                // найти, как передать сюда TasksPackageGuid
                TaskDescriptionAndProgress descriptor = DescriptorInit(guid);

                int currentCycleCount = descriptor.TaskDescription.CycleCount;

                // дополняем taskPackageGuid префиксом PrefixPackage
                string taskPackagePrefixGuid = $"{eventKeysSet.PrefixTask}:{guid}";
                taskPackage.Add(taskPackagePrefixGuid, descriptor);

                _logger.LogInformation(30030, "Task {I} from {TasksCount} with ID {Guid} and {CycleCount} cycles was added to Dictionary.", i, tasksCount, taskPackagePrefixGuid, currentCycleCount);
                //_logger.LogInformation(30033, "TaskDescriptionAndProgress descriptor TaskCompletedOnPercent = {0}.", descriptor.TaskState.TaskCompletedOnPercent);
            }
            return taskPackage;
        }

        private TaskDescriptionAndProgress DescriptorInit(string guid)
        {
            TaskDescriptionAndProgress.TaskComplicatedDescription cycleCount = new()
            {
                TaskGuid = guid,
                CycleCount = Math.Abs(guid.GetHashCode()) % 10 // получать 10 из констант
            };

            TaskDescriptionAndProgress.TaskProgressState init = new()
            {
                IsTaskRunning = false,
                TaskCompletedOnPercent = -1
            };

            // получать max (3) из констант
            if (cycleCount.CycleCount < 3)
            {
                cycleCount.CycleCount += 3;
            }

            TaskDescriptionAndProgress descriptor = new()
            {
                TaskDescription = cycleCount,
                TaskState = init
            };

            return descriptor;
        }

        private async Task<int> FrontServerSetTasks(Dictionary<string, TaskDescriptionAndProgress> taskPackage, EventKeyNames eventKeysSet, string taskPackageGuid)
        {
            int inPackageTaskCount = 0;
            foreach (KeyValuePair<string, TaskDescriptionAndProgress> t in taskPackage)
            {
                (string guid, TaskDescriptionAndProgress cycleCount) = t;
                // записываем пакет задач в ключ пакета задач
                // потом здесь записывать в значение класс с условием и ходом выполнения задач
                // или условия и выполнение это разные ключи (префиксы)?
                await _cache.SetHashedAsync(taskPackageGuid, guid, cycleCount, TimeSpan.FromDays(eventKeysSet.EventKeyFromTimeDays));
                inPackageTaskCount++;
                _logger.LogInformation(30050, "TaskPackage No. {0}, with Task No. {1} with {2} cycles was set.", taskPackageGuid, guid, cycleCount);
            }

            // только после того, как создан ключ с пакетом задач, можно положить этот ключ в подписной ключ eventKeyFrontGivesTask
            // записываем ключ пакета задач в ключ eventKeyFrontGivesTask, а в поле и в значение - ключ пакета задач
            string eventKeyFrontGivesTask = eventKeysSet.EventKeyFrontGivesTask;

            await _cache.SetHashedAsync(eventKeysSet.EventKeyFrontGivesTask, taskPackageGuid, taskPackageGuid, TimeSpan.FromDays(eventKeysSet.EventKeyFrontGivesTaskTimeDays));
            // сервера подписаны на ключ eventKeyFrontGivesTask и пойдут забирать задачи, на этом тут всё
            return inPackageTaskCount;
        }
    }
}
