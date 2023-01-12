using System.Linq.Expressions;

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

public class QueryCompiler
{
    private readonly record struct BinaryOperatorDefinition(string Operator, ExpressionType ExpressionType, int Precedence);  // TODO(jh)

    private static readonly BinaryOperatorDefinition[] BinaryOperators = new  BinaryOperatorDefinition[]
    {
        // TODO(jh) Precedences
        new("&&", ExpressionType.And, 0),
        new("||", ExpressionType.Or, 0),
        new("<=", ExpressionType.LessThanOrEqual, 0),
        new("<", ExpressionType.LessThan, 0),
        new(">=", ExpressionType.GreaterThanOrEqual, 0),
        new(">", ExpressionType.GreaterThan, 0),
        new("==", ExpressionType.Equal, 0),
        new("!=", ExpressionType.NotEqual, 0),
    };

    private static readonly string[] SpecialBinaryOperators = new[] { "older", "newer", "like", "ilike", "matches" };
    private static readonly string[] NestedQueryOperators = new[] { "any", "all" };

    private abstract record Token { }  // TODO(jh) Start, end etc.
    private record Identifier(string Value) : Token;
    private record BinaryOperator(BinaryOperatorDefinition Definition) : Token;
    private record SpecialBinaryOperator(string Operator) : Token;
    private record NestedQueryOperator(string Operator) : Token;
    private record StringLiteral(string Value) : Token;  // TODO(jh)
    private record IntegerLiteral(long Value) : Token;
    private record FloatingPointLiteral(double Value) : Token;  // TODO(jh)
    private record ParenthesisOpen() : Token;  // TODO(jh)
    private record ParenthesisClose() : Token;  // TODO(jh)
    private record BlockOpen() : Token;  // TODO(jh)
    private record BlockClose() : Token;  // TODO(jh)
    private record EndOfFile() : Token;  // TODO(jh)

    private record struct Scanner(string Input, int Position = 0)
    {
        private char this[int index] => index < Input.Length ? Input[index] : '\0';
        private char Current => this[Position];

        public Token PopToken()
        {
            while (char.IsWhiteSpace(Current))
                ++Position;

            // Identifier or special binary operator?
            if (char.IsLetter(Current) || Current == '_')
            {
                var wordStart = Position;
                while (char.IsLetter(Current) || char.IsNumber(Current) || Current == '_')
                    ++Position;

                var word = Input.Substring(wordStart, Position - wordStart);

                // TODO(jh) not?
                if (SpecialBinaryOperators.Any(x => x.Equals(word, StringComparison.OrdinalIgnoreCase)))
                    return new SpecialBinaryOperator(word);

                if (NestedQueryOperators.Any(x => x.Equals(word, StringComparison.OrdinalIgnoreCase)))
                    return new NestedQueryOperator(word);

                return new Identifier(word);
            }

            // Number? (integer or floating point)
            if (char.IsNumber(Current))
            {
                var numberStart = Position;
                while (char.IsNumber(Current))
                    ++Position;

                // TODO(jh) Floating point
                var numberString = Input.Substring(numberStart, Position - numberStart);
                var value = int.Parse(numberString);  // TODO(jh) Try & error message

                return new IntegerLiteral(value);
            }

            // Binary operator?
            foreach (var binOp in BinaryOperators)
            {
                var isMatch = true;
                for (var i = 0; i < binOp.Operator.Length; ++i)
                {
                    if (binOp.Operator[i] != this[Position + i])
                        isMatch = false;

                    ++i;
                }

                if (!isMatch)
                    continue;

                Position += binOp.Operator.Length;

                return new BinaryOperator(binOp);
            }

            if (Current == '(') return new ParenthesisOpen();
            if (Current == ')') return new ParenthesisClose();
            if (Current == '}') return new BlockOpen();
            if (Current == '{') return new BlockClose();

            throw new NotImplementedException("Next token type is not implemented yet");
        }
    }

    public static Func<T, bool> Compile<T>(string query)
    {
        var memberVariables = typeof(T).GetProperties()
            .Select(x => Expression.Variable(x.PropertyType, x.Name))
            .ToDictionary(x => x.Name!.ToLower());

        var scanner = new Scanner(query);
        var expression = ParseExpression(scanner, memberVariables);

        var todoSomethingLeftOver = true;  // TODO(jh)
        if (todoSomethingLeftOver)
            throw new Exception("TODO(jh) Error message for extraneous tokens");
    }

    private static Expression ParseExpression(
        in Scanner scanner,
        Dictionary<string, ParameterExpression> members)
    {
        var expression = ParsePrimaryExpression(scanner, members);
        switch (scanner.PopToken())
        {
            // TODO(jh) Operator precedence
            case BinaryOperator binOp:
                var right = ParseExpression(scanner, members);
                return Expression.MakeBinary(binOp.Definition.ExpressionType, expression, right);

            default:
                return expression;
        }
    }

    private static Expression ParsePrimaryExpression(
        in Scanner scanner,
        Dictionary<string, ParameterExpression> members)
    {
        switch (scanner.PopToken())
        {
            case Identifier ident:
                return members[ident.Value.ToLower()];

            case ParenthesisOpen:
                var expression = ParseExpression(scanner, members);
                if (scanner.PopToken() is not ParenthesisClose)
                    throw new Exception("TODO(jh) scanner.Require(predicate, errorMessage)");
                return expression;

            case StringLiteral s:
                return Expression.Constant(s.Value);

            case IntegerLiteral i:
                return Expression.Constant(i.Value);

            case FloatingPointLiteral f:
                return Expression.Constant(f.Value);

            // TODO(jh)

            default:
                throw new Exception("TODO(jh) Error message for invalid expression token");
        }
    }
}
