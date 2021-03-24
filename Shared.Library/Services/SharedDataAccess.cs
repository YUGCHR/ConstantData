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

        //public Task SetStartConstants(EventKeyNames eventKeysSet, string checkTokenFetched);
        public Task<EventKeyNames> FetchAllConstants();
        public void SubscribeOnAllConstantsEvent();
        public Task<EventKeyNames> FetchAllConstantsWhenAppeared(CancellationToken cancellationToken);
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

        //private readonly TimeSpan _startConstantKeyLifeTime = TimeSpan.FromDays(1);
        //private const string CheckToken = "tt-tt-tt";

        public (string, string) FetchBaseConstants([CallerMemberName] string currentMethodNameName = "") // May be will cause problem with Docker
        {
            // if problem with Docker can use token
            if (currentMethodNameName == "ConstantsMountingMonitor") return (StartConstantKey, StartConstantField);
            _logger.LogError(IndexBaseValue + 070, "FetchBaseConstants was called by wrong method - {0}.", currentMethodNameName);
            return (null, null);

        }

        //public async Task SetStartConstants(EventKeyNames eventKeysSet, string checkTokenFetched)
        //{
        //    if (checkTokenFetched == CheckToken)
        //    {
        //        // установить своё время для ключа, можно вместе с названием ключа
        //        await _cache.SetHashedAsync<EventKeyNames>(StartConstantKey, StartConstantField, eventKeysSet, _startConstantKeyLifeTime);

        //        _logger.LogInformation(55050, "SetStartConstants set constants (EventKeyFrom for example = {0}) in key {1}.", eventKeysSet.EventKeyFrom, "constants");
        //    }
        //    else
        //    {
        //        _logger.LogError(55070, "SetStartConstants try to set constants unsuccessfully.");
        //    }
        //}

        public async Task<EventKeyNames> FetchAllConstants()
        {
            // проверить, есть ли ключ
            bool isExistEventKeyFrontGivesTask = await _cache.KeyExistsAsync(StartConstantKey);

            if (isExistEventKeyFrontGivesTask)
            {
                return await _cache.GetHashedAsync<EventKeyNames>(StartConstantKey, StartConstantField);
            }

            return null;
        }

        public void SubscribeOnAllConstantsEvent()
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

        public async Task<EventKeyNames> FetchAllConstantsWhenAppeared(CancellationToken cancellationToken)
        {
            //static AutoResetEvent autoEvent = new AutoResetEvent(false);
            //autoEvent.WaitOne();
            // когда поле станет тру, значит ключ появился и можно идти за константами
            while (!cancellationToken.IsCancellationRequested && !_allConstantsAppeared)
            {
                // wait when _allConstantsAppeared become true
                await Task.Delay(10, cancellationToken);
            }
            return await FetchAllConstants();
        }
    }
}