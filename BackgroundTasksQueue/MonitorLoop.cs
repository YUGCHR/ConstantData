using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CachingFramework.Redis.Contracts;
using CachingFramework.Redis.Contracts.Providers;
using Microsoft.Extensions.Hosting;
//using Microsoft.Extensions.Logging;
using Serilog;
using BackgroundTasksQueue.Services;
using Shared.Library.Services;
using Shared.Library.Models;

namespace BackgroundTasksQueue
{
    public class MonitorLoop
    {
        private readonly ILogger _logger;
        private readonly ISharedDataAccess _data;
        private readonly CancellationToken _cancellationToken;
        private readonly ICacheProviderAsync _cache;
        private readonly IOnKeysEventsSubscribeService _subscribe;
        private readonly string _guid;

        public MonitorLoop(
            GenerateThisInstanceGuidService thisGuid,
            ILogger logger,
            ISharedDataAccess data,
            ICacheProviderAsync cache,
            IHostApplicationLifetime applicationLifetime,
            IOnKeysEventsSubscribeService subscribe)
        {
            _logger = logger;
            _data = data;
            _cache = cache;
            _subscribe = subscribe;
            _cancellationToken = applicationLifetime.ApplicationStopping;
            _guid = thisGuid.ThisBackServerGuid();
        }
        
        private static Serilog.ILogger Logs => Serilog.Log.ForContext<MonitorLoop>();

        public void StartMonitorLoop()
        {
            string thisMethodName = System.Reflection.MethodBase.GetCurrentMethod()?.Name;// ?? UnknownMethod;
            Logs.Here().Debug("BackServer's MonitorLoop is starting.");

            // Run a console user input loop in a background thread
            Task.Run(Monitor, _cancellationToken);
        }

        public async Task Monitor()
        {
            // при старте проверить наличие ключа с константами и если его нет, затаиться в ожидании

            // концепция хищных бэк-серверов, борющихся за получение задач
            // контроллеров же в лесу (на фронте) много и желудей, то есть задач, у них тоже много
            // а несколько (много) серверов могут неспешно выполнять задачи из очереди в бэкграунде

            // собрать все константы в один класс
            //EventKeyNames eventKeysSet = InitialiseEventKeyNames();

            // разделить на Init, Register и Subscribe

            // можно получить имя текущего метода
            //string thisMethodName = System.Reflection.MethodBase.GetCurrentMethod()?.Name;// ?? UnknownMethod;
            // но используем другой способ - спросить (в extension), какой метод вызвал

            EventKeyNames eventKeysSet = await _data.FetchAllConstants(_cancellationToken, 750);
            
            if (eventKeysSet != null)
            {
                Logs.Here().Debug("EventKeyNames fetched constants in EventKeyNames - {@D}.", new { CycleDelay = eventKeysSet.TaskEmulatorDelayTimeInMilliSeconds });
                await RegisterAndSubscribe(eventKeysSet);
            }
            else
            {
                _logger.Error("eventKeysSet CANNOT be Init.");
            }

            // заменить на while(всегда) и проверять условие в теле - и вынести ожидание в отдельный метод - the same in Constants
            while (IsCancellationNotYet())
            {
                var keyStroke = Console.ReadKey();

                if (keyStroke.Key == ConsoleKey.W)
                {
                    Logs.Here().Information("ConsoleKey was received {@K}.", new {Key = keyStroke.Key});
                }
            }
            //Log.CloseAndFlush();
            Logs.Here().Warning("MonitorLoop was canceled by the Token.");
        }

        private bool IsCancellationNotYet()
        {
            Logs.Here().Debug("Is Cancellation Token obtained? - {@C}", new { IsCancelled = _cancellationToken.IsCancellationRequested });
            return !_cancellationToken.IsCancellationRequested; // add special key from Redis?
        }

        private async Task RegisterAndSubscribe(EventKeyNames eventKeysSet)
        {
            //string thisMethodName = System.Reflection.MethodBase.GetCurrentMethod()?.Name;// ?? UnknownMethod;
            // множественные контроллеры по каждому запросу (пользователей) создают очередь - каждый создаёт ключ, на который у back-servers подписка, в нём поле со своим номером, а в значении или имя ключа с заданием или само задание            
            // дальше бэк-сервера сами разбирают задания
            // бэк после старта кладёт в ключ ___ поле со своим сгенерированным guid для учета?
            // все бэк-сервера подписаны на базовый ключ и получив сообщение по подписке, стараются взять задание - у кого получилось удалить ключ, тот и взял

            //string test = ThisBackServerGuid.GetThisBackServerGuid(); // guid from static class
            // получаем уникальный номер этого сервера, сгенерированный при старте экземпляра сервера
            //string backServerGuid = $"{eventKeysSet.PrefixBackServer}:{_guid}"; // Guid.NewGuid()
            //EventId aaa = new EventId(222, "INIT");

            // наверное, нехорошо сохранять новые значения в константы, а как ещё?
            string backServerGuid = _guid ?? throw new ArgumentNullException(nameof(_guid));
            eventKeysSet.BackServerGuid = backServerGuid;
            string backServerPrefixGuid = $"{eventKeysSet.PrefixBackServer}:{backServerGuid}";
            eventKeysSet.BackServerPrefixGuid = backServerPrefixGuid;

            Logs.Here().Information("Server Guid was fetched and stored into EventKeyNames. \n {@S} \n", new { ServerId = backServerPrefixGuid });

            // в значение можно положить время создания сервера
            // проверить, что там за время на ключах, подумать, нужно ли разное время для разных ключей - скажем, кафе и регистрация серверов - день, пакет задач - час
            // регистрируем поле guid сервера на ключе регистрации серверов, а в значение кладём чистый гуид, без префикса
            await _cache.SetHashedAsync<string>(eventKeysSet.EventKeyBackReadiness, backServerPrefixGuid, backServerGuid, TimeSpan.FromDays(eventKeysSet.EventKeyBackReadinessTimeDays));
            // восстановить время жизни ключа регистрации сервера перед новой охотой - где и как?
            // при завершении сервера успеть удалить своё поле из ключа регистрации серверов - обработать cancellationToken

            // подписываемся на ключ сообщения о появлении свободных задач
            _subscribe.SubscribeOnEventRun(eventKeysSet);

            // слишком сложная цепочка guid
            // оставить в общем ключе задач только поле, известное контроллеру и в значении сразу положить сумму задачу в модели
            // первым полем в модели создать номер guid задачи - прямо в модели?
            // оставляем слишком много guid, но добавляем к ним префиксы, чтобы в логах было понятно, что за guid
            // key EventKeyFrontGivesTask, fields - request:guid (model property - PrefixRequest), values - package:guid (PrefixPackage)
            // key package:guid, fileds - task:guid (PrefixTask), values - models
            // key EventKeyBackReadiness, fields - back(server):guid (PrefixBackServer)
            // key EventKeyBacksTasksProceed, fields - request:guid (PrefixRequest), values - package:guid (PrefixPackage)
            // method to fetch package (returns dictionary) from request:guid

            // можно здесь (в while) ждать появления гуид пакета задач, чтобы подписаться на ход его выполнения
            // а можно подписаться на стандартный ключ появления пакета задач - общего для всех серверов, а потом проверять, что это событие на своём сервере
            // хотелось, чтобы вся подписка происходила из monitorLoop, но тут пока никак не узнать номера пакета
            // а если подписываться там, где становится известен номер, придётся перекрёстно подключать сервисы
        }
    }
}
