namespace SNUS_K1;

public class JobExecutionRecord
{
    public Guid JobId { get; set; }
    public JobType Type { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public string Status { get; set; } = string.Empty;
}