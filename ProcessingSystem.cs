namespace SNUS_K1;

public class ProcessingSystem
{
    public event EventHandler<JobResult>? JobCompleted;
    public event EventHandler<JobResult>? JobFailed;

    private readonly object _queueLock = new();
    private readonly PriorityQueue<Job, int> _jobQueue = new();
    private readonly SemaphoreSlim _jobsAvailable;
    private readonly int _maxQueueSize;
    private readonly Task[] _workers;

    private readonly Dictionary<Guid, Job> _allJobs = new();

    private readonly object _recordsLock = new();
    private readonly List<JobExecutionRecord> _executionRecords = new();
    private readonly Timer _reportTimer; // Keep as attribute to shield from GC

    public ProcessingSystem(int workerCount, int maxQueueSize)
    {
        _maxQueueSize = maxQueueSize;
        _jobsAvailable = new SemaphoreSlim(0);

        _workers = new Task[workerCount];
        for (int i = 0; i < workerCount; i++)
        {
            _workers[i] = Task.Run(WorkerLoop);
        }

        // Generate report every minute
        _reportTimer = new Timer(_ => GenerateReport(), null,
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    public Job? GetJob(Guid id)
    {
        lock (_queueLock)
        {
            _allJobs.TryGetValue(id, out var job);
            return job;
        }
    }

    public IEnumerable<Job> GetTopJobs(int n)
    {
        lock (_queueLock)
        {
            return _jobQueue.UnorderedItems
                .OrderBy(x => x.Priority)
                .Take(n)
                .Select(x => x.Element)
                .ToList();
        }
    }

    /*
     * Add job to priority queue and notify workers
     * Return a handle with Task that will complete when job is done
     * If job is not unique, return a failed task
     * If queue is full, return a failed task
     */
    public JobHandle Submit(Job job)
    {
        var tcs = new TaskCompletionSource<int>();
        job.Tcs = tcs;

        var handle = new JobHandle
        {
            Id = job.Id,
            Result = tcs.Task
        };

        lock (_queueLock)
        {
            if (_jobQueue.Count >= _maxQueueSize)
            {
                tcs.SetException(new InvalidOperationException("Queue is full"));
                return handle;
            }

            if (_allJobs.ContainsKey(job.Id))
            {
                tcs.SetException(new InvalidOperationException($"Duplicate job ID {job.Id}"));
                return handle;
            }

            _allJobs.Add(job.Id, job);
            _jobQueue.Enqueue(job, job.Priority);
        }

        _jobsAvailable.Release();
        return handle;
    }

    /*
     * Processes highest priority job from queue
     * If there are no jobs, wait until one is available
     */
    private async Task WorkerLoop()
    {
        while (true)
        {
            await _jobsAvailable.WaitAsync();

            Job? job;
            lock (_queueLock)
            {
                _jobQueue.TryDequeue(out job, out _);
            }

            if (job != null)
            {
                await ProcessWithRetry(job);
            }
        }
    }

    /*
     * Processes a job with n attempts, each lasting m seconds max
     * If job succeeds, mark as COMPLETED and invoke job failed event
     * If job timeouts, mark as FAILED and invoke job failed event
     * If all attempts fail, mark job as ABORT and invoke job completed event
     */
    private async Task ProcessWithRetry(Job job)
    {
        const int maxAttempts = 3;
        const int timeoutMs = 2000;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var cts = new CancellationTokenSource();
            var jobTask = ExecuteJob(job);
            var delayTask = Task.Delay(timeoutMs, cts.Token);
            var completed = await Task.WhenAny(jobTask, delayTask);

            if (completed == jobTask && jobTask.IsCompletedSuccessfully)
            {
                cts.Cancel(); // Cancel the task.delay if job finished in time
                stopwatch.Stop();
                int result = jobTask.Result;
                job.Tcs!.TrySetResult(result);

                // Add to records for report
                lock (_recordsLock)
                {
                    _executionRecords.Add(new JobExecutionRecord
                    {
                        JobId = job.Id,
                        Type = job.Type,
                        ExecutionTime = stopwatch.Elapsed,
                        Status = "COMPLETED"
                    });
                }

                JobCompleted?.Invoke(this, new JobResult
                {
                    JobId = job.Id,
                    Status = "COMPLETED",
                    Result = result.ToString()
                });
                return;
            }

            string reason = (completed == jobTask)
                ? $"Exception: {jobTask.Exception?.InnerException?.Message ?? "unknown"}"
                : "Timeout after 2s";

            bool isLastAttempt = (attempt == maxAttempts);
            string status = isLastAttempt ? "ABORT" : "FAILED";

            JobFailed?.Invoke(this, new JobResult
            {
                JobId = job.Id,
                Status = status,
                Result = isLastAttempt ? "Max retries exceeded" : reason
            });

            if (isLastAttempt)
            {
                stopwatch.Stop();
                lock (_recordsLock)
                {
                    // Only add ABORT status to records for report and not FAILED
                    _executionRecords.Add(new JobExecutionRecord
                    {
                        JobId = job.Id,
                        Type = job.Type,
                        ExecutionTime = stopwatch.Elapsed,
                        Status = "ABORT"
                    });
                }

                job.Tcs!.TrySetException(new Exception("Job aborted after max retries"));
                return;
            }
        }
    }

    private static Task<int> ExecuteJob(Job job)
    {
        if (job.Type == JobType.IO)
        {
            int delay = int.Parse(job.Payload.Split(':')[1].Replace("_", ""));
            return Task.Run(() => ProcessIO(delay));
        }
        else
        {
            var parts = job.Payload.Split(',');
            int limit = int.Parse(parts[0].Split(':')[1].Replace("_", ""));
            int threadCount = int.Parse(parts[1].Split(':')[1].Replace("_", ""));
            threadCount = Math.Clamp(threadCount, 1, 8);
            return Task.Run(() => ProcessPrime(limit, threadCount));
        }
    }

    public static int ProcessIO(int delay)
    {
        Thread.Sleep(delay);
        return Random.Shared.Next(0, 101);
    }

    public static int ProcessPrime(int limit, int threadCount)
    {
        int chunkSize = limit / threadCount;
        var tasks = new Task<int>[threadCount];

        for (int i = 0; i < threadCount; i++)
        {
            int start = i * chunkSize + (i == 0 ? 2 : 1);
            int end = (i == threadCount - 1) ? limit : (i + 1) * chunkSize;

            tasks[i] = Task.Run(() => CountPrimes(start, end));
        }

        Task.WaitAll(tasks);
        return tasks.Sum(t => t.Result);
    }

    private static int CountPrimes(int start, int end)
    {
        int count = 0;
        for (int n = start; n <= end; n++)
            if (IsPrime(n)) count++;
        return count;
    }

    private static bool IsPrime(int n)
    {
        if (n < 2) return false;
        for (int i = 2; i * i <= n; i++)
            if (n % i == 0) return false;
        return true;
    }

    /*
     * Copy existing records and send them to report generator
     */
    private void GenerateReport()
    {
        JobExecutionRecord[] snapshot;
        lock (_recordsLock)
        {
            snapshot = _executionRecords.ToArray();
        }
        ReportGenerator.Generate(snapshot);
    }

}
