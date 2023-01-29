using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;

namespace Bundlor.QueryCompiler;

public static class QueryCompiler
{
    public static Func<T, bool> Compile<T>(string query)
    {
        var lambda = CompileFilterExpression<T>(query);
        return lambda.Compile();
    }

    internal static Expression<Func<T, bool>> CompileFilterExpression<T>(string query)
    {
        var scanner = new Scanner(query);
        var result = CompileFilterExpression(typeof(T), scanner, null);

        scanner.EnsureEofReached();

        return (Expression<Func<T, bool>>)result;
    }

    internal static LambdaExpression CompileFilterExpression(
        Type type,
        Scanner scanner,
        ParserContext? parentContext)
    {
        var inputParameter = Expression.Parameter(type, $"it{(parentContext?.Depth + 1) ?? 0}");

        var parser = new Parser(scanner, new ParserContext(parentContext, type, inputParameter));

        var filterExpression = parser.ParseExpression();

        var lambdaDelegateType = typeof(Func<,>).MakeGenericType(type, typeof(bool));
        var lambda = Expression.Lambda(lambdaDelegateType, filterExpression, inputParameter);

        return lambda;
    }
}

