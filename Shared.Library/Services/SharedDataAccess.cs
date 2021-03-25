using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using CachingFramework.Redis.Contracts;
using CachingFramework.Redis.Contracts.Providers;
using Microsoft.Extensions.Logging;
using Shared.Library.Models;

namespace Shared.Library.Services
{
    public interface ISharedDataAccess
    {
        public (string, string) FetchBaseConstants([CallerMemberName] string currentMethodNameName = "");
        public Task<EventKeyNames> FetchAllConstants(CancellationToken cancellationToken, int loggerIndexBase);
    }

    public class SharedDataAccess : ISharedDataAccess
    {

        private readonly ILogger<SharedDataAccess> _logger;
        private readonly ICacheProviderAsync _cache;
        private readonly IKeyEventsProvider _keyEvents;

        public SharedDataAccess(
            ILogger<SharedDataAccess> logger,
            ICacheProviderAsync cache,
            IKeyEventsProvider keyEvents)
        {
            _logger = logger;
            _cache = cache;
            _keyEvents = keyEvents;
        }

        private const int IndexBaseValue = 400 * 1000;

        private const string StartConstantKey = "constants";
        private const string StartConstantField = "all";
        private const KeyEvent SubscribedKeyEvent = KeyEvent.HashSet;
        private bool _allConstantsAppeared = false;

        public (string, string) FetchBaseConstants([CallerMemberName] string currentMethodNameName = "") // May be will cause problem with Docker
        {
            // if problem with Docker can use token
            if (currentMethodNameName == "ConstantsMountingMonitor") return (StartConstantKey, StartConstantField);
            _logger.LogError(IndexBaseValue + 070, "FetchBaseConstants was called by wrong method - {0}.", currentMethodNameName);
            return (null, null);
        }

        public async Task<EventKeyNames> FetchAllConstants(CancellationToken cancellationToken, int loggerIndexBase)
        {
            // проверить, есть ли ключ
            bool isExistEventKeyFrontGivesTask = await _cache.KeyExistsAsync(StartConstantKey);

            if (isExistEventKeyFrontGivesTask)
            {
                return await _cache.GetHashedAsync<EventKeyNames>(StartConstantKey, StartConstantField);
            }

            int thisIndex = loggerIndexBase * 1000 + 100;
            _logger.LogInformation(thisIndex, "eventKeysSet was NOT Init.");
            // и что делать, если нет - подписаться?
            SubscribeOnAllConstantsEvent(loggerIndexBase);
            // обратиться туда же и ждать появления констант
            thisIndex = loggerIndexBase * 1000 + 300;
            _logger.LogInformation(thisIndex, "SharedDataAccess cannot find constants and will wait them!");

            return await FetchAllConstantsWhenAppeared(cancellationToken, loggerIndexBase);
        }

        private void SubscribeOnAllConstantsEvent(int loggerIndexBase)
        {
            _logger.LogInformation(IndexBaseValue + 100, "SharedDataAccess subscribed on key {0}.", StartConstantKey);
            
            _keyEvents.Subscribe(StartConstantKey, (string key, KeyEvent cmd) =>
            {
                if (cmd == SubscribedKeyEvent)
                {
                    // при появлении ключа срабатывает подписка и делаем глобальное поле тру
                    _logger.LogInformation(IndexBaseValue + 110, "\n --- Key {Key} with command {Cmd} was received.", StartConstantKey, cmd);
                    //((AutoResetEvent)stateInfo).Set();
                    _allConstantsAppeared = true;
                    _logger.LogInformation(IndexBaseValue + 120, "All Constants appeared = {0}.", _allConstantsAppeared);
                }
            });

            string eventKeyCommand = $"Key = {StartConstantKey}, Command = {SubscribedKeyEvent}";
            _logger.LogInformation(IndexBaseValue + 130, "You subscribed on event - {EventKey}.", eventKeyCommand);
        }

        private async Task<EventKeyNames> FetchAllConstantsWhenAppeared(CancellationToken cancellationToken, int loggerIndexBase)
        {
            //static AutoResetEvent autoEvent = new AutoResetEvent(false);
            //autoEvent.WaitOne();
            // когда поле станет тру, значит ключ появился и можно идти за константами
            int count = 0;
            while (!cancellationToken.IsCancellationRequested && !_allConstantsAppeared)
            {
                // wait when _allConstantsAppeared become true
                await Task.Delay(10, cancellationToken);
                count++;
                if (count > 1000)
                {
                    int thisIndex = loggerIndexBase * 1000 + 300;
                    _logger.LogInformation(thisIndex, "SharedDataAccess still waits the constants! - {0} sec.", count/100);
                    count = 0;
                }
            }
            // замыкается кольцо - то ли это, что ожидалось?
            return await FetchAllConstants(cancellationToken, loggerIndexBase);
        }
    }
}