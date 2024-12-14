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
    private static bool initailized = false, inLogView = false;
    private static int signalFlag = 1;

    public static async void Initialize()
    {
        (List<Log>? oldLog, string errorMessage) = await DbHelper.GetLogHistory();

        if(oldLog == null)
            Console.WriteLine($" DB error trying to retrieve log: {errorMessage}");
        else if(oldLog.Count > 0)
            logList = oldLog;

        initailized = true;
    }

    public static async void AddLog(string logContent)
    {
        if(!initailized)
            return;

        (bool success, string errorMessage) = await DbHelper.AddLog(logContent);

        if(success)
        {
            Log newLog = new(null, logContent);

            lock(logList)
            {
                logList.Add(newLog);
                
                if(inLogView)
                    Console.WriteLine(newLog.ToString());
            }

        }
        else
            Console.WriteLine($" DB error trying to add new log: {errorMessage}");

        // if(Interlocked.Exchange(ref signalFlag, 1) == 0) // Check if there's an active listener
            // logSemaphore.Release(); // Signal that a new log is available.
    }

    public static async void ClearLog()
    {
        if(!initailized)
            return;

        lock(logList)
            logList.Clear();

        (bool success, string errorMessage) = await DbHelper.ClearLog();

        if(!success)
        {
            Console.WriteLine($" DB error trying to clear log: {errorMessage}");
            AddLog($"DB error trying to clear log: {errorMessage}");
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

    public static void ToggleLogView(bool toggle)
        => inLogView = toggle;

    public static async Task WriteNewLogAsync(CancellationToken token = default)
    {
        if(!initailized)
            return;

        inLogView = true;

        try
        {
            while(!token.IsCancellationRequested)
            {
                // Interlocked.Exchange(ref signalFlag, 0);
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
            // Interlocked.Exchange(ref signalFlag, 1);
            inLogView = false;
        }
    }
}
