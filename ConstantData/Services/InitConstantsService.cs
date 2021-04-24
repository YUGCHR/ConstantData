using Shared.Library.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CachingFramework.Redis.Contracts;

namespace ConstantData.Services
{
    public interface IInitConstantsService
    {
        public ConstantsSet InitialiseConstantsSet();
        public EventKeyNames InitialiseEventKeyNames();
        
    } 

    public class InitConstantsService : IInitConstantsService
    {
        private readonly ISettingConstantsService _constantService;
        private readonly IConstantsCollectionService _collection;
        private readonly string _guid;

        public InitConstantsService(
            ISettingConstantsService constantService, 
            IConstantsCollectionService collection)
        {
            _constantService = constantService;
            _collection = collection;
        }

        private static Serilog.ILogger Logs => Serilog.Log.ForContext<ConstantsCollectionService>();

        public ConstantsSet InitialiseConstantsSet()
        {
            ConstantsSet constantsSet = _collection.SettingConstants;

            return constantsSet;
        }

        public EventKeyNames InitialiseEventKeyNames()
        {
            //string blank15 = _constantService.Blank15; // for testing

            return new EventKeyNames
            {
                // версия обновления констант - присваивается сервером констант
                ConstantsVersionBase = _guid,
                ConstantsVersionNumber = 0,
                // !!! this server guid - will be set in BackgroundTasksQueue
                BackServerGuid = _guid,
                // !!! backserver:(this server guid) - will be set in BackgroundTasksQueue
                BackServerPrefixGuid = $"{_constantService.GetPrefixBackServer}:{_guid}",

                // время задержки в секундах для эмулятора счета задачи
                TaskEmulatorDelayTimeInMilliseconds = _constantService.GetTaskEmulatorDelayTimeInMilliseconds,

                // верхний предел для генерации случайного числа - расширенный (например, миллион)
                RandomRangeExtended = _constantService.GetRandomRangeExtended,

                // соотношение количества задач и процессов для их выполнения на back-processes-servers (количества задач разделить на это число и сделать столько процессов)
                BalanceOfTasksAndProcesses = _constantService.GetBalanceOfTasksAndProcesses,
                // максимальное количество процессов на back-processes-servers (минимальное - 1)
                MaxProcessesCountOnServer = _constantService.GetMaxProcessesCountOnServer,

                // "subscribeOnFrom" - ключ для подписки на команду запуска эмулятора сервера
                EventKeyFrom = _constantService.GetEventKeyFrom,
                // "count" - поле для подписки на команду запуска эмулятора сервера
                EventFieldFrom = _constantService.GetEventFieldFrom,
                // операция для подписки
                EventCmd = KeyEvent.HashSet,

                // ключ регистрации серверов
                EventKeyBackReadiness = _constantService.GetEventKeyBackReadiness,
                // универсальное поле-заглушка - чтобы везде одинаковое
                EventFieldBack = _constantService.GetEventFieldBack,
                // кафе выдачи задач
                EventKeyFrontGivesTask = _constantService.GetEventKeyFrontGivesTask,
                // constants updating key
                EventKeyUpdateConstants = _constantService.GetEventKeyUpdateConstants,
                // Prefix - request:guid
                PrefixRequest = _constantService.GetPrefixRequest,
                // Prefix - package:guid
                PrefixPackage = _constantService.GetPrefixPackage,
                // Prefix - control:package:guid
                PrefixPackageControl = _constantService.GetPrefixPackageControl,
                // Prefix - completed:package:guid
                PrefixPackageCompleted = _constantService.GetPrefixPackageCompleted,
                // Prefix - task:guid
                PrefixTask = _constantService.GetPrefixTask,
                // Prefix - backserver:guid
                PrefixBackServer = _constantService.GetPrefixBackServer,
                // Prefix - process:add
                PrefixProcessAdd = _constantService.GetPrefixProcessAdd,
                // Prefix - process:cancel
                PrefixProcessCancel = _constantService.GetPrefixProcessCancel,
                // Prefix - process:count
                PrefixProcessCount = _constantService.GetPrefixProcessCount,
                // UNUSED - ?
                EventFieldFront = _constantService.GetEventFieldFront,
                // ключ выполняемых/выполненных задач
                EventKeyBacksTasksProceed = _constantService.GetEventKeyBacksTasksProceed,

                // срок хранения ключа Common
                EventKeyCommonKeyTimeDays = _constantService.GetEventKeyCommonKeyTimeDays,
                // срок хранения ключа eventKeyFrom
                EventKeyFromTimeDays = _constantService.GetEventKeyFromTimeDays,
                // срок хранения ключа 
                EventKeyBackReadinessTimeDays = _constantService.GetEventKeyBackReadinessTimeDays,
                // срок хранения ключа 
                EventKeyFrontGivesTaskTimeDays = _constantService.GetEventKeyFrontGivesTaskTimeDays,
                // срок хранения ключа 
                EventKeyBackServerMainTimeDays = _constantService.GetEventKeyBackServerMainTimeDays,
                // срок хранения ключа 
                EventKeyBackServerAuxiliaryTimeDays = _constantService.GetEventKeyBackServerAuxiliaryTimeDays
                
            };
        }
    }
}
