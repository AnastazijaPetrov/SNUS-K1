namespace SNUS_K1;

public class JobHandle
{
    public Guid Id { get; set; }
    public required Task<int> Result { get; set; }
}
