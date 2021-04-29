using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CachingFramework.Redis.Contracts;
using CachingFramework.Redis.Contracts.Providers;
using Microsoft.Extensions.Hosting;
using Serilog;
using BackgroundTasksQueue.Services;
using Shared.Library.Services;
using Shared.Library.Models;

namespace BackgroundTasksQueue
{
    // оставлен про запас
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
            string packageSeparator = new('*', 80);
            Logs.Here().Debug("Is Cancellation Token obtained? - {@C} \n {1} \n", new { IsCancelled = _cancellationToken.IsCancellationRequested }, packageSeparator);
            return !_cancellationToken.IsCancellationRequested; // add special key from Redis?
        }
    }
}
