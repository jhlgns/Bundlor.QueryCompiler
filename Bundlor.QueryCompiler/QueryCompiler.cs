using System.Linq.Expressions;
using System.Reflection;

namespace Bundlor.QueryCompiler;

/*

NOTE(jh): "x not like y" === "x !like y"
NOTE(jh): "x not == y"   === "x != y"

Example queries:

record BirthDay(int Year, int Month, int Day);
record Address(string City, string Street)
record User(string UserName, string Email, string Password, Address Address, BirthDay BirthDay, DateTime RegisteredAt);

Query for User[]:
username like *Müller* && a.cit == "Frankfurt" && ADDRESS.STREET not like "Rosa*Parks*" || (regis newer 1h29m59s || regis !newer)


record Team(User Manager, User[] Users)

Query for Team:
manager.id == 82348193 && users any { city ilike "bad*" }
manager.name ilike "steven*" || users not all { street like "*29" }

*/

// TODO(jh) Make everything as resilient as possible - case insensitive, shortcuts (if not ambiguous etc.)
// TODO(jh) Make more efficient by using ReadOnlySpan<char> etc.
// TODO(jh) String quotes are optional for words without whitespace on right side of binary operator
// TODO(jh) Tests
// TODO(jh) Benchmarks

/*
record Cowabunga(int Id, string Name)
record SomeStruct(int A, double B, string C, Cowabunga Value)

Compile<SomeStruct>("a == 123 && C like hans* || v.id < 1000");

Should generate something like the following lambda:

(Cowabunga input) =>
{
    return input.A == 123 && Like(input.C, "hans*") || input.Value.Id < 1000;
}
*/

public static class QueryCompiler
{
    public static Func<T, bool> Compile<T>(string query)
    {
        var (filterExpression, inputParameter) = CompileFilterExpression<T>(query);
        var lambda = Expression.Lambda<Func<T, bool>>(filterExpression, inputParameter);

        return lambda.Compile();
    }

    internal static (Expression expression, ParameterExpression) CompileFilterExpression<T>(string query)
    {
        var members = typeof(T).GetProperties()
         .Cast<MemberInfo>()
         .Concat(typeof(T).GetFields())
         .ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        var inputParameter = Expression.Parameter(typeof(T), "input");  // TODO(jh) 'in' in case of structs?

        var scanner = new Scanner(query);
        var parser = new Parser(scanner, new ParserContext(members, inputParameter));

        var filterExpression = parser.ParseExpression();

        return (filterExpression, inputParameter);
    }
}
