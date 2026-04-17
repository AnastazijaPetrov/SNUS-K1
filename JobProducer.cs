namespace SNUS_K1;

public static class JobProducer
{
    public static async Task Run(int producerId, ProcessingSystem system, List<Job> templates)
    {
        var rng = new Random(producerId);
        while (true)
        {
            var template = templates[rng.Next(templates.Count)];
            var job = new Job
            {
                Id = Guid.NewGuid(),
                Type = template.Type,
                Payload = template.Payload,
                Priority = template.Priority
            };

            system.Submit(job);
            await Task.Delay(rng.Next(300, 800));
        }
    }
}