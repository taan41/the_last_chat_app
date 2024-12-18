using System.Text;

using static System.Console;

/// <summary>
/// Manages IO-related utility functions.
/// </summary>
static class IOHelper
{
    private const int uiWidth = 60;

    /// <summary>
    /// Gets the effective UI width, limited by current window width.
    /// </summary>
    public static int UIWidth => Math.Max(0, Math.Min(uiWidth, WindowWidth - 1));

    /// <summary>
    /// Draws a border line made of character '-'.
    /// </summary>
    public static void WriteBorder() => WriteBorder('-');

    /// <summary>
    /// Draws a border line made of provided character.
    /// </summary>
    public static void WriteBorder(char ch) => WriteLine(new string(ch, UIWidth));
    
    /// <summary>
    /// Writes a header decorated with borders.
    /// </summary>
    public static void WriteHeader(string header)
    {
        WriteBorder('=');
        WriteLine(header.PadLeft((UIWidth + header.Length - 1) / 2));
        WriteBorder();
    }

    // /// <summary>
    // /// Restores cursor to start position of current line.
    // /// </summary>
    // public static void RestoreLineCursor() => SetCursorPosition(0, CursorTop);

    public static void MoveCursorLeft(int charAmount)
    {
        int targetLeft = CursorLeft + charAmount;
        int targetTop = CursorTop;

        if(targetLeft < 0)
        {
            targetTop += (int) Math.Floor((double) targetLeft / WindowWidth);
            targetLeft = (targetLeft % WindowWidth + WindowWidth) % WindowWidth; // Wrap to valid range
        }
        else if(targetLeft >= WindowWidth)
        {
            targetTop += targetLeft / WindowWidth;
            targetLeft %= WindowWidth;
        }

        if(targetTop < 0) targetTop = 0;
        if(targetTop > WindowHeight)
        {
            targetTop = WindowHeight;
            targetLeft = WindowWidth;
        }

        SetCursorPosition(targetLeft, targetTop);
    }

    /// <summary>
    /// Read and output user's input to console.
    /// </summary>
    /// <param name="intercept"> Whether to hide input as '*'. </param>
    /// <returns> A string representing user's input when ENTER key is pressed, null when ESC key is pressed. </returns>
    public static string? ReadInput(bool intercept)
        => ReadInput(null, intercept);
    
    /// <summary>
    /// Read and output user's input to console.
    /// </summary>
    /// <param name="limit"> Character limit of input. </param>
    /// <param name="intercept"> Whether to hide input as '*'. </param>
    /// <returns> A string representing user's input when ENTER key is pressed, null when ESC key is pressed. </returns>
    public static string? ReadInput(int? limit, bool intercept)
    {
        StringBuilder sb = new();
        bool done = false;
        int index = 0, startCursorLeft = CursorLeft;
        ConsoleKeyInfo key;

        while(!done)
        {
            key = ReadKey(true);

            switch(key.Key)
            {
                case ConsoleKey.Enter:
                    done = true;
                    break;

                case ConsoleKey.Escape:
                    WriteLine();
                    return null;

                case ConsoleKey.Backspace:
                    HandleBackspace(sb, ref index, key.Modifiers);
                    break;
                
                case ConsoleKey.W:
                    if((key.Modifiers & ConsoleModifiers.Control) != 0)
                        goto case ConsoleKey.Backspace;
                    else
                        goto default;

                case ConsoleKey.LeftArrow:
                    if(index > 0)
                    {
                        MoveCursorLeft(-1);
                        index--;
                    }
                    break;

                case ConsoleKey.RightArrow:
                    if(index < sb.Length)
                    {
                        MoveCursorLeft(1);
                        index++;
                    }
                    break;

                case ConsoleKey.UpArrow:
                    if(index > WindowWidth)
                    {
                        MoveCursorLeft(-WindowWidth);
                        index -= WindowWidth;
                    }
                    else
                    {
                        MoveCursorLeft(-index);
                        index = 0;
                    }
                    break;

                case ConsoleKey.DownArrow:
                    if(sb.Length - index > WindowWidth)
                    {
                        MoveCursorLeft(WindowWidth);
                        index += WindowWidth;
                    }
                    else
                    {
                        MoveCursorLeft(sb.Length - index);
                        index = sb.Length;
                    }
                    break;

                default:
                    if((startCursorLeft + index + 1) / WindowWidth < WindowHeight && sb.Length < (limit ?? MagicNum.inputLimit))
                        HandleDefaultKey(sb, ref index, key.KeyChar, startCursorLeft, intercept);
                    break;
            }
        }

        MoveCursorLeft(-index);
        WriteLine(intercept ? new string('*', sb.Length) : sb.ToString());
        return sb.ToString();
    }

    private static void HandleBackspace(StringBuilder sb, ref int index, ConsoleModifiers modifiers)
    {
        if(index < 1) return;

        int removedLength = 1;

        if((modifiers & ConsoleModifiers.Control) != 0)
        {
            sb.Remove(0, index);
            removedLength = index;
            MoveCursorLeft(-index);
            index = 0;
        }
        else
        {
            sb.Remove(--index, 1);
            MoveCursorLeft(-1);
        }

        (int oldLeft, int oldTop) = GetCursorPosition();
        Write(sb.ToString()[index..]);
        Write(new string(' ', removedLength));
        SetCursorPosition(oldLeft, oldTop);
    }

    private static void HandleDefaultKey(StringBuilder sb, ref int index, char keyChar, int startCursorLeft, bool intercept)
    {
        sb.Insert(index++, keyChar);
        
        char displayChar = intercept ? '*' : keyChar;

        if(index < sb.Length)
        {
            Write(displayChar);
            Write(intercept ? new string('*', sb.Length - index) : sb.ToString()[index ..]);
            MoveCursorLeft(index - sb.Length);

            if((sb.Length + startCursorLeft) % WindowWidth == 0)
                MoveCursorLeft(1);
        }
        else if(CursorLeft + 1 == BufferWidth)
            WriteLine(displayChar);
        else
            Write(displayChar);
    }

    /// <summary>
    /// Read user's input for confirmation.
    /// </summary>
    /// <returns> True if user enters empty string, "y", "Y"; false if "n", "N" or ESC key; null if input's invalid. </returns>
    public static bool? ReadConfirm()
        => ReadConfirm(["", "y", "Y"], [null, "n", "N"]);

    /// <summary>
    /// Read user's input for confirmation.
    /// </summary>
    /// <param name="yesValues"> Array of strings to be taken as user's confirmation. </param>
    /// <param name="noValues"> Array of strings to be taken as user's refusal. </param>
    /// <returns> A boolean based on user's input, null if input's invalid </returns>
    public static bool? ReadConfirm(string?[] yesValues, string?[] noValues)
    {
        string? input = ReadInput(false);

        if(yesValues.Contains(input))
            return true;
        else if(noValues.Contains(input))
            return false;
        else
            return null;
    }
}