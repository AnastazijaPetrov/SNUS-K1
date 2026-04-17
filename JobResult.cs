namespace SNUS_K1;

public class JobResult
{
    public Guid JobId { get; set; }
    public required string Result { get; set; }
    public required string Status { get; set; }
}
