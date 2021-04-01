using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace BackgroundTasksQueue
{
    public interface IBackgroundTaskQueue
    {
        void QueueBackgroundWorkItem(Func<CancellationToken, Task> workItem);

        Task<Func<CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken);
    }

    public class BackgroundTaskQueue : IBackgroundTaskQueue
    {
        private readonly ILogger<BackgroundTaskQueue> _logger;
        private ConcurrentQueue<Func<CancellationToken, Task>> _workItems = new ConcurrentQueue<Func<CancellationToken, Task>>();
        private SemaphoreSlim _signal = new SemaphoreSlim(0);

        public BackgroundTaskQueue(ILogger<BackgroundTaskQueue> logger)
        {
            _logger = logger;
        }

        private static Serilog.ILogger Logs => Serilog.Log.ForContext<BackgroundTaskQueue>();


        public void QueueBackgroundWorkItem(Func<CancellationToken, Task> workItem)
        {
            if (workItem == null)
            {
                throw new ArgumentNullException(nameof(workItem));
            }
            // Adds an object to the end of the System.Collections.Concurrent.ConcurrentQueue`1
            _workItems.Enqueue(workItem);
            Logs.Here().Verbose("Single Task placed in Concurrent Queue.");

            _signal.Release();
            Logs.Here().Verbose("Concurrent Queue is ready for new task.");

        }

        public async Task<Func<CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken)
        {
            await _signal.WaitAsync(cancellationToken);
            _workItems.TryDequeue(out var workItem);
            Logs.Here().Verbose("Single Task was dequeued from Concurrent Queue.");
            return workItem;
        }
    }
}

