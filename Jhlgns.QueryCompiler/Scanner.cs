using System.Diagnostics;
using static Jhlgns.QueryCompiler.TokenConstants;

namespace Jhlgns.QueryCompiler;

internal struct Scanner
{
    public Scanner(string input) => _input = input;

    private readonly string _input;
    private int _position;
    private int _currentTokenStart;
    private bool _eofReached;

    private char this[int index] => index < _input.Length ? _input[index] : '\0';
    private char Current => this[_position];
    private string Cut() => _input.Substring(_currentTokenStart, _position - _currentTokenStart);

    public Token PopToken()
    {
        Debug.Assert(!_eofReached);

        while (char.IsWhiteSpace(Current))
            ++_position;

        _currentTokenStart = _position;

        // Identifier or special binary operator?
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

            return new Token(TokenKind.Identifier, word);
        }

        // Number? (integer or floating point)
        if (char.IsNumber(Current))
        {
            while (char.IsNumber(Current))
                ++_position;

            // TODO(jh) Floating point
            var numberString = Cut();
            var value = int.Parse(numberString);  // TODO(jh) Try & error message

            return new Token(TokenKind.IntegerLiteral, numberString) { IntValue = value };
        }

        // Binary operator?
        foreach (var binaryOperatorDef in BinaryOperators)
        {
            var isMatch = true;
            for (var i = 0; i < binaryOperatorDef.Operator.Length; ++i)
            {
                if (binaryOperatorDef.Operator[i] != this[_position + i])
                    isMatch = false;
            }

            if (!isMatch)
                continue;

            _position += binaryOperatorDef.Operator.Length;

            return new Token(TokenKind.BinaryOperator, Cut());
        }

        switch (Current)
        {
            case '(': ++_position; return new Token(TokenKind.ParenthesisOpen, "(");
            case ')': ++_position; return new Token(TokenKind.ParenthesisClose, ")");
            case '{': ++_position; return new Token(TokenKind.BlockOpen, "{");
            case '}': ++_position; return new Token(TokenKind.BlockClose, "}");
            case '\0': _eofReached = true; return new Token(TokenKind.EndOfFile, "");
            default: throw new Exception("TODO unexpected character " + Current);
        }
    }
}
