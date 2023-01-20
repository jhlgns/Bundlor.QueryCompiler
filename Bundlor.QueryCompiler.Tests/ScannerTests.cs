namespace Bundlor.QueryCompiler.Tests;

using TK = TokenKind;

// TODO(jh) Test error reporting
// * Unterminated string literal
// * Invalid int/double literal
// * Unexpected character
// * ...

// TODO(jh) Parenthesis & Block etc.

public class ScannerTests
{
    // NOTE(jh) Because otherwise two consecutive string literals with no space inbetween
    // are intepreted as the end of the '"""' multiline string
    private const char Quote = '"';

    private record TokenExpectation(TK Kind, string Text, Func<Token, bool>? Assertion = null);

    private void AssertTokensMeetExpectations(string query, TokenExpectation[] expectations)
    {
        var scanner = new Scanner(query);
        var tokens = new List<Token>();
        while (true)
        {
            var token = scanner.Pop();
            tokens.Add(token);
            if (token.Kind == TK.EndOfFile) break;
        }

        Assert.Equal(tokens.Count(), expectations.Length);

        for (var i = 0; i < tokens.Count(); ++i)
        {
            var expectation = expectations[i];
            Assert.Equal(expectation.Kind, tokens[i].Kind);
            Assert.Equal(expectation.Text, tokens[i].Text);
            Assert.True(expectation.Assertion?.Invoke(tokens[i]) ?? true);
        }
    }


    public static IEnumerable<object[]> IdentifierCases() =>
        new object[][]
        {
            new object[]
            {
                "xxx",
                new TokenExpectation[]
                {
                    new(TK.Identifier, "xxx"),
                    new(TK.EndOfFile, ""),
                },
            },
            new object[]
            {
                "    \n \n\t word \nCAPITAL\t\v\nMiXeD s",
                new TokenExpectation[]
                {
                    new(TK.Identifier, "word"),
                    new(TK.Identifier, "CAPITAL"),
                    new(TK.Identifier, "MiXeD"),
                    new(TK.Identifier, "s"),
                    new(TK.EndOfFile, ""),
                },
            }
        };

    [Theory]
    [MemberData(nameof(IdentifierCases))]
    public void Identifiers(string query, object[] expectationsOpaque)
    {
        var expectations = expectationsOpaque.Cast<TokenExpectation>().ToArray();
        AssertTokensMeetExpectations(query, expectations);
    }

    // TODO(jh) Inline this and other theories
    public static IEnumerable<object[]> LiteralsCases() =>
        new object[][]
        {
            new object[]
            {
                "\"string\"",
                new TokenExpectation[]
                {
                    new(TK.Literal, "\"string\"", t => t.StringValue == "string"),
                    new(TK.EndOfFile, ""),
                }
            },
            new object[]
            {
                "1234",
                new TokenExpectation[]
                {
                    new(TK.Literal, "1234", t => t.IntValue == 1234),
                    new(TK.EndOfFile, ""),
                }
            },
            new object[]
            {
                "1.234",
                new TokenExpectation[]
                {
                    new(TK.Literal, "1.234", t => t.DoubleValue == 1.234),
                    new(TK.EndOfFile, ""),
                }
            },
            new object[]
            {
                "true",
                new TokenExpectation[]
                {
                    new(TK.Literal, "true", t => t.BoolValue == true),
                    new(TK.EndOfFile, ""),
                }
            },
            new object[]
            {
                $"""
                0 123   12345687 1.0 3.1
                3.1415926535897932384626433832795028841971693993751058209
                {Quote}{Quote}"1"
                "string
                with newlines
                ""true"
                true false
                """,
                new TokenExpectation[]
                {
                    new(TK.Literal, "0", t => t.IntValue == 0),
                    new(TK.Literal, "123", t => t.IntValue == 123),
                    new(TK.Literal, "12345687", t => t.IntValue == 12345687),
                    new(TK.Literal, "1.0", t => t.DoubleValue == 1.0),
                    new(TK.Literal, "3.1", t => t.DoubleValue == 3.1),
                    new(TK.Literal, "3.1415926535897932384626433832795028841971693993751058209",
                        t => t.DoubleValue == 3.141592653589793),
                    new(TK.Literal, "\"\"", t => t.StringValue == ""),
                    new(TK.Literal, "\"1\"", t => t.StringValue == "1"),
                    new(TK.Literal, "\"string\r\nwith newlines\r\n\"", t => t.StringValue == "string\r\nwith newlines\r\n"),
                    new(TK.Literal, "\"true\"", t => t.StringValue == "true"),
                    new(TK.Literal, "true", t => t.BoolValue == true),
                    new(TK.Literal, "false", t => t.BoolValue == false),
                    new(TK.EndOfFile, ""),
                }
            }
        };

    [Theory]
    [MemberData(nameof(LiteralsCases))]
    public void Literals(string query, object[] expectationsOpaque)
    {
        var expectations = expectationsOpaque.Cast<TokenExpectation>().ToArray();
        AssertTokensMeetExpectations(query, expectations);
    }

    [Theory]
    [InlineData("\"unterminated string")]
    [InlineData("' unexpected character")]
    public void ErrorReporting(string query)
    {
        Assert.Throws<QueryCompilationException>(() =>
        {
            var scanner = new Scanner(query);
            while (scanner.Pop().Kind != TK.EndOfFile) { }
        });
    }
}
