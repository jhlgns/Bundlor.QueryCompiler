namespace Bundlor.QueryCompiler;

public static class Functions
{
    public static DateTime DateTime(string value)
    {
        return System.DateTime.Parse(value);
    }

    public static DateTime DateTime(string value, string format)
    {
        return System.DateTime.ParseExact(value, format, null);
    }

    public static TimeSpan TimeSpan(string value)
    {
        return System.TimeSpan.Parse(value);
    }

    public static TimeSpan TimeSpan(string value, string format)
    {
        return System.TimeSpan.ParseExact(value, format, null);
    }
}
