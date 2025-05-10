using System;

namespace Cherris;

public static class StringExtensions
{
    public static string TrimQuotes(this string input)
    {
        if (input is null || input.Length < 2)
        {
            return input ?? "";
        }

        if ((input[0] == '"' && input[^1] == '"') || (input[0] == '\'' && input[^1] == '\''))
        {
            return input[1..^1];
        }

        return input;
    }
}