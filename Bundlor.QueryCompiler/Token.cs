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

    // String matching
    MatchesRegex,
    DoesNotMatchRegex,
    Like,
    NotLike,

    // Logical
    And,
    Or,
    Not,

    // Arithmetical
    Plus,
    Minus,
    Multiply,
    Divide,
    Modulo,
    BitAnd,
    BitOr,
    BitXor,
    BitNot,
    LeftShift,
    RightShift,

    // Other
    Identifier,
    IteratorVariable,
    Dot,
    NestedQueryOperator,
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

    private readonly DateTime? _dateTimeValue;
    public DateTime? DateTimeValue
    {
        get => _dateTimeValue ?? throw new InvalidOperationException();
        init => _dateTimeValue = value;
    }

    public object Opaque
    {
        get
        {
            if (_stringValue != null) return _stringValue;
            if (_intValue != null) return _intValue;
            if (_doubleValue != null) return _doubleValue;
            if (_boolValue != null) return _boolValue;
            if (_timeSpanValue != null) return _timeSpanValue;
            if (_dateTimeValue != null) return _dateTimeValue;
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
    public DateTime DateTimeValue => LiteralValue!.Value.DateTimeValue!.Value;

    public override string ToString() => Text;
}

internal record class BinaryOperatorInfo(
    string Operator,
    TokenKind TokenKind,
    ExpressionType? ExpressionType,
    MethodInfo? Method,
    int Precedence);

internal record class UnaryOperatorInfo(
    char Operator,
    TokenKind TokenKind,
    ExpressionType ExpressionType);

internal static class TokenConstants
{
    // TODO(jh) What do we need the TokenKind for in the BinaryOperatorInfo???
    //  Could we not just make the token kind = 'operator'?

    // NOTE(jh) https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/#operator-precedence
    // NOTE(jh) Must be sorted by operator length so the scanner does not early-out when trying to match the text.
    public static readonly BinaryOperatorInfo[] BinaryOperators = new BinaryOperatorInfo[]
    {
        new("*", TokenKind.Multiply, ExpressionType.Multiply, null, 100),
        new("/", TokenKind.Divide, ExpressionType.Divide, null, 100),
        new("%", TokenKind.Modulo, ExpressionType.Modulo, null, 100),

        new("+", TokenKind.Plus, ExpressionType.Add, null, 90),
        new("-", TokenKind.Minus, ExpressionType.Subtract, null, 90),

        new("<<", TokenKind.LeftShift, ExpressionType.LeftShift, null, 80),
        new(">>", TokenKind.RightShift, ExpressionType.RightShift, null, 80),

        new("<", TokenKind.LessThan, ExpressionType.LessThan, null, 70),
        new(">", TokenKind.GreaterThan, ExpressionType.GreaterThan, null, 70),
        new("<=", TokenKind.LessThanOrEqual, ExpressionType.LessThanOrEqual, null, 70),
        new(">=", TokenKind.GreaterThanOrEqual, ExpressionType.GreaterThanOrEqual, null, 70),

        new("==", TokenKind.Equal, ExpressionType.Equal, null, 60),
        new("!=", TokenKind.NotEqual, ExpressionType.NotEqual, null, 60),

        // TODO(jh) I really want to make text-based alternates for this (a like "*.dll" or
        // a matches "\w+\s\w")
        new("=?", TokenKind.Like, null, GetBinaryMethod("Like"), 60),
        new("!?", TokenKind.NotLike, null, GetBinaryMethod("NotLike"), 60),
        new("=~", TokenKind.MatchesRegex, null, GetBinaryMethod("MatchesRegex"), 60),
        new("!~", TokenKind.DoesNotMatchRegex, null, GetBinaryMethod("DoesNotMatchRegex"), 60),

        new("&", TokenKind.BitAnd, ExpressionType.And, null, 40),

        new("^", TokenKind.BitXor, ExpressionType.ExclusiveOr, null, 30),

        new("|", TokenKind.BitOr, ExpressionType.Or, null, 20),

        new("&&", TokenKind.And, ExpressionType.AndAlso, null, 10),

        new("||", TokenKind.Or, ExpressionType.OrElse, null, 0),
    }.OrderByDescending(x => x.Operator.Length).ToArray();

    private static MethodInfo GetBinaryMethod(string name) =>
        typeof(SpecialBinaryOperatorFunctions).GetMethod(name)!;

    private static readonly UnaryOperatorInfo[] UnaryOperators = new UnaryOperatorInfo[]
    {
        new('-', TokenKind.Minus, ExpressionType.Negate),
        new('!', TokenKind.Not, ExpressionType.Not),
        new('~', TokenKind.BitNot, ExpressionType.Not),
    };

    public static readonly string[] NestedQueryOperators = new[] { "any", "all", "count" };

    public static BinaryOperatorInfo? TryGetBinaryOperatorInfo(TokenKind tokenKind) =>
        BinaryOperators.FirstOrDefault(x => x.TokenKind == tokenKind);

    public static UnaryOperatorInfo? TryGetUnaryOperatorInfo(TokenKind tokenKind) =>
        UnaryOperators.FirstOrDefault(x => x.TokenKind == tokenKind);}
