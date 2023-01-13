using System.Linq.Expressions;

namespace Jhlgns.QueryCompiler;

internal class BinaryOperatorDefinition
{
    public BinaryOperatorDefinition(string @operator, ExpressionType expressionType, int precedence)
    {
        Operator = @operator;
        ExpressionType = expressionType;
        Precedence = precedence;
    }

    public readonly string Operator;
    public readonly ExpressionType ExpressionType;
    public readonly int Precedence;
}

internal enum TokenKind
{
    Identifier,
    BinaryOperator,
    SpecialBinaryOperator,
    NestedQueryOperator,
    StringLiteral,
    IntegerLiteral,
    FloatingPointLiteral,
    ParenthesisOpen,
    ParenthesisClose,
    BlockOpen,
    BlockClose,
    EndOfFile,
}

internal readonly struct Token
{
    public Token(TokenKind kind, string text) => (Kind, Text) = (kind, text);

    public readonly TokenKind Kind;
    public readonly string Text;

    private readonly string? _stringValue;
    public string? StringValue
    {
        get => _stringValue ?? throw new InvalidOperationException();
        init => _stringValue = value;
    }

    private readonly int? _intValue;
    public int? IntValue
    {
        get => _intValue ?? throw new InvalidOperationException();
        init => _intValue = value;
    }

    private readonly double? _doubleValue;
    public double? DoubleValue
    {
        get => _doubleValue ?? throw new InvalidOperationException();
        init => _doubleValue = value;
    }
}

internal static class TokenConstants
{
    public static readonly BinaryOperatorDefinition[] BinaryOperators = new BinaryOperatorDefinition[]
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

    public static BinaryOperatorDefinition GetBinaryOperatorDefinition(string @operator)
        => BinaryOperators.First(x => x.Operator == @operator);

    public static readonly string[] SpecialBinaryOperators = new[] { "older", "newer", "like", "ilike", "matches" };
    public static readonly string[] NestedQueryOperators = new[] { "any", "all" };
}
