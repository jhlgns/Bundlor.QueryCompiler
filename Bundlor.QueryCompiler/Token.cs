using System.Linq.Expressions;
using System.Reflection;

namespace Bundlor.QueryCompiler;

internal enum TokenKind
{
    // Comparison
    Equal,
    NotEqual,
    LessThan,
    LessThanOrEqual,
    GreaterThan,
    GreaterThanOrEqual,

    // Logical
    And,
    Or,

    // Arithmetical
    Plus,
    Minus,  // Also unary
    Multiply,
    Divide,

    // Unary
    Not,

    // Other
    Identifier,
    SpecialIdentifier,  // like '@now'
    SpecialBinaryOperator,  // like 'matches'
    NestedQueryOperator,  // like 'any'
    Literal,
    ParenthesisOpen,
    ParenthesisClose,
    BlockOpen,
    BlockClose,
    EndOfFile,
}

internal readonly struct LiteralValue
{
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

    private readonly TimeSpan? _timeSpanValue;
    public TimeSpan? TimeSpanValue
    {
        get => _timeSpanValue ?? throw new InvalidOperationException();
        init => _timeSpanValue = value;
    }

    public object Opaque
    {
        get
        {
            if (_stringValue != null) return _stringValue;
            if (_intValue != null) return _intValue;
            if (_doubleValue != null) return _doubleValue;
            if (_boolValue != null) return _boolValue;
            if (_timeSpanValue != null) return _timeSpanValue;  // TODO(jh) Might not be compatible with Expression.Constant()
            throw new InvalidOperationException();
        }
    }
}

internal readonly struct Token
{
    public Token(int start, TokenKind kind, string text, LiteralValue? literalValue)
    {
        Start = start;
        Kind = kind;
        Text = text;
        LiteralValue = literalValue;
    }

    public readonly int Start;
    public readonly TokenKind Kind;
    public readonly string Text;
    public readonly LiteralValue? LiteralValue;

    public string StringValue => LiteralValue!.Value.StringValue!;
    public int IntValue => LiteralValue!.Value.IntValue!.Value;
    public double DoubleValue => LiteralValue!.Value.DoubleValue!.Value;
    public bool BoolValue => LiteralValue!.Value.BoolValue!.Value;
    public TimeSpan TimeSpanValue => LiteralValue!.Value.TimeSpanValue!.Value;

    public override string ToString() => Text;
}

internal record class BinaryOperatorInfo(
    string Operator,
    string Alternate,
    TokenKind TokenKind,
    ExpressionType ExpressionType,
    int Precedence);

// TODO(jh) Alternate
internal record class UnaryOperatorInfo(
    char Operator,
    TokenKind TokenKind,
    ExpressionType ExpressionType);

internal record class SpecialBinaryOperatorInfo(
    string Operator,
    MethodInfo Method);

internal static class TokenConstants
{
    public static readonly BinaryOperatorInfo[] BinaryOperators = new BinaryOperatorInfo[]
    {
        new("*", "mul", TokenKind.Multiply, ExpressionType.Multiply, 120),
        new("/", "div", TokenKind.Divide, ExpressionType.Divide, 120),

        new("+", "add", TokenKind.Plus, ExpressionType.Add, 110),
        new("-", "sub", TokenKind.Minus, ExpressionType.Subtract, 110),

        new("==", "eq", TokenKind.Equal, ExpressionType.Equal, 100),
        new("!=", "ne", TokenKind.NotEqual, ExpressionType.NotEqual, 100),

        // NOTE(jh) 'XXXOrEqual' must come before XXX for the scanner to work
        new("<=", "le", TokenKind.LessThanOrEqual, ExpressionType.LessThanOrEqual, 90),
        new("<", "lt", TokenKind.LessThan, ExpressionType.LessThan, 90),
        new(">=", "ge", TokenKind.GreaterThanOrEqual, ExpressionType.GreaterThanOrEqual, 90),
        new(">", "gt", TokenKind.GreaterThan, ExpressionType.GreaterThan, 90),

        new("&&", "and", TokenKind.And, ExpressionType.And, 80),

        new("||", "or", TokenKind.Or, ExpressionType.Or, 70),
    };

    private static readonly UnaryOperatorInfo[] UnaryOperators = new UnaryOperatorInfo[]
    {
        new('-', TokenKind.Minus, ExpressionType.Negate),
        new('!', TokenKind.Not, ExpressionType.Not),
        //new('~', TokenKind.BitwiseNot, ExpressionType.Not),  // TODO(jh) Make this also a string match operator
    };

    private static readonly SpecialBinaryOperatorInfo[] SpecialBinaryOperators = new SpecialBinaryOperatorInfo[]
    {
        /*
        new("older", typeof(SpecialBinaryOperatorFunctions).GetMethod("Older")!),
        new("newer", typeof(SpecialBinaryOperatorFunctions).GetMethod("Newer")!),
        */
        new("like", typeof(SpecialBinaryOperatorFunctions).GetMethod("Like")!),
        //new("ilike", typeof(SpecialBinaryOperatorFunctions).GetMethod("Ilike")!),
        new("matches", typeof(SpecialBinaryOperatorFunctions).GetMethod("Matches")!),
    };

    public static readonly string[] NestedQueryOperators = new[] { "any", "all" };

    public static BinaryOperatorInfo? TryGetBinaryOperatorInfo(TokenKind tokenKind) =>
        BinaryOperators.FirstOrDefault(x => x.TokenKind == tokenKind);

    public static BinaryOperatorInfo? TryGetBinaryOperatorInfoByAlternate(string value) =>
        BinaryOperators.FirstOrDefault(x =>
            x.Alternate.Equals(value, StringComparison.OrdinalIgnoreCase));

    public static UnaryOperatorInfo? TryGetUnaryOperatorInfo(TokenKind tokenKind) =>
        UnaryOperators.FirstOrDefault(x => x.TokenKind == tokenKind);

    public static SpecialBinaryOperatorInfo? TryGetSpecialOperatorInfo(string text) =>
        SpecialBinaryOperators.FirstOrDefault(x =>
            x.Operator.Equals(text, StringComparison.OrdinalIgnoreCase));
}
