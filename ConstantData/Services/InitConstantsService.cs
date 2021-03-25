using Shared.Library.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CachingFramework.Redis.Contracts;

namespace ConstantData.Services
{
    public interface IInitConstantsService
    {
        public EventKeyNames InitialiseEventKeyNames();
    } 

    public class InitConstantsService : IInitConstantsService
    {
        private readonly ISettingConstantsService _constantService;
        private readonly string _guid;

        public InitConstantsService(ISettingConstantsService constantService)
        {
            _constantService = constantService;
        }

        public EventKeyNames InitialiseEventKeyNames()
        {
            return new EventKeyNames
            {
                TaskEmulatorDelayTimeInMilliSeconds = _constantService.GetTaskEmulatorDelayTimeInMilliSeconds, // время задержки в секундах для эмулятора счета задачи
                BalanceOfTasksAndProcesses = _constantService.GetBalanceOfTasksAndProcesses, // соотношение количества задач и процессов для их выполнения на back-processes-servers (количества задач разделить на это число и сделать столько процессов)
                MaxProcessesCountOnServer = _constantService.GetMaxProcessesCountOnServer, // максимальное количество процессов на back-processes-servers (минимальное - 1)
                EventKeyFrom = _constantService.GetEventKeyFrom, // "subscribeOnFrom" - ключ для подписки на команду запуска эмулятора сервера
                EventFieldFrom = _constantService.GetEventFieldFrom, // "count" - поле для подписки на команду запуска эмулятора сервера
                EventCmd = KeyEvent.HashSet,
                EventKeyBackReadiness = _constantService.GetEventKeyBackReadiness, // ключ регистрации серверов
                EventFieldBack = _constantService.GetEventFieldBack,
                EventKeyFrontGivesTask = _constantService.GetEventKeyFrontGivesTask, // кафе выдачи задач
                PrefixRequest = _constantService.GetPrefixRequest, // request:guid
                PrefixPackage = _constantService.GetPrefixPackage, // package:guid
                PrefixTask = _constantService.GetPrefixTask, // task:guid
                PrefixBackServer = _constantService.GetPrefixBackServer, // backserver:guid
                BackServerGuid = _guid, // !!! this server guid - NOT USED
                BackServerPrefixGuid = $"{_constantService.GetPrefixBackServer}:{_guid}", // !!! backserver:(this server guid) - NOT USED
                PrefixProcessAdd = _constantService.GetPrefixProcessAdd, // process:add
                PrefixProcessCancel = _constantService.GetPrefixProcessCancel, // process:cancel
                PrefixProcessCount = _constantService.GetPrefixProcessCount, // process:count
                EventFieldFront = _constantService.GetEventFieldFront,
                EventKeyBacksTasksProceed = _constantService.GetEventKeyBacksTasksProceed, //  ключ выполняемых/выполненных задач                
                EventKeyFromTimeDays = _constantService.GetEventKeyFromTimeDays, // срок хранения ключа eventKeyFrom
                EventKeyBackReadinessTimeDays = _constantService.GetEventKeyBackReadinessTimeDays, // срок хранения 
                EventKeyFrontGivesTaskTimeDays = _constantService.GetEventKeyFrontGivesTaskTimeDays, // срок хранения ключа 
                EventKeyBackServerMainTimeDays = _constantService.GetEventKeyBackServerMainTimeDays, // срок хранения ключа 
                EventKeyBackServerAuxiliaryTimeDays = _constantService.GetEventKeyBackServerAuxiliaryTimeDays, // срок хранения ключа 
            };
        }
    }
}
