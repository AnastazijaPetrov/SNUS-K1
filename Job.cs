namespace SNUS_K1;

public class Job
{
    public Guid Id { get; set; }
    public JobType Type { get; set; }
    public required string Payload { get; set; }
    public int Priority { get; set; }

    // Internal field for detecting job completion
    public TaskCompletionSource<int>? Tcs { get; set; }
}
