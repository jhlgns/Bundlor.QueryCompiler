﻿using System.Diagnostics;
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
    public Scanner(string input) => _input = input.ReplaceLineEndings("\n");

    private readonly string _input;
    private int _position;
    private int _currentTokenStart;

    private char this[int index] => index < _input.Length ? _input[index] : '\0';
    private char Current => this[_position];
    private void EatChar() => ++_position;
    private void UnEatChar() => --_position;
    private bool EatChar(char c)
    {
        if (Current == c)
        {
            EatChar();
            return true;
        }

        return false;
    }

    private string Cut() => Cut(_currentTokenStart);
    private string Cut(int start) => _input.Substring(start, _position - start);

    private Scanner Clone()
    {
        var clone = new Scanner(_input);
        clone._position = _position;
        return clone;
    }

    public Token Peek() => Clone().Pop();

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
        while (char.IsWhiteSpace(Current))
            EatChar();

        _currentTokenStart = _position;

        // Identifier/special binary operator/boolean literal?
        if (char.IsLetter(Current) || Current == '_' || Current == '@')
        {
            EatChar();
            while (char.IsLetter(Current) || char.IsNumber(Current) || Current == '_')
                EatChar();

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

        if (EatChar('$'))
        {
            while (EatChar('$')) ;
            var iteratorVariable = Cut();
            return MakeToken(TokenKind.IteratorVariable, iteratorVariable);
        }

        // Integer or floating point literal or just '.'?
        if (char.IsNumber(Current))
        {
            var clone = Clone();
            if (clone.TryParseTimeSpan() is { } timeSpan)
            {
                _position = clone._position;
                return MakeToken(TokenKind.Literal, Cut(), new LiteralValue { TimeSpanValue = timeSpan });
            }

            clone = Clone();
            if (clone.TryParseDateTime() is { } dateTime)
            {
                _position = clone._position;
                return MakeToken(TokenKind.Literal, Cut(), new LiteralValue { DateTimeValue = dateTime });
            }

            EatChar();
            while (char.IsNumber(Current))
                EatChar();

            if (!EatChar('.'))
            {
                var intString = Cut();
                if (!int.TryParse(intString, out var intValue))
                {
                    ThrowError(_currentTokenStart, _position, "Failed to parse integer");
                }

                return MakeToken(TokenKind.Literal, intString, new LiteralValue() { IntValue = intValue });
            }

            while (char.IsNumber(Current))
                EatChar();

            var doubleString = Cut();
            if (!double.TryParse(doubleString, CultureInfo.InvariantCulture, out var doubleValue))
            {
                ThrowError(_currentTokenStart, _position, "Failed to floating point value");
            }

            return MakeToken(TokenKind.Literal, doubleString, new LiteralValue() { DoubleValue = doubleValue });
        }


        // Just '.' or '.123' floating point literal?
        if (EatChar('.'))
        {
            if (!char.IsNumber(Current))
                return MakeToken(TokenKind.Dot, ".");

            while (char.IsNumber(Current))
                EatChar();

            var doubleString = Cut();
            var doubleValue = double.Parse(doubleString, CultureInfo.InvariantCulture);

            return MakeToken(TokenKind.Literal, doubleString, new LiteralValue() { DoubleValue = doubleValue });
        }

        // String literal?
        if (EatChar('"'))
        {
            while (Current != '"' && Current != '\0')
                EatChar();

            if (Current == '\0')
                ThrowError(_currentTokenStart, _position - 1, "Unterminated string literal");

            EatChar();

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

        if (EatChar('(')) return MakeToken(TokenKind.ParenthesisOpen, "(");
        if (EatChar(')')) return MakeToken(TokenKind.ParenthesisClose, ")");
        if (EatChar('{')) return MakeToken(TokenKind.BlockOpen, "{");
        if (EatChar('}')) return MakeToken(TokenKind.BlockClose, "}");
        if (EatChar('*')) return MakeToken(TokenKind.Multiply, "*");
        if (EatChar('/')) return MakeToken(TokenKind.Divide, "/");
        if (EatChar('+')) return MakeToken(TokenKind.Plus, "+");
        if (EatChar('-')) return MakeToken(TokenKind.Minus, "-");
        if (EatChar('!')) return MakeToken(TokenKind.Not, "!");
        if (EatChar('~')) return MakeToken(TokenKind.BitNot, "~");
        if (EatChar('\0')) return MakeToken(TokenKind.EndOfFile, "");

        ThrowError(_currentTokenStart, _position, $"Unexpected character '{Current}'");
        throw new();
    }

    private int? ParseInt()
    {
        if (!char.IsNumber(Current))
            return null;

        var start = _position;
        while (char.IsNumber(Current))
            EatChar();

        if (!int.TryParse(Cut(start).TrimStart('0'), out var value))
        {
            _position = start;
            return null;
        }

        return value;
    }

    private TimeSpan? TryParseTimeSpan(bool allowDays = true)
    {
        // dd.HH:mm:ss.ffff
        // Days, hours and milliseconds are optional

        Debug.Assert(char.IsNumber(Current));

        int? days = null;
        Span<int?> colonSegments = stackalloc int?[3];
        int numberOfColonSegments = 0;
        int? milliseconds = null;

        var done = false;
        while (!done)  // TODO(jh) What about != '\0'?
        {
            if (ParseInt() is not { } value)
            {
                colonSegments[0] = null;
                break;
            }

            switch (Current)
            {
                case '.':
                    EatChar();

                    // TODO(jh) Something is not right here!

                    if (numberOfColonSegments == 0)
                    {
                    }

                    if (days == null || !allowDays)
                    {
                        if (numberOfColonSegments != 0)
                        {
                            colonSegments[0] = null;
                            done = true;
                        }

                        days = value;
                        break;
                    }

                    // Milliseconds only make sense if there were at least minutes and seconds
                    if (numberOfColonSegments < 2)
                    {
                        done = true;
                        break;
                    }

                    milliseconds = value;
                    done = true;
                    break;

                case ':':
                    if (numberOfColonSegments == 3)
                    {
                        done = true;
                        break;
                    }

                    colonSegments[numberOfColonSegments++] = value;
                    EatChar();
                    break;

                default:
                    done = true;
                    break;
            }
        }

        return colonSegments switch
        {
            [{ } hours, { } minutes, { } seconds] =>
                new TimeSpan(days ?? 0, hours, minutes, seconds, milliseconds ?? 0),
            [{ } minutes, { } seconds, null] =>
                new TimeSpan(days ?? 0, 0, minutes, seconds, milliseconds ?? 0),
            _ => (TimeSpan?)null,
        };
    }

    private DateTime? TryParseDateTime()
    {
        // yyyy-MM-dd HH:mm:ss.fff
        // Time is optional and if given, seconds and milliseconds are optional

        var dateScanner = Clone();
        if (
            dateScanner.ParseInt() is not { } year  || !dateScanner.EatChar('-') ||
            dateScanner.ParseInt() is not { } month || !dateScanner.EatChar('-') ||
            dateScanner.ParseInt() is not { } day)
            return null;

        var result = new DateTime(year, month, day);

        var timeOfDayScanner = dateScanner.Clone();
        if (timeOfDayScanner.EatChar(' ') && timeOfDayScanner.TryParseTimeSpan(allowDays: false) is { } timeOfDay)
        {
            result += timeOfDay;
            _position = timeOfDayScanner._position;
        }
        else
        {
            _position = dateScanner._position;
        }

        return result;
    }

    // TODO(jh) text could be replaced by doing Cut() here, right?
    private Token MakeToken(TokenKind kind, string text, LiteralValue? literalValue = null) =>
        new Token(_currentTokenStart, kind, text, literalValue);
}
