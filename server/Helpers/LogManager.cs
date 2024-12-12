class Log(DateTime? _time, string _content)
{
    private readonly DateTime time = _time ?? DateTime.Now;
    private readonly string content = _content;

    public override string ToString()
        => $"[{time}] {content}";
}

static class LogManager
{
    private static readonly SemaphoreSlim logSemaphore = new(0);
    private static List<Log> logList = [];
    private static bool initailized = false;
    private static int signalFlag = 1;

    public static void Initialize()
    {
        List<Log>? oldLog = DbHelper.GetLogHistory(out string errorMessage);

        if(oldLog == null)
            Console.WriteLine($" Error while retrieving log: {errorMessage}");
        else if(oldLog.Count > 0)
            logList = oldLog;

        initailized = true;
    }

    public static void AddLog(string logContent)
    {
        if(!initailized)
            return;

        lock(logList)
            logList.Add(new(null, logContent));

        if(!DbHelper.AddLog(logContent, out string errorMessage))
            Console.WriteLine($" Error while adding log: {errorMessage}");
        
        if(Interlocked.Exchange(ref signalFlag, 1) == 0) // Check if there's an active listener
            logSemaphore.Release(); // Signal that a new log is available.
    }

    public static void ClearLog()
    {
        if(!initailized)
            return;

        lock(logList)
            logList.Clear();

        if(!DbHelper.ClearLog(out string errorMessage))
        {
            Console.WriteLine($" Error while clearing log: {errorMessage}");
            AddLog($"Error while clearing log: {errorMessage}");
        }
    }

    public static void WriteCurrentLog()
    {
        if(!initailized)
            return;
        
        lock(logList)
            foreach(Log log in logList)
                Console.WriteLine(log);
    }

    public static async Task WriteNewLogAsync(CancellationToken token = default)
    {
        if(!initailized)
            return;

        try
        {
            while(!token.IsCancellationRequested)
            {
                Interlocked.Exchange(ref signalFlag, 0);
                await logSemaphore.WaitAsync(token); // Wait for a new log signal.
                
                lock(logList)
                    Console.WriteLine(logList.Last());
            }
        }
        catch(OperationCanceledException) {}
        catch(Exception ex)
        {
            Console.WriteLine($" Error while displaying activity log: {ex.Message}");
            AddLog($"Error while displaying activity log: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref signalFlag, 1);
        }
    }
}
