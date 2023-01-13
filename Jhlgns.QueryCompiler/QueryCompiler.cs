using System.Linq.Expressions;
using System.Reflection;
using static Jhlgns.QueryCompiler.TokenConstants;

namespace Jhlgns.QueryCompiler;

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
    int a;
    double b;
    string c;
    Cowabunga value;
    a = input.A;
    b = input.B;
    c = input.C;
    value = input.Cowabunga;

    return a == 123 && Like(c,  "hans*") || value.Id < 1000;
}
*/

public class QueryCompiler
{
    private record MemberInfo(PropertyInfo Property, ParameterExpression Variable);

    public static Func<T, bool> Compile<T>(string query)
    {
        var members = typeof(T).GetProperties()
            .Select(x => new MemberInfo(x, Expression.Variable(x.PropertyType, x.Name)))
            .ToDictionary(x => x.Property.Name.ToLower());

        var scanner = new Scanner(query);
        var filterExpression = ParseExpression(scanner, members);

        //if (scanner.Position != scanner.Input.Length)

        var todoSomethingLeftOver = true;  // TODO(jh)
        if (todoSomethingLeftOver)
            throw new Exception("TODO(jh) Error message for extraneous tokens");


        var inputParam = Expression.Parameter(typeof(T), "input");

        var bodyExpressions = new List<Expression>();
        bodyExpressions.AddRange(members.Values.Select(x => x.Variable));

        foreach (var (_, variable) in members)
        {
            var inputMember = Expression.MakeMemberAccess(inputParam, variable.Property);
            var assignment = Expression.Assign(variable.Variable, inputMember);
            bodyExpressions.Add(assignment);
        }

        var returnExpression = Expression.Return(Expression.Label(), filterExpression, typeof(bool));  // TODO
        bodyExpressions.Add(returnExpression);

        var body = Expression.Block(bodyExpressions);
        var lambda = Expression.Lambda<Func<T, bool>>(body, inputParam);

        return lambda.Compile();
    }

    private static Expression ParseExpression(
        in Scanner scanner,
        Dictionary<string, MemberInfo> members)
    {
        var expression = ParsePrimaryExpression(scanner, members);
        var token = scanner.PopToken();
        switch (token.Kind)
        {
            // TODO(jh) Operator precedence
            case TokenKind.BinaryOperator:
                var right = ParseExpression(scanner, members);
                var defn = GetBinaryOperatorDefinition(token.Text);
                return Expression.MakeBinary(defn.ExpressionType, expression, right);

            default:
                return expression;
        }
    }

    private static Expression ParsePrimaryExpression(
        in Scanner scanner,
        Dictionary<string, MemberInfo> members)
    {
        var token = scanner.PopToken();
        switch (token.Kind)
        {
            case TokenKind.Identifier:
                return members[token.Text.ToLower()].Variable;

            case TokenKind.ParenthesisOpen:
                var expression = ParseExpression(scanner, members);
                if (scanner.PopToken().Kind != TokenKind.ParenthesisClose)
                    throw new Exception("TODO(jh) scanner.Require(predicate, errorMessage)");
                return expression;

            case TokenKind.StringLiteral:
                return Expression.Constant(token.StringValue);

            case TokenKind.IntegerLiteral:
                return Expression.Constant(token.IntValue);

            case TokenKind.FloatingPointLiteral:
                return Expression.Constant(token.DoubleValue);

            // TODO(jh)

            default:
                throw new Exception("TODO(jh) Error message for invalid expression token");
        }
    }
}
