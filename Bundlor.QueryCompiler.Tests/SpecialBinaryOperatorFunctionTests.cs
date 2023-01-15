namespace Bundlor.QueryCompiler.Tests;

public class SpecialBinaryOperatorFunctionTests
{
    [Theory]
    [InlineData("", "", true)]
    [InlineData("*", "", true)]
    [InlineData("*****", "", true)]
    [InlineData("*a", "", false)]
    [InlineData("*a", "a", true)]
    [InlineData("*a*", "a", true)]
    [InlineData("a*", "a", true)]
    [InlineData("abc*", "a", false)]
    [InlineData("abc*", "abc*", true)]
    [InlineData("a*c", "abc", true)]
    [InlineData("a*c", "abbbbbbc", true)]
    [InlineData("a*c*", "abbbbbbc*", true)]
    [InlineData("*a*c*", "abbbbbbc", true)]
    [InlineData("*a*c*", "xxxabbbbbbc", true)]
    [InlineData("*a*b*c*d*e*f*", "a x b1c...depppffff", true)]
    [InlineData("?", "", false)]
    [InlineData("?", "a", true)]
    [InlineData("?", "aa", false)]
    [InlineData("??", "aa", true)]
    [InlineData("bun*r", "bundlor", true)]
    [InlineData("bun?r", "bundlor", false)]
    public void Like(string pattern, string input, bool expectedToMatch)
    {
        Assert.Equal(expectedToMatch, SpecialBinaryOperatorFunctions.Like(input, pattern));
    }

    // TODO(jh) Regex tests
}
