using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
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
    public Scanner(string input) => _input = input.Replace("\r", "");
    public Scanner(Scanner other)
    {
        _input = other._input;
        _position = other._position;
        _currentTokenStart = other._currentTokenStart;
        _currentLine = other._currentLine;
        EofReached = other.EofReached;
    }

    private readonly string _input;
    private int _position;
    private int _currentTokenStart;
    private int _currentLine;
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
            ThrowError(token, $"Expected '{kind}'");

        return token;
    }

    public void EnsureEofReached() => Require(TokenKind.EndOfFile);

    [DoesNotReturn]
    public void ThrowError(Token token, string message) =>
        ThrowError(token.Start, token.Start + token.Text.Length, message);

    [DoesNotReturn]
    private void ThrowError(int start, int end, string message)
    {
        Debug.Assert(start >= 0 && start <= _input.Length);
        Debug.Assert(end >= 0 && end <= _input.Length && end >= start);

        string line;
        string marker;

        if (!_input.Any())
        {
            line = "(empty input)";
            marker = "";
        }
        else
        {
            // Extract line that encloses the specified position
            var lineStart = start;
            // TODO(jh) What if _input empty?
            while (lineStart > 1 && _input[lineStart - 1] != '\n')
                --lineStart;

            var (relativeStart, relativeEnd) = (start - lineStart, end - lineStart);

            var lineEnd = end;
            while (lineEnd < _input.Length && _input[lineEnd] != '\n')
                ++lineEnd;

            line = _input.Substring(lineStart, lineEnd - lineStart);

            if (relativeStart > 20)
            {
                line = $"...{line.Substring(relativeStart - 20)}";
                relativeStart = 23;
            }

            if (line.Length - relativeEnd > 20)
                line = $"{line.Substring(0, relativeEnd + 20)}...";

            Debug.Assert(!line.Contains('\n'));

            marker = new string(' ', relativeStart) + new string('^', end - start);
        }

        var errorMessage = $"""
            Query compilation failed, error at position {start}:
              {message}

            Surrounding text:
              {line}
              {marker}
            """;

        throw new QueryCompilationException(errorMessage);
    }

    public Token Pop()
    {
        Debug.Assert(!EofReached);

        while (char.IsWhiteSpace(Current))
            Next();

        _currentTokenStart = _position;

        // Identifier/special binary operator/boolean literal?
        if (char.IsLetter(Current) || Current == '_' || Current == '@')
        {
            Next();
            while (char.IsLetter(Current) || char.IsNumber(Current) || Current == '_')
                Next();

            var word = Cut();

            if (NestedQueryOperators.Any(x => x.Equals(word, StringComparison.OrdinalIgnoreCase)))
                return MakeToken(TokenKind.NestedQueryOperator, word);

            return word switch
            {
                "true" => MakeToken(TokenKind.Literal, word, new LiteralValue() { BoolValue = true }),
                "false" => MakeToken(TokenKind.Literal, word, new LiteralValue() { BoolValue = false }),
                _ => MakeToken(TokenKind.Identifier, word),
            };
        }

        if (Current == '$')
        {
            Next();
            while (Current == '$')
                Next();
            var iteratorVariable = Cut();
            return MakeToken(TokenKind.IteratorVariable, iteratorVariable);
        }

        // Integer or floating point literal or just '.'?
        if (char.IsNumber(Current))
        {
            Next();
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
                    // TODO(jh) Parse TimeSpan literal
                    // dd.HH:mm:ss.ffff
                    var first = Cut();
                    if (char.IsNumber(Current))
                        ThrowError(_currentTokenStart, _position, "Malformed timespan");

                    throw new NotImplementedException();

                case '/':
                    // TODO(jh) Parse DateTime literal
                    // yyyy/MM/dd
                    throw new NotImplementedException();

                default:
                    var intString = Cut();
                    var intValue = int.Parse(intString);  // TODO(jh) Try & error message

                    return MakeToken(TokenKind.Literal, intString, new LiteralValue() { IntValue = intValue });
            }
        }


        // Just '.' or '.123' floating point literal?
        if (Current == '.')
        {
            Next();
            if (!char.IsNumber(Current))
                return MakeToken(TokenKind.Dot, ".");

            while (char.IsNumber(Current))
                Next();

            var doubleString = Cut();
            var doubleValue = double.Parse(doubleString, CultureInfo.InvariantCulture);

            return MakeToken(TokenKind.Literal, doubleString, new LiteralValue() { DoubleValue = doubleValue });
        }

        // String literal?
        if (Current == '"')
        {
            Next();
            while (Current != '"' && Current != '\0')
                Next();

            if (Current == '\0')
                ThrowError(_currentTokenStart, _position - 1, "Unterminated string literal");

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
            //case '.': Next(); return MakeToken(TokenKind.Dot, ".");
            case '~': Next(); return MakeToken(TokenKind.BitNot, "~");
            case '\0': EofReached = true; return MakeToken(TokenKind.EndOfFile, "");
            default: ThrowError(_currentTokenStart, _position, $"Unexpected character '{Current}'"); break;
        }

        throw new();
    }

    /*
    // 3h40m10s1234
    private TimeSpan? ParseTimeSpan(string input, int position, out int end)
    {
        end = position;

        //var regex = new Regex(@"(\d+h?)(\d+m?)()");
        if (!char.IsNumber(input[position]))
            return null;

        var start = position;
        while (char.IsNumber(input[position]) && position < input.Length)
            ++position;

        var quantifier = int.Parse(input.Substring(start, position - start));

        switch (input[position])
        {
            case 'h': return ParseTimeSpan(initial + TimeSpan.FromHours(quantifier), 'h', input, position, out end);
            case 'm': return ParseTimeSpan(initial + TimeSpan.FromMinutes(quantifier), 'm', input, position, out end);
            case 's': return ParseTimeSpan(initial + TimeSpan.FromSeconds(quantifier), 's', input, position, out end);
            default:
                switch (last)
                {
                    'h' => TimeSpan.FromMinutes(quantifier),
                    'm' => TimeSpan.FromSeconds(quantifier),
                    's' => TimeSpan.FromMilliseconds(quantifier),
                    _ => ThrowError(start, position, "Malformed timespan"),
                }
        };
    }
    */

    // TODO(jh) text could be replaced by doing Cut() here, right?
    private Token MakeToken(TokenKind kind, string text, LiteralValue? literalValue = null) =>
        new Token(_currentTokenStart, kind, text, literalValue);
}
