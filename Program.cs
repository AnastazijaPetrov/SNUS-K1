using SNUS_K1;

try
{
    SystemConfig config = ConfigLoader.Load(@"..\..\..\SystemConfig.xml");

    var system = new ProcessingSystem(config.WorkerCount, config.MaxQueueSize);

    system.JobCompleted += async (s, e) =>
        await Logger.WriteAsync($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{e.Status}] {e.JobId}, {e.Result}");

    system.JobFailed += async (s, e) =>
        await Logger.WriteAsync($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{e.Status}] {e.JobId}, {e.Result}");

    // Create n producers that will spam random jobs from config
    var producers = new List<Task>();
    for (int i = 0; i < config.ProducerCount; i++)
    {
        int producerId = i;
        producers.Add(Task.Run(() => JobProducer.Run(producerId, system, config.Jobs)));
    }

    // Don't let main thread end
    await Task.WhenAll(producers);
}
catch (Exception ex)
{
    Console.WriteLine($"Fatal error: {ex.Message}");
}