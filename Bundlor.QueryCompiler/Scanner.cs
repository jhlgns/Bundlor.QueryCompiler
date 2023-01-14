using System.Diagnostics;
using System.Globalization;
using static Bundlor.QueryCompiler.TokenConstants;

namespace Bundlor.QueryCompiler;

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
    private string Cut() => _input.Substring(_currentTokenStart, _position - _currentTokenStart);

    public Token PeekToken()
    {
        var copy = new Scanner(this);
        return copy.PopToken();
    }

    public Token? TryPopToken(TokenKind kind)
    {
        if (PeekToken().Kind != kind)
            return null;

        return PopToken();
    }

    public Token PopToken()
    {
        Debug.Assert(!EofReached);

        while (char.IsWhiteSpace(Current))
            ++_position;

        _currentTokenStart = _position;

        // Identifier/special binary operator/boolean literal?
        if (char.IsLetter(Current) || Current == '_')
        {
            while (char.IsLetter(Current) || char.IsNumber(Current) || Current == '_')
                ++_position;

            var word = Cut();


            // TODO(jh) not?
            if (SpecialBinaryOperators.Any(x => x.Equals(word, StringComparison.OrdinalIgnoreCase)))
                return new Token(TokenKind.SpecialBinaryOperator, word);

            if (NestedQueryOperators.Any(x => x.Equals(word, StringComparison.OrdinalIgnoreCase)))
                return new Token(TokenKind.NestedQueryOperator, word);

            return word switch
            {
                "true" => new Token(TokenKind.BooleanLiteral, word) { BoolValue = true },
                "false" => new Token(TokenKind.BooleanLiteral, word) { BoolValue = false },
                _ => new Token(TokenKind.Identifier, word),
            };
        }

        // Integer or floating point literal?
        if (char.IsNumber(Current))
        {
            while (char.IsNumber(Current))
                ++_position;

            if (Current != '.')
            {
                var intString = Cut();
                var intValue = int.Parse(intString);  // TODO(jh) Try & error message

                return new Token(TokenKind.IntegerLiteral, intString) { IntValue = intValue };
            }

            ++_position;
            while (char.IsNumber(Current))
                ++_position;

            var doubleString = Cut();
            var doubleValue = double.Parse(doubleString, CultureInfo.InvariantCulture);

            return new Token(TokenKind.FloatingPointLiteral, doubleString) { DoubleValue = doubleValue };
        }

        // String literal?
        if (Current == '"')
        {
            ++_position;
            while (Current != '"' && Current != '\0')
                ++_position;
            ++_position;

            if (Current == '\0')
                throw new Exception("TODO unterminated string literal");

            var stringWithQuotes = Cut();
            return new Token(TokenKind.StringLiteral, stringWithQuotes)
            {
                StringValue = stringWithQuotes.Substring(1, stringWithQuotes.Length - 2)
            };
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

            return new Token(operatorInfo.TokenKind, Cut());
        }

        switch (Current)
        {
            case '(': ++_position; return new Token(TokenKind.ParenthesisOpen, "(");
            case ')': ++_position; return new Token(TokenKind.ParenthesisClose, ")");
            case '{': ++_position; return new Token(TokenKind.BlockOpen, "{");
            case '}': ++_position; return new Token(TokenKind.BlockClose, "}");
            case '-': ++_position; return new Token(TokenKind.Minus, "-");
            case '!': ++_position; return new Token(TokenKind.Not, "!");
            case '~': ++_position; return new Token(TokenKind.BitwiseNot, "~");
            case '\0': EofReached = true; return new Token(TokenKind.EndOfFile, "");
            default: throw new Exception($"TODO unexpected character '{Current}'");
        }
    }
}
