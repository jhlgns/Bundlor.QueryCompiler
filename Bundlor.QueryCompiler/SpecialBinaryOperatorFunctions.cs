﻿using System.Text.RegularExpressions;

namespace Bundlor.QueryCompiler;

internal static class SpecialBinaryOperatorFunctions
{
    public static bool Like(string left, string right)
    {
        left ??= "";
        right ??= "";

        var lookup = new bool[left.Length + 1, right.Length + 1];

        lookup[0, 0] = true;

        for (var j = 1; j <= right.Length; ++j)
        {
            if (right[j - 1] == '*')
            {
                lookup[0, j] = lookup[0, j - 1];
            }
        }

        for (var i = 1; i <= left.Length; ++i)
        {
            for (var j = 1; j <= right.Length; ++j)
            {
                if (right[j - 1] == '*')
                {
                    lookup[i, j] = lookup[i - 1, j] || lookup[i, j - 1];
                }
                else if (right[j - 1] == '?' || left[i - 1] == right[j - 1])
                {
                    lookup[i, j] = lookup[i - 1, j - 1];
                }
                else
                {
                    lookup[i, j] = false;
                }
            }
        }

        return lookup[left.Length, right.Length];
    }

    public static bool NotLike(string left, string right) => !Like(left, right);

    public static bool MatchesRegex(string left, string right)
    {
        left ??= "";
        right ??= "";

        return Regex.IsMatch(left, right);
    }

    public static bool DoesNotMatchRegex(string left, string right) => !MatchesRegex(left, right);
}
