namespace Jhlgns.QueryCompiler.Tests;

public class ScannerTests
{
    [Fact]
    public void BasicScanningWorks()
    {
        var text = "    \n \n\t word \nCAPITAL\t\vMiXeD && || != ";
        var scanner = new Scanner(text);

        var tokens = new List<Token>();
        while (true)
        {
            var token = scanner.PopToken();
            tokens.Add(token);
            if (token.Kind == TokenKind.EndOfFile) break;
        }

        Assert.True(tokens is
            [{ Kind: TokenKind.Identifier, Text: "word" },
            { Kind: TokenKind.Identifier, Text: "CAPITAL" },
            { Kind: TokenKind.Identifier, Text: "MiXeD" },
            { Kind: TokenKind.BinaryOperator, Text: "&&" },
            { Kind: TokenKind.BinaryOperator, Text: "||" },
            { Kind: TokenKind.BinaryOperator, Text: "!=" },
            { Kind: TokenKind.EndOfFile, Text: "" },]);
    }
}
