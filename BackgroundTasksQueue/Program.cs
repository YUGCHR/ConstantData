using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using CachingFramework.Redis;
using CachingFramework.Redis.Contracts.Providers;
using StackExchange.Redis;
using Microsoft.Extensions.Configuration;
using BackgroundTasksQueue.Services;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using Shared.Library.Services;

namespace BackgroundTasksQueue
{
    public class Program
    {
        private static Serilog.ILogger Logs => Serilog.Log.ForContext<Program>();

        public static void Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();

            var monitorLoop = host.Services.GetRequiredService<MonitorLoop>();
            monitorLoop.StartMonitorLoop();

            host.Run();
            Log.Information("The global logger has been closed and flushed");
            Log.CloseAndFlush();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
            .UseContentRoot(Directory.GetCurrentDirectory())
            .ConfigureAppConfiguration((hostContext, config) =>
            {
                IHostEnvironment env = hostContext.HostingEnvironment;

                // find the shared folder in the parent folder
                //string[] paths = { env.ContentRootPath, "..", "SharedSettings" };
                //var sharedFolder = Path.Combine(paths);

                //load the SharedSettings first, so that appsettings.json overrwrites it
                config
                    //.AddJsonFile(Path.Combine(sharedFolder, "sharedSettings.json"), optional: true)
                    .AddJsonFile("appsettings.json", optional: true)
                    .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true);

                config.AddEnvironmentVariables();
            })
            .ConfigureLogging((ctx, sLog) =>
            {
                //var seriLog = new LoggerConfiguration()
                //    .WriteTo.Console()
                //    .CreateLogger();

                //var outputTemplate = "{Timestamp:HH:mm} [{Level:u3}] ({ThreadId}) {Message}{NewLine}{Exception}";
                //var outputTemplate = "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message}{NewLine}in method {MemberName} at {FilePath}:{LineNumber}{NewLine}{Exception}{NewLine}";
                string outputTemplate = "{NewLine}[{Timestamp:HH:mm:ss} {Level:u3} ({ThreadId}) {SourceContext}.{MemberName} - {LineNumber}] {NewLine} {Message} {NewLine} {Exception}";

                //seriLog.Information("Hello, Serilog!");

                //Log.Logger = seriLog;

                Log.Logger = new LoggerConfiguration()
                    .Enrich.With(new ThreadIdEnricher())
                    .Enrich.FromLogContext()
                    .MinimumLevel.Verbose()
                    .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Debug, outputTemplate: outputTemplate, theme: AnsiConsoleTheme.Literate) //.Verbose .Debug .Information .Warning .Error .Fatal
                    .WriteTo.File("logs/BackgroundTasksQueue{Date}.txt", rollingInterval: RollingInterval.Day, outputTemplate: outputTemplate)
                    .CreateLogger();

                Logs.Information("The global logger Serilog has been configured.\n");
            })
            .UseDefaultServiceProvider((ctx, opts) => { /* elided for brevity */ })
            .ConfigureServices((hostContext, services) =>
            {
                services.AddSingleton<ILogger>(Log.Logger);
                try
                {
                    ConnectionMultiplexer muxer = ConnectionMultiplexer.Connect("localhost");
                    services.AddSingleton<ICacheProviderAsync>(new RedisContext(muxer).Cache);
                    services.AddSingleton<IPubSubProvider>(new RedisContext(muxer).PubSub);
                    services.AddSingleton<IKeyEventsProvider>(new RedisContext(muxer).KeyEvents);
                }
                catch (Exception ex)
                {
                    string message = ex.Message;
                    //Console.WriteLine($"\n\n Redis server did not start: \n + {message} \n");
                    Logs.Fatal("Redis server did not find: \n {@EM} \n\n", new{ ExceptionMessage = message});
                    throw;
                }
                services.AddSingleton<GenerateThisInstanceGuidService>();
                services.AddSingleton<ISharedDataAccess, SharedDataAccess>();
                services.AddHostedService<QueuedHostedService>();
                services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
                services.AddSingleton<MonitorLoop>();
                services.AddSingleton<IBackgroundTasksService, BackgroundTasksService>();
                services.AddSingleton<IOnKeysEventsSubscribeService, OnKeysEventsSubscribeService>();
                services.AddSingleton<ITasksPackageCaptureService, TasksPackageCaptureService>();
                services.AddSingleton<ITasksBatchProcessingService, TasksBatchProcessingService>();
                services.AddSingleton<ITasksProcessingControlService, TasksProcessingControlService>();
            });
    }

    internal class ThreadIdEnricher : ILogEventEnricher
    {
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
                "ThreadId", Thread.CurrentThread.ManagedThreadId));
        }
    }

    public static class LoggerExtensions
    {
        // https://stackoverflow.com/questions/29470863/serilog-output-enrich-all-messages-with-methodname-from-which-log-entry-was-ca/46905798

        public static ILogger Here(this ILogger logger, [CallerMemberName] string memberName = "", [CallerLineNumber] int sourceLineNumber = 0)
            //[CallerFilePath] string sourceFilePath = "",
        {
            return logger.ForContext("MemberName", memberName).ForContext("LineNumber", sourceLineNumber);
            //.ForContext("FilePath", sourceFilePath)
        }
    }

    public static class RandomProvider
    {
        private static readonly Random Rnd = new(Guid.NewGuid().ToString().GetHashCode());
        private static readonly object Sync = new();

        public static int Next(int min, int max)
        {
            lock (Sync)
            {
                return Rnd.Next(min, max);
            }
        }
    }

    // BackServers
    // кубик всё время выбрасывает ноль - TasksPackageCaptureService.DiceRoll corrected
    // ----- вы сейчас находитесь здесь -----
    // разделить подписку и обработку для завершения задач
    // 
    // процесс всё время создаётся один
    // процесс при каждом вбросе создаётся новый или старые учитываются?
    // как учитывать занятые процессы и незанятые
    // можно дополнительно иногда проверять по таймеру завершение пакета
    // и ещё можно параллельно проверять загрузку процессов - если появились свободные процессы, пора идти искать новый пакет
    // неправильно обрабатывается условие в while в Monitor
    // при завершении сервера успеть удалить своё поле из ключа регистрации серверов - обработать cancellationToken
    // 

    // Constants
    // ----- вы сейчас находитесь здесь -----
    // можно убрать shared setting и хранить все константы в локальном appsetting проекта константы
    // установить своё время для каждого ключа, можно вместе с названием ключа - словарь
    // сделать константы ключей в виде словаря - строка/время существования ключа
    // везде использовать имя ключа с типом словаря и только в последнем методе раскрывать и записывать
    // подумать, как обновятся константы в сервере по требованию
    // добавить веб-интерфейс с возможностью устанавливать константы - страница setting
    // при сохранении новых значений генерировать событие, чтобы все взяли новые константы

    // FrontEmulator
    // ----- вы сейчас находитесь здесь -----
    // 
    // 
    // 
    // 

    // вариант генерации уникального номера сервера (сохранить)
    //public static class ThisBackServerGuid
    //{
    //    static ThisBackServerGuid()
    //    {
    //        thisBackServerGuid = Guid.NewGuid().ToString();
    //    }

    //    private static readonly string thisBackServerGuid;

    //    public static string GetThisBackServerGuid()
    //    {
    //        return thisBackServerGuid;
    //    }
    //}
}
