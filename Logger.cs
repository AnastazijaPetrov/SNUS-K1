namespace SNUS_K1;

public static class Logger
{
    private static readonly SemaphoreSlim _fileLock = new(1, 1);
    private const string LogPath = "log.txt";

    public static async Task WriteAsync(string line)
    {
        await _fileLock.WaitAsync();
        try
        {
            await File.AppendAllTextAsync(LogPath, line + Environment.NewLine);
        }
        finally
        {
            _fileLock.Release();
        }
    }
}