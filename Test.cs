namespace SNUS_K1;

public static class Test
{
    public static async Task RunAllAsync()
    {
        Console.WriteLine("=== Running tests ===");
        await TestIOJob();
        await TestPrimeJob();
        await TestMultipleJobsPriority();
        await TestQueueFull();
        await TestDuplicateId();
        Console.WriteLine("\n=== Tests finished ===");
    }

    private static async Task TestIOJob()
    {
        Console.WriteLine("\n[Test 1] IO job returns result without Thread.Sleep in test");
        var system = new ProcessingSystem(2, 10);

        var job = new Job
        {
            Id = Guid.NewGuid(),
            Type = JobType.IO,
            Payload = "delay:500",
            Priority = 1
        };

        var handle = system.Submit(job);
        int result = await handle.Result;

        Console.WriteLine($"  IO job {job.Id} finished, result={result}");
        Console.WriteLine(result >= 0 && result <= 100 ? "  PASS" : "  FAIL");
    }

    private static async Task TestPrimeJob()
    {
        Console.WriteLine("\n[Test 2] Prime job returns correct prime count");
        var system = new ProcessingSystem(2, 10);

        // Number of primes up to 100 is 25
        var job = new Job
        {
            Id = Guid.NewGuid(),
            Type = JobType.Prime,
            Payload = "numbers:100,threads:2",
            Priority = 1
        };

        var handle = system.Submit(job);
        int result = await handle.Result;

        Console.WriteLine($"  Prime job {job.Id} finished, result={result}");
        Console.WriteLine(result == 25 ? "  PASS" : "  FAIL (expected 25)");
    }

    private static async Task TestMultipleJobsPriority()
    {
        Console.WriteLine("\n[Test 3] Multiple jobs, all finish through await");
        var system = new ProcessingSystem(3, 10);

        var jobs = new List<Job>
        {
            new() { Id = Guid.NewGuid(), Type = JobType.IO, Payload = "delay:200", Priority = 3 },
            new() { Id = Guid.NewGuid(), Type = JobType.IO, Payload = "delay:300", Priority = 1 },
            new() { Id = Guid.NewGuid(), Type = JobType.IO, Payload = "delay:100", Priority = 2 },
        };

        var handles = jobs.Select(j => system.Submit(j)).ToList();
        var results = await Task.WhenAll(handles.Select(h => h.Result));

        Console.WriteLine($"  All jobs finished: {string.Join(", ", results)}");
        Console.WriteLine("  PASS");
    }

    private static async Task TestQueueFull()
    {
        Console.WriteLine("\n[Test 4] Reject job when queue is full");
        var system = new ProcessingSystem(1, 2);  // 1 worker, queue size 2

        var j1 = new Job { Id = Guid.NewGuid(), Type = JobType.IO, Payload = "delay:1000", Priority = 1 };
        var j2 = new Job { Id = Guid.NewGuid(), Type = JobType.IO, Payload = "delay:100", Priority = 1 };
        var j3 = new Job { Id = Guid.NewGuid(), Type = JobType.IO, Payload = "delay:100", Priority = 1 };
        var j4 = new Job { Id = Guid.NewGuid(), Type = JobType.IO, Payload = "delay:100", Priority = 1 };

        system.Submit(j1);
        system.Submit(j2);
        system.Submit(j3);
        var h4 = system.Submit(j4);

        try
        {
            await h4.Result;
            Console.WriteLine("  FAIL: expected exception for full queue");
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"  Rejected job {j4.Id}: {ex.Message}");
            Console.WriteLine("  PASS");
        }
    }

    private static async Task TestDuplicateId()
    {
        Console.WriteLine("\n[Test 5] Same Job ID must not execute twice");
        var system = new ProcessingSystem(2, 10);

        var id = Guid.NewGuid();
        var j1 = new Job { Id = id, Type = JobType.IO, Payload = "delay:200", Priority = 1 };
        var j2 = new Job { Id = id, Type = JobType.IO, Payload = "delay:200", Priority = 1 };

        var h1 = system.Submit(j1);
        var h2 = system.Submit(j2);

        int r1 = await h1.Result;
        Console.WriteLine($"  First job passed, result={r1}");

        try
        {
            await h2.Result;
            Console.WriteLine("  FAIL: expected exception for duplicate ID");
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"  Duplicate rejected: {ex.Message}");
            Console.WriteLine("  PASS");
        }
    }
}