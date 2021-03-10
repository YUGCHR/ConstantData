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
    public interface ITasksPackageCaptureService
    {
        public Task<string> AttemptToCaptureTasksPackage(EventKeyNames eventKeysSet);
    }

    public class TasksPackageCaptureService : ITasksPackageCaptureService
    {
        private readonly IBackgroundTasksService _task2Queue;
        private readonly ILogger<TasksPackageCaptureService> _logger;
        private readonly ICacheProviderAsync _cache;

        public TasksPackageCaptureService(
            ILogger<TasksPackageCaptureService> logger,
            ICacheProviderAsync cache,
            IBackgroundTasksService task2Queue)
        {
            _task2Queue = task2Queue;
            _logger = logger;
            _cache = cache;
        }

        public async Task<string> AttemptToCaptureTasksPackage(EventKeyNames eventKeysSet) // Main for Capture
        {
            string backServerPrefixGuid = eventKeysSet.BackServerPrefixGuid;
            string eventKeyFrontGivesTask = eventKeysSet.EventKeyFrontGivesTask;
            string eventKeyBacksTasksProceed = eventKeysSet.EventKeyBacksTasksProceed;
            _logger.LogInformation(401, "This BackServer {0} started AttemptToCaptureTasksPackage.", backServerPrefixGuid);
            
            // начало главного цикла сразу после срабатывания подписки, условие - пока существует ключ распределения задач
            // считать пакет полей из ключа, если задач больше одной, бросить кубик
            // проверить захват задачи, если получилось - выполнять, нет - вернулись на начало главного цикла
            // выполнение - в отдельном методе, достать по ключу задачи весь пакет
            // определить, сколько надо процессов - количество задач в пакете разделить на константу, не менее одного и не более константы
            // запустить процессы в отдельном методе, сложить количество в ключ пакета
            // достать задачи из пакета и запустить их в очередь
            // следующим методом висеть и контролировать ход выполнения всех задач - подписаться на их ключи, собирать ход выполнения каждой, суммировать и складывать общий процент в ключ сервера
            // по окончанию всех задач удалить все процессы?
            // вернуться на начало главного цикла
            bool isExistEventKeyFrontGivesTask = true;

            // нет смысла проверять isDeleteSuccess, достаточно существования ключа задач - есть он, ловим задачи, нет его - возвращаемся
            while (isExistEventKeyFrontGivesTask) // может и надо поставить while всегда - все условия выхода внутри и по ним будет р
            {
                // проверить существование ключа, может, все задачи давно разобрали и ключ исчез
                isExistEventKeyFrontGivesTask = await _cache.KeyExistsAsync(eventKeyFrontGivesTask);
                _logger.LogInformation(402, "isExistEventKeyFrontGivesTask = {1}.", isExistEventKeyFrontGivesTask);
                
                if (!isExistEventKeyFrontGivesTask)
                // если ключа нет, тогда возвращаемся в состояние подписки на ключ кафе и ожидания события по этой подписке                
                { return null; } // задача не досталась

                // после сообщения подписки об обновлении ключа, достаём список свободных задач
                // список получается неполный! - оказывается, потому, что фронт не успеваем залить остальные поля, когда бэк с первым полем уже здесь
                IDictionary<string, string> tasksList = await _cache.GetHashedAllAsync<string>(eventKeyFrontGivesTask);
                int tasksListCount = tasksList.Count;
                _logger.LogInformation(403, "TasksList fetched - tasks count = {1}.", tasksListCount);

                // временный костыль - 0 - это задач в ключе не осталось - возможно, только что (перед носом) забрали последнюю
                if (tasksListCount == 0)
                // тогда возвращаемся в состояние подписки на ключ кафе и ожидания события по этой подписке
                // поскольку тогда следить за выполнением не надо, возвращаем null - и где-то его заменят на true
                { return null; } // задача не досталась

                // выбираем случайное поле пакета задач - скорее всего, первая попытка будет только с одним полем, остальные не успеют положить и будет драка, но на второй попытке уже разойдутся по разным полям
                (string tasksPackageGuidField, string tasksPackageGuidValue) = tasksList.ElementAt(DiceRoll(tasksListCount));

                // проверяем захват задачи - пробуем удалить выбранное поле ключа                
                // в дальнейшем можно вместо Remove использовать RedLock
                bool isDeleteSuccess = await _cache.RemoveHashedAsync(eventKeyFrontGivesTask, tasksPackageGuidField);
                // здесь может разорваться цепочка между ключом, который известен контроллеру и ключом пакета задач
                _logger.LogInformation(411, "This BackServer reported - isDeleteSuccess = {1}.", isDeleteSuccess);

                if (isDeleteSuccess)
                {
                    // тут перейти в TasksBatchProcessingService
                    // не перейти, а вернуться в подписку с номером пакета задач

                    return tasksPackageGuidField;
                }
            }

            // пока что сюда никак попасть не может, но надо предусмотреть, что все задачи исчерпались, а никого не поймали
            // скажем, ключ вообще исчез и ловить больше нечего
            // теперь сюда попадём, если ключ eventKeyFrontGivesTask исчез и задачу не захватить
            // надо сделать возврат в исходное состояние ожидания вброса ключа
            // побочный эффект - можно смело брать последнюю задачу и не опасаться, что ключ eventKeyFrontGivesTask исчезнет
            _logger.LogInformation(481, "This BackServer cannot catch the task.");

            // возвращаемся в состояние подписки на ключ кафе и ожидания события по этой подписке
            _logger.LogInformation(491, "This BackServer goes over to the subscribe event awaiting.");
            // восстанавливаем условие разрешения обработки подписки
            return  null; // задача не досталась
        }
        
        private int DiceRoll(int tasksListCount)
        {
            // generate random integers from 0 to guids count
            Random rand = new();
            // индекс словаря по умолчанию
            int diceRoll = tasksListCount - 1;
            // если осталась одна задача, кубик бросать не надо
            if (tasksListCount > 1)
            {
                diceRoll = rand.Next(0, tasksListCount - 1);
            }
            _logger.LogInformation(407, "DiceRoll rolled {1}.", diceRoll);
            return diceRoll;
        }
    }
}
