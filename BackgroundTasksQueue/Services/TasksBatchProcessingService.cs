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
        public Task<bool> WhenTasksPackageWasCaptured(EventKeyNames eventKeysSet, string tasksPackageGuidField);
        public Task<bool> CheckingAllTasksCompletion(EventKeyNames eventKeysSet);
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

        public async Task<bool> WhenTasksPackageWasCaptured(EventKeyNames eventKeysSet, string tasksPackageGuidField) // Main for Processing
        {
            string backServerPrefixGuid = eventKeysSet.BackServerPrefixGuid;
            string eventKeyFrontGivesTask = eventKeysSet.EventKeyFrontGivesTask;
            string eventKeyBacksTasksProceed = eventKeysSet.EventKeyBacksTasksProceed;

            _logger.LogInformation(421, "This BackServer fetched taskPackageKey {1} successfully.", tasksPackageGuidField); // победитель по жизни
                                                                                                                            
            // следующие две регистрации пока непонятно, зачем нужны - доступ к состоянию пакета задач всё равно по ключу пакета

            // регистрируем полученную задачу на ключе выполняемых/выполненных задач
            // поле - исходный ключ пакета (известный контроллеру, по нему он найдёт сервер, выполняющий его задание)
            // пока что поле задачи в кафе и ключ самой задачи совпадают, поэтому контроллер может напрямую читать состояние пакета задач по известному ему ключу
            await _cache.SetHashedAsync(eventKeyBacksTasksProceed, tasksPackageGuidField, backServerPrefixGuid, TimeSpan.FromDays(eventKeysSet.EventKeyBackServerAuxiliaryTimeDays)); // lifetime!
            _logger.LogInformation(431, "Tasks package was registered on key {0} - \n      with source package key {1}.", eventKeyBacksTasksProceed, tasksPackageGuidField);

            
            // регистрируем исходный ключ и ключ пакета задач на ключе сервера - чтобы не разорвать цепочку
            // цепочка уже не актуальна, можно этот ключ использовать для контроля состояния пакета задач
            // для этого в дальнейшем в значение можно класть общее состояние всех задач пакета в процентах
            // или не потом, а сейчас класть 0 - тип значения менять нельзя
            int packageStateInit = -1; // value in percentages, but have set special value for newly created field now
            await _cache.SetHashedAsync(backServerPrefixGuid, tasksPackageGuidField, packageStateInit, TimeSpan.FromDays(eventKeysSet.EventKeyBackServerAuxiliaryTimeDays)); // lifetime!
            _logger.LogInformation(441, "This BackServer registered tasks package - \n      with source package key {1}.", tasksPackageGuidField);


            // тут подписаться (SubscribeOnEventCheck) на ключ пакета задач для контроля выполнения, но будет много событий
            // каждая задача будет записывать в этот ключ своё состояние каждый цикл - надо ли так делать?


            // и по завершению выполнения задач хорошо бы удалить процессы
            // нужен внутрисерверный ключ (константа), где каждый из сервисов (каждого) сервера может узнать номер сервера, на котором запущен - чтобы правильно подписаться на событие
            // сервера одинаковые и жёлуди у них тоже одинаковые, разница только в номере, который сервер генерирует при своём старте
            // вот этот номер нужен сервисам, чтобы подписаться на события своего сервера, а не соседнего                    

            // складываем задачи во внутреннюю очередь сервера
            // tasksPakageGuidValue больше не нужно передавать, вместо нее tasksPackageGuidField
            int taskPackageCount = await TasksFromKeysToQueue(tasksPackageGuidField, backServerPrefixGuid);

            // здесь подходящее место, чтобы определить количество процессов, выполняющих задачи из пакета - в зависимости от количества задач, но не более максимума из константы
            // PrefixProcessAdd - префикс ключа (+ backServerGuid) управления добавлением процессов
            // PrefixProcessCancel - префикс ключа (+ backServerGuid) управления удалением процессов
            // в значение положить требуемое количество процессов
            // имя поля должно быть общим для считывания значения
            // PrefixProcessCount - 
            // не забыть обнулить (или удалить) ключ после считывания и добавления процессов - можно и не удалять, всё равно, пока его не перепишут, он больше никого не интересует
            // можно в качестве поля использовать гуид пакета задач, но, наверное, это лишние сложности, всё равно процессы общие
            int addProcessesCount = await AddProcessesToPerformingTasks(eventKeysSet, taskPackageCount);



            // тут ждать, пока не будут посчитаны всё задачи пакета
            // теперь не будем ждать, вернём false и пусть ждут другую подписку, которая поменяет на true
            int completionPercentage = 1; // await CheckingAllTasksCompletion(tasksPackageGuidField);

            // если проценты не сто, то какая-то задача осталась невыполненной, надо сообщить на подписку диспетчеру (потом)
            // сообщать тоже не здесь будет, отсюда сейчас уходим
            int hundredPercents = 100; // from constants
            if (completionPercentage < hundredPercents)
            {
                await _cache.SetHashedAsync("dispatcherSubscribe:thisServerGuid", "thisTasksPackageKey", completionPercentage); // TimeSpan.FromDays - !!! как-то так
            }
            // тут удалить все процессы (потом)
            // процессы тоже не здесь удаляем - перенести их отсюда
            int cancelExistingProcesses = await CancelExistingProcesses(eventKeysSet, addProcessesCount, completionPercentage);
            // выйти из цикла можем только когда не останется задач в ключе кафе

            // здесь всё сделали, выходим с блокировкой подписки на следующие пакеты задач
            return false;
        }

        public async Task<bool> CheckingAllTasksCompletion(EventKeyNames eventKeysSet) // Main for Check
        {
            // ----------------- вы находитесь здесь


            // подписку оформить в отдельном методе, а этот вызывать оттуда
            // можно ставить блокировку на подписку и не отвлекаться на события, пока не закончена очередная проверка

            return default;
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

            _logger.LogInformation(518, "This BackServer ask to start {0} processes, key = {1}, field = {2}.", toAddProcessesCount, eventKeyProcessAdd, eventFieldBack);
            return toAddProcessesCount;
        }

        private async Task<int> TasksFromKeysToQueue(string tasksPackageGuidField, string backServerPrefixGuid)
        {
            IDictionary<string, int> taskPackage = await _cache.GetHashedAllAsync<int>(tasksPackageGuidField); // получили пакет заданий - id задачи и данные (int) для неё
            int taskPackageCount = taskPackage.Count;
            foreach (var t in taskPackage)
            {
                var (singleTaskGuid, assignmentTerms) = t;
                // складываем задачи во внутреннюю очередь сервера
                _task2Queue.StartWorkItem(backServerPrefixGuid, tasksPackageGuidField, singleTaskGuid, assignmentTerms);
                // создаём ключ для контроля выполнения задания из пакета - нет, создаём не тут и не такой (ключ)
                //await _cache.SetHashedAsync(backServerPrefixGuid, singleTaskGuid, assignmentTerms); 
                _logger.LogInformation(501, "This BackServer sent Task with ID {1} and {2} cycles to Queue.", singleTaskGuid, assignmentTerms);
            }

            _logger.LogInformation(511, "This BackServer sent total {1} tasks to Queue.", taskPackageCount);
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
                    _logger.LogInformation(517, "CalcAddProcessesCount calculated total {1} processes are necessary.", toAddProcessesCount);
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
            _logger.LogInformation(519, "This BackServer ask to CANCEL {0} processes, key = {1}, field = {2}.", toCancelProcessesCount, eventKeyProcessCancel, eventFieldBack);

            return cancelExistingProcesses;
        }
    }
}
