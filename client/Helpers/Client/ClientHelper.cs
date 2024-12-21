using System.Net;
using System.Net.Sockets;

using static System.Console;
using static Utilities;

static class ClientHelper
{
    public static bool CheckIPv4(string ipAddress)
    {
        if (!IPAddress.TryParse(ipAddress, out _))
            return false;

        string[] parts = ipAddress.Split('.');
        if (parts.Length != 4) return false;

        foreach(var part in parts)
        {
            if (!int.TryParse(part, out int number))
                return false;

            if (number < 0 || number > 255)
                return false;

            if (part.Length > 1 && part[0] == '0')
                return false;
        }

        return true;
    }

    /// <summary>
    /// Save user's input to 'dataBuffer', automatically print error prompt if input's length is shorter than 'minLength'.
    /// </summary>
    /// <param name="intercept"> Whether to hide input as '*'. </param>
    /// <returns> True if input sastifies the condition; false otherwise; null if cancelled. </returns>
    public static string? InputData(string dataName, int minLength, int? maxLength, bool intercept)
    {
        string? inputBuffer;
        string errorPrompt = $" Error: {dataName} must have at least {minLength} characters";
        int moveLength = WindowWidth - CursorLeft + errorPrompt.Length;
        
        while ((inputBuffer = IOHelper.ReadInput(maxLength, intercept)) != null && inputBuffer.Length < minLength)
        {
            Write(errorPrompt);
            ReadKey(true);
            IOHelper.MoveCursor(-moveLength);
            Write(new string(' ', moveLength));
            IOHelper.MoveCursor(-moveLength);
        }

        return inputBuffer;
    }

    
    // public static bool? InputData(ref StringBuilder dataBuilder, string dataName, int minLength, int? maxLength, bool intercept)
    // {
    //     dataBuilder.Clear();
    //     string? inputBuffer = IOHelper.ReadInput(maxLength, intercept);

    //     if (inputBuffer == null)
    //         return null;
        
    //     if (inputBuffer.Length < minLength)
    //     {
    //         WriteLine($" Error: {dataName} must have at least {minLength} characters");
    //         ReadKey(true);
    //         return false;
    //     }

    //     dataBuilder.Append(inputBuffer);
    //     return true;
    // }

    /// <summary>
    /// Send 'cmdToSend' over provided NetworkStream 'stream'. Automatically output to console if 'receivedCmd' is error type.
    /// </summary>
    /// <returns> True if 'receivedCmd' is same type as 'cmdToSend'; false otherwise. </returns>
    public static bool SendCmd(NetworkStream stream, ref byte[] buffer, Command cmdToSend, out Command receivedCmd)
    {
        receivedCmd = new();

        int bytesRead, totalRead = 0;
        lock(stream)
        {
            stream.Write(EncodeString(cmdToSend.Serialize()));

            while((bytesRead = stream.Read(buffer, totalRead, 1024)) > 0)
            {
                totalRead += bytesRead;
                
                if(bytesRead < 1024)
                    break;

                if(totalRead + 1024 >= buffer.Length)
                    Array.Resize(ref buffer, buffer.Length * 2);
            }
        }
        
        Command? tempCmd = Command.Deserialize(DecodeBytes(buffer, 0, totalRead));
        Array.Clear(buffer);

        if(tempCmd == null)
        {
            WriteLine(" Error: Received null command");
            ReadKey(true);
            return false;
        }

        switch(tempCmd.CommandType)
        {
            case var value when value == cmdToSend.CommandType:
                receivedCmd = tempCmd;
                return true;

            case CommandType.Error:
                WriteLine($" Error: {tempCmd.Payload}");
                ReadKey(true);
                return false;

            default:
                WriteLine($" Error: Received invalid command {tempCmd.CommandType}");
                ReadKey(true);
                return false;
        }
    }

    public static void SendDisconnect(NetworkStream stream)
    {
        Command disconnect = new(CommandType.Disconnect, null);
        stream.Write(EncodeString(disconnect.Serialize()));
    }
}
