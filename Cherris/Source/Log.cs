namespace Cherris;

using System;
using System.IO;
using System.Runtime.CompilerServices;

public class Log
{
    private readonly static string LogFilePath = "Res/Log.txt";
    private readonly static ConsoleColor infoColor = ConsoleColor.DarkGray;
    private readonly static ConsoleColor warningColor = ConsoleColor.Yellow;
    private readonly static ConsoleColor errorColor = ConsoleColor.Red;

    public static void Info(string message, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        string fullMessage = $"[{DateTime.Now:HH:mm:ss}] [INFO] [{Path.GetFileName(filePath)}:{lineNumber}] {message}";
        Console.ForegroundColor = infoColor;
        Console.WriteLine(fullMessage);
        Console.ResetColor();
        File.AppendAllText(LogFilePath, Environment.NewLine + fullMessage);
    }

    public static void Info(string message, bool condition, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        if (!condition)
        {
            return;
        }

        Console.ForegroundColor = infoColor;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [INFO] [{Path.GetFileName(filePath)}:{lineNumber}] {message}");
        Console.ResetColor();
    }

    public static void Warning(string message, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        string fullMessage = $"[{DateTime.Now:HH:mm:ss}] [WARNING] [{Path.GetFileName(filePath)}:{lineNumber}] {message}";
        Console.ForegroundColor = warningColor;
        Console.WriteLine(fullMessage);
        Console.ResetColor();
        File.AppendAllText(LogFilePath, Environment.NewLine + fullMessage);
    }

    public static void Warning(string message, bool condition, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        if (!condition)
        {
            return;
        }

        Console.ForegroundColor = warningColor;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [WARNING] [{Path.GetFileName(filePath)}:{lineNumber}] {message}");
        Console.ResetColor();
    }

    public static void Error(string message, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        string fullMessage = $"[{DateTime.Now:HH:mm:ss}] [ERROR] - {Path.GetFileName(filePath)}:{lineNumber} - {message}";
        Console.ForegroundColor = errorColor;
        Console.WriteLine(fullMessage);
        Console.ResetColor();
        File.AppendAllText(LogFilePath, Environment.NewLine + fullMessage);
    }

    public static void Error(string message, bool condition, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
    {
        if (!condition)
        {
            return;
        }

        Console.ForegroundColor = errorColor;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [ERROR] - [{Path.GetFileName(filePath)}:{lineNumber}] - {message}");
        Console.ResetColor();
    }
}