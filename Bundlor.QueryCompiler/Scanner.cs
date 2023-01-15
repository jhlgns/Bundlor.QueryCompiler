using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using static Bundlor.QueryCompiler.TokenConstants;

namespace Bundlor.QueryCompiler;

public class QueryCompilationException : Exception
{
    public QueryCompilationException(string? message = null, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

internal class Scanner
{
    public Scanner(string input) => _input = input;
    public Scanner(Scanner other)
    {
        _input = other._input;
        _position = other._position;
        _currentTokenStart = other._currentTokenStart;
        EofReached = other.EofReached;
    }

    private readonly string _input;
    private int _position;
    private int _currentTokenStart;
    public bool EofReached { private set; get; }

    private char this[int index] => index < _input.Length ? _input[index] : '\0';
    private char Current => this[_position];
    private void Next() => ++_position;
    private string Cut() => _input.Substring(_currentTokenStart, _position - _currentTokenStart);

    public Token Peek()
    {
        var copy = new Scanner(this);
        return copy.Pop();
    }

    public Token? TryPop(TokenKind kind)
    {
        if (Peek().Kind != kind)
            return null;

        return Pop();
    }

    public Token Require(TokenKind kind)
    {
        var token = Pop();
        if (token.Kind != kind)
            ThrowError(token.Start, $"Expected {kind}");

        return token;
    }

    [DoesNotReturn]
    public void ThrowError(int position, string message) =>
        throw new QueryCompilationException($"Query compilation failed, error at position {position}: {message}");

    public void EnsureEofReached()
    {
        if (!EofReached && Peek().Kind != TokenKind.EndOfFile)
            ThrowError(_position, "Extraneous tokens that could not be parsed");
    }

    public Token Pop()
    {
        Debug.Assert(!EofReached);

        while (char.IsWhiteSpace(Current))
            Next();

        _currentTokenStart = _position;

        // Identifier/special binary operator/boolean literal?
        if (char.IsLetter(Current) || Current == '_')
        {
            while (char.IsLetter(Current) || char.IsNumber(Current) || Current == '_')
                Next();

            var word = Cut();

            if (TryGetSpecialOperatorInfo(word) != null)
                return MakeToken(TokenKind.SpecialBinaryOperator, word);

            if (NestedQueryOperators.Any(x => x.Equals(word, StringComparison.OrdinalIgnoreCase)))
                return MakeToken(TokenKind.NestedQueryOperator, word);

            if (TryGetBinaryOperatorInfoByAlternate(word) is { } operatorInfo)
                return MakeToken(operatorInfo.TokenKind, word);

            return word switch
            {
                "true" => MakeToken(TokenKind.Literal, word, new LiteralValue() { BoolValue = true }),
                "false" => MakeToken(TokenKind.Literal, word, new LiteralValue() { BoolValue = false }),
                _ => MakeToken(TokenKind.Identifier, word),
            };
        }

        // Integer or floating point literal?
        if (char.IsNumber(Current))
        {
            while (char.IsNumber(Current))
                Next();

            switch (Current)
            {
                case '.':
                    Next();
                    while (char.IsNumber(Current))
                        Next();

                    var doubleString = Cut();
                    var doubleValue = double.Parse(doubleString, CultureInfo.InvariantCulture);

                    return MakeToken(TokenKind.Literal, doubleString, new LiteralValue() { DoubleValue = doubleValue });

                case ':':
                // 0000:00:00.0000
                // TODO(jh) Parse TimeSpan literal

                case '/':
                // yyyy/MM/dd
                // TODO(jh) Parse DateTime literal

                default:
                    var intString = Cut();
                    var intValue = int.Parse(intString);  // TODO(jh) Try & error message

                    return MakeToken(TokenKind.Literal, intString, new LiteralValue() { IntValue = intValue });
            }
        }

        // String literal?
        if (Current == '"')
        {
            Next();
            while (Current != '"' && Current != '\0')
                Next();

            if (Current == '\0')
                ThrowError(_position, "Unterminated string literal");

            Next();

            var stringWithQuotes = Cut();
            return MakeToken(TokenKind.Literal, stringWithQuotes, new LiteralValue()
            {
                StringValue = stringWithQuotes.Substring(1, stringWithQuotes.Length - 2)
            });
        }

        // Binary operator?
        foreach (var operatorInfo in BinaryOperators)
        {
            var isMatch = true;
            for (var i = 0; i < operatorInfo.Operator.Length; ++i)
            {
                if (operatorInfo.Operator[i] != this[_position + i])
                    isMatch = false;
            }

            if (!isMatch)
                continue;

            _position += operatorInfo.Operator.Length;

            return MakeToken(operatorInfo.TokenKind, Cut());
        }

        switch (Current)
        {
            case '(': Next(); return MakeToken(TokenKind.ParenthesisOpen, "(");
            case ')': Next(); return MakeToken(TokenKind.ParenthesisClose, ")");
            case '{': Next(); return MakeToken(TokenKind.BlockOpen, "{");
            case '}': Next(); return MakeToken(TokenKind.BlockClose, "}");
            case '*': Next(); return MakeToken(TokenKind.Multiply, "*");
            case '/': Next(); return MakeToken(TokenKind.Divide, "/");
            case '+': Next(); return MakeToken(TokenKind.Plus, "+");
            case '-': Next(); return MakeToken(TokenKind.Minus, "-");
            case '!': Next(); return MakeToken(TokenKind.Not, "!");
            //case '~': Next(); return MakeToken(TokenKind.BitwiseNot, "~");
            case '\0': EofReached = true; return MakeToken(TokenKind.EndOfFile, "");
            default: ThrowError(_position, $"Unexpected character '{Current}'"); break;
        }

        throw new();
    }

    // TODO(jh) text could be replaced by doing Cut() here, right?
    private Token MakeToken(TokenKind kind, string text, LiteralValue? literalValue = null) =>
        new Token(_currentTokenStart, kind, text, literalValue);
}
