namespace Bundlor.QueryCompiler.Tests;

using Xunit.Abstractions;
using TK = TokenKind;

// TODO(jh) Test error reporting
// * Unterminated string literal
// * Invalid int/double literal
// * Unexpected character
// * ...

// TODO(jh) Parenthesis & Block etc.
// TODO(jh) Test for integer under/overflow?

public class ScannerTests
{
    // NOTE(jh) Because otherwise two consecutive string literals with no space inbetween
    // are intepreted as the end of the '"""' multiline string
    private const char Quote = '"';

    private record TokenExpectation(TK Kind, string Text, Func<Token, bool>? Assertion = null);

    private readonly ITestOutputHelper _outputHelper;

    public ScannerTests(ITestOutputHelper outputHelper) => _outputHelper = outputHelper;

    private List<Token> Scan(string query)
    {
        var scanner = new Scanner(query);
        var tokens = new List<Token>();
        while (true)
        {
            var token = scanner.Pop();
            tokens.Add(token);
            if (token.Kind == TK.EndOfFile) break;
        }

        return tokens;
    }

    private void AssertTokensMeetExpectations(string query, TokenExpectation[] expectations)
    {
        var tokens = Scan(query);
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
        AssertTokensMeetExpectations(
            "xxx",
            new TokenExpectation[]
            {
                new(TK.Identifier, "xxx"),
                new(TK.EndOfFile, ""),
            });

        AssertTokensMeetExpectations(
            "$ $$$$",
            new TokenExpectation[]
            {
                new(TK.IteratorVariable, "$"),
                new(TK.IteratorVariable, "$$$$"),
                new(TK.EndOfFile, ""),
            });

        AssertTokensMeetExpectations(
            "    \n \n\t word \nCAPITAL\t\v\nMiXeD s",
            new TokenExpectation[]
            {
                new(TK.Identifier, "word"),
                new(TK.Identifier, "CAPITAL"),
                new(TK.Identifier, "MiXeD"),
                new(TK.Identifier, "s"),
                new(TK.EndOfFile, ""),
            });
    }

    [Fact]
    public void Literals()
    {
        AssertTokensMeetExpectations(
            "\"string\"",
            new TokenExpectation[]
            {
                new(TK.Literal, "\"string\"", t => t.StringValue == "string"),
                new(TK.EndOfFile, ""),
            });
    }

    [Fact]
    public void Integers()
    {
        AssertTokensMeetExpectations(
            "0",
            new TokenExpectation[]
            {
                new(TK.Literal, "0", t => t.IntValue == 0),
                new(TK.EndOfFile, ""),
            });

        AssertTokensMeetExpectations(
            "01",
            new TokenExpectation[]
            {
                new(TK.Literal, "01", t => t.IntValue == 1),
                new(TK.EndOfFile, ""),
            });

        AssertTokensMeetExpectations(
            int.MaxValue.ToString(),
            new TokenExpectation[]
            {
                new(TK.Literal, int.MaxValue.ToString(), t => t.IntValue == int.MaxValue),
                new(TK.EndOfFile, ""),
            });

        var onePastMaxValue = int.MaxValue + 1L;
        Assert.Throws<QueryCompilationException>(() => Scan(onePastMaxValue.ToString()));
    }

    [Fact]
    public void FloatingPoint()
    {
        AssertTokensMeetExpectations(
            "1.2",
            new TokenExpectation[]
            {
                new(TK.Literal, "1.2", t => t.DoubleValue == 1.2),
                new(TK.EndOfFile, ""),
            });

        var piString = "3.1415926535897932384626433832795028841971693993751058209";
        AssertTokensMeetExpectations(
            piString,
            new TokenExpectation[]
            {
                new(TK.Literal, piString, t => t.DoubleValue == 3.141592653589793),
                new(TK.EndOfFile, ""),
            });

        // TODO Special floating point numbers/exponents?
    }

    [Fact]
    public void Boolean()
    {
        AssertTokensMeetExpectations(
            "true",
            new TokenExpectation[]
            {
                new(TK.Literal, "true", t => t.BoolValue == true),
                new(TK.EndOfFile, ""),
            });

        AssertTokensMeetExpectations(
            "false",
            new TokenExpectation[]
            {
                new(TK.Literal, "false", t => t.BoolValue == false),
                new(TK.EndOfFile, ""),
            });
    }

    [Fact]
    public void DateTime()
    {
        // TODO(jh)

        AssertTokensMeetExpectations(
            "2023-01-29 10:16:36.1234",
            new TokenExpectation[]
            {
                new(TK.Literal, "2023-01-29 20:26:36.1234", t => t.DateTimeValue == new DateTime(2023, 1, 29, 20, 26, 36, 1234)),
                new(TK.EndOfFile, ""),
            });
    }

    [Fact]
    public void TimeSpan()
    {
        // TODO(jh)
    }

    [Fact]
    public void Mixed()
    {
        AssertTokensMeetExpectations(
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
                new(TK.Literal, "\"string\nwith newlines\n\"", t => t.StringValue == "string\nwith newlines\n"),
                new(TK.Literal, "\"true\"", t => t.StringValue == "true"),
                new(TK.Literal, "true", t => t.BoolValue == true),
                new(TK.Literal, "false", t => t.BoolValue == false),
                new(TK.EndOfFile, ""),
            });
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


    [Fact]
    public void NiceErrorMessaeges()
    {
        var veryLongWord = "Pneumonoultramicroscopicsilicovolcanoconiosis";
        var input = $"""
         This is a line that does not contain an error.
         This line is different and right over there is an error: xxx here is the error.
         This line also has no error.
         Very long word: {veryLongWord}.
         """.ReplaceLineEndings("\n");
        var errorMessage = "Oh no, there was an error in your query!";
        var scanner = new Scanner(input);

        var exception = Assert.Throws<QueryCompilationException>(() =>
            scanner.ThrowError(new Token(input.IndexOf("xxx"), TK.Identifier, "xxx", null), errorMessage));
        _outputHelper.WriteLine(exception.Message);  // NOTE(jh) For visual inspection using the output window

        exception = Assert.Throws<QueryCompilationException>(() =>
            scanner.ThrowError(new Token(input.IndexOf("the error"), TK.Identifier, "the error", null), errorMessage));
        _outputHelper.WriteLine(exception.Message);

        exception = Assert.Throws<QueryCompilationException>(() =>
            scanner.ThrowError(new Token(input.IndexOf(veryLongWord), TK.Identifier, veryLongWord, null), errorMessage));
        _outputHelper.WriteLine(exception.Message);

        // TODO(jh) Instead of printing do some actual assertions!
    }
}
