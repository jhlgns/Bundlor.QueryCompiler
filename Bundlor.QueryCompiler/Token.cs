using System.Linq.Expressions;

namespace Bundlor.QueryCompiler;

internal enum TokenKind
{
    Equal,
    NotEqual,
    LessThanOrEqual,
    LessThan,
    GreaterThanOrEqual,
    GreaterThan,
    And,
    Or,
    Minus,
    Not,
    BitwiseNot,
    Identifier,
    SpecialBinaryOperator,
    NestedQueryOperator,
    StringLiteral,
    IntegerLiteral,
    FloatingPointLiteral,
    BooleanLiteral,
    ParenthesisOpen,
    ParenthesisClose,
    BlockOpen,
    BlockClose,
    EndOfFile,
}

internal record class BinaryOperatorInfo(
    string Operator,
    string Alternate,
    TokenKind TokenKind,
    ExpressionType ExpressionType,
    int Precedence);

internal record class UnaryOperatorInfo(
    char Operator,
    TokenKind TokenKind,
    ExpressionType ExpressionType);

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

    private readonly bool? _boolValue;
    public bool? BoolValue
    {
        get => _boolValue ?? throw new InvalidOperationException();
        init => _boolValue = value;
    }

    public object OpaqueLiteralValue
    {
        get
        {
            if (_stringValue != null) return _stringValue;
            if (_intValue != null) return _intValue;
            if (_doubleValue != null) return _doubleValue;
            if (_boolValue != null) return _boolValue;
            throw new InvalidOperationException();
        }
    }

    public override string ToString() => Text;
}

internal static class TokenConstants
{
    public static readonly BinaryOperatorInfo[] BinaryOperators = new BinaryOperatorInfo[]
    {
        new("==","eq", TokenKind.Equal, ExpressionType.Equal, 100),
        new("!=","ne", TokenKind.NotEqual, ExpressionType.NotEqual, 100),
        new("<=","le", TokenKind.LessThanOrEqual, ExpressionType.LessThanOrEqual, 90),
        new("<", "lt", TokenKind.LessThan, ExpressionType.LessThan, 90),
        new(">=","ge", TokenKind.GreaterThanOrEqual, ExpressionType.GreaterThanOrEqual, 90),
        new(">", "gt", TokenKind.GreaterThan, ExpressionType.GreaterThan, 90),
        new("&&","and", TokenKind.And, ExpressionType.And, 80),
        new("||","or", TokenKind.Or, ExpressionType.Or, 70),
    };

    public static BinaryOperatorInfo? TryGetBinaryOperatorInfo(TokenKind tokenKind)
        => BinaryOperators.FirstOrDefault(x => x.TokenKind == tokenKind);

    public static readonly UnaryOperatorInfo[] UnaryOperators = new UnaryOperatorInfo[]
    {
        new('-', TokenKind.Minus, ExpressionType.Negate),
        new('!', TokenKind.Not, ExpressionType.Not),
        new('~', TokenKind.BitwiseNot, ExpressionType.Not),
    };

    public static UnaryOperatorInfo? TryGetUnaryOperatorInfo(TokenKind tokenKind)
        => UnaryOperators.FirstOrDefault(x => x.TokenKind == tokenKind);

    public static readonly string[] SpecialBinaryOperators = new[] { "older", "newer", "like", "ilike", "matches" };
    public static readonly string[] NestedQueryOperators = new[] { "any", "all" };
}
