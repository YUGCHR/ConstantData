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
            //CreateHostBuilder(args).Build().Run();


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
                string outputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3} ({ThreadId}) {SourceContext}.{MemberName} - {LineNumber}] {NewLine}               {Message} {NewLine} {Exception}";

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
                    Console.WriteLine($"\n\n Redis server did not start: \n + {message} \n");
                    throw;
                }

                services.AddSingleton<GenerateThisInstanceGuidService>();
                services.AddSingleton<ILogger>(Log.Logger);
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
