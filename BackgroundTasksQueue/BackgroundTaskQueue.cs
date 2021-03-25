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

        private const int IndexBaseValue = 900 * 1000;

        public void QueueBackgroundWorkItem(Func<CancellationToken, Task> workItem)
        {
            if (workItem == null)
            {
                throw new ArgumentNullException(nameof(workItem));
            }

            _logger.LogInformation(IndexBaseValue + 110, "Task {Name} received in the Queue.", nameof(workItem));
            // Adds an object to the end of the System.Collections.Concurrent.ConcurrentQueue`1
            _workItems.Enqueue(workItem);
            _signal.Release();
            _logger.LogInformation(IndexBaseValue + 120, "QueueBackgroundWorkItem finished");
        }

        public async Task<Func<CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation(IndexBaseValue + 210, "BEFORE await _signal.WaitAsync");
            await _signal.WaitAsync(cancellationToken);
            _logger.LogInformation(IndexBaseValue + 220, "AFTER await _signal.WaitAsync");
            _workItems.TryDequeue(out var workItem);
            _logger.LogInformation(IndexBaseValue + 230, "AFTER _workItems.TryDequeue");


            return workItem;
        }
    }
}

