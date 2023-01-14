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
            var token = scanner.PopToken();
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

    [Fact]
    public void Identifiers()
    {
        var query = "    \n \n\t word \nCAPITAL\t\v\nMiXeD s";
        var expectations = new TokenExpectation[]
        {
            new(TK.Identifier, "word"),
            new(TK.Identifier, "CAPITAL"),
            new(TK.Identifier, "MiXeD"),
            new(TK.Identifier, "s"),
            new(TK.EndOfFile, ""),
        };

        AssertTokensMeetExpectations(query, expectations);
    }

    [Fact]
    public void Literals()
    {
        var query = $"""

        0 123   12345687 1.0 3.1
        3.1415926535897932384626433832795028841971693993751058209
        {Quote}{Quote}"1" " x1" "1x "
        "string
        with newlines
        ""true"
        true false
        
        """;
        var expectations = new TokenExpectation[]
        {
            new(TK.IntegerLiteral, "0", t => t.IntValue == 0),
            new(TK.IntegerLiteral, "123", t => t.IntValue == 123),
            new(TK.IntegerLiteral, "12345687", t => t.IntValue == 12345687),
            new(TK.FloatingPointLiteral, "1.0", t => t.DoubleValue == 1.0),
            new(TK.FloatingPointLiteral, "3.1", t => t.DoubleValue == 3.1),
            new(TK.FloatingPointLiteral, "3.1415926535897932384626433832795028841971693993751058209", t => t.DoubleValue == 3.141592653589793),
            new(TK.StringLiteral, "\"\"", t => t.StringValue == ""),
            new(TK.StringLiteral, "\"1\"", t => t.StringValue == "1"),
            new(TK.StringLiteral, "\" x1\"", t => t.StringValue == " x1"),
            new(TK.StringLiteral, "\"1x \"", t => t.StringValue == "1x "),
            new(TK.StringLiteral, "\"string\r\nwith newlines\r\n\"", t => t.StringValue == "string\r\nwith newlines\r\n"),
            new(TK.StringLiteral, "\"true\"", t => t.StringValue == "true"),
            new(TK.BooleanLiteral, "true", t => t.BoolValue == true),
            new(TK.BooleanLiteral, "false", t => t.BoolValue == false),
            new(TK.EndOfFile, ""),
        };

        AssertTokensMeetExpectations(query, expectations);
    }
}
