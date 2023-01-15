namespace Bundlor.QueryCompiler;

internal static class SpecialBinaryOperatorFunctions
{
    /*
    public static bool Older(DateTime left, TimeSpan right)
    {
        // TODO(jh)
        return true;
    }

    public static bool Newer(DateTime left, TimeSpan right)
    {
        // TODO(jh)
        return true;
    }
    */

    public static bool Like(string left, string right)
    {
        // TODO(jh) If right contains no wildcard check if left contains right
        return true;
    }

    /*
    public static bool Ilike(string left, string right)
    {
        // TODO(jh)
        return true;
    }
    */

    public static bool Matches(string left, string right)
    {
        // TODO(jh)
        return true;
    }
}
