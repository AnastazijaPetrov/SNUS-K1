namespace SNUS_K1;

public class SystemConfig
{
    public int WorkerCount { get; set; }
    public int ProducerCount { get; set; }
    public int MaxQueueSize { get; set; }
    public List<Job> Jobs { get; set; } = [];
}
