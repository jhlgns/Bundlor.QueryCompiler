using static Bundlor.QueryCompiler.QueryCompiler;

namespace Bundlor.QueryCompiler.Tests;

// TODO(jh) When use [Theory] and when use [Fact]?
// It seems to me that facts are generally easier to write and read...

public class CompilerTests
{
    private record BooleanTriple(bool A, bool B, bool C = false);
    private record TruthTableRow(bool ExpectedValue, int A, int B, int C);

    public struct StructField
    {
        public int NumberOfBananas;
        public double EatPercent;
    }

    public struct SampleStruct
    {
        public string FirstName;
        public string LastName { get; set; }
        public int LoginAttempts;
        public int NumberOfTeeth { get; set; }
        public double CoolnessFactor;
        public double WordsPerMinute { get; set; }
        public bool NamesAreDifficult;
        public bool Flagged { get; set; }
        public StructField Record;
        public List<StructField> List;
    }

    private void AssertTruthTable(string query, TruthTableRow[] truthTable)
    {
        var filter = Compile<BooleanTriple>(query);
        foreach (var row in truthTable)
        {
            var record = new BooleanTriple(row.A != 0, row.B != 0, row.C != 0);
            var filterResult = filter(record);
            Assert.Equal(row.ExpectedValue, filterResult);
        }
    }

    [Fact]
    public void Conjunction()
    {
        AssertTruthTable(
            "a && b",
            new TruthTableRow[]
            {
                new(false, 0, 0, 0),
                new(false, 0, 1, 0),
                new(false, 1, 0, 0),
                new(true,  1, 1, 0),
            });
    }

    [Fact]
    public void Disjunction()
    {
        AssertTruthTable(
            "a || b",
            new TruthTableRow[]
            {
                new(false, 0, 0, 0),
                new(true,  0, 1, 0),
                new(true,  1, 0, 0),
                new(true,  1, 1, 0),
            });
    }

    [Fact]
    public void AOr_BAndC()
    {
        AssertTruthTable(
            "a || b && c",
            new TruthTableRow[]
            {
                new(false, 0, 0, 1),
                new(false, 0, 1, 0),
                new(true,  0, 1, 1),
                new(true,  1, 0, 0),
                new(true,  1, 0, 1),
                new(true,  1, 1, 0),
                new(true,  1, 1, 1),
            });
    }

    [Fact]
    public void AAndB_OrC()
    {
        AssertTruthTable(
            "a && b || c",
            new TruthTableRow[]
            {
                new(true,  0, 0, 1),
                new(false, 0, 1, 0),
                new(true,  0, 1, 1),
                new(false, 1, 0, 0),
                new(true,  1, 0, 1),
                new(true,  1, 1, 0),
                new(true,  1, 1, 1),
            });
    }

    [Fact]
    public void Equality()
    {
        AssertTruthTable(
            "a == b",
            new TruthTableRow[]
            {
                new(true,  0, 0, 1),
                new(false, 0, 1, 0),
                new(false, 0, 1, 1),
                new(false, 1, 0, 0),
                new(false, 1, 0, 1),
                new(true,  1, 1, 0),
                new(true,  1, 1, 1),
            });
    }

    [Fact]
    public void Negation()
    {
        AssertTruthTable(
            "!(a == b)",
            new TruthTableRow[]
            {
                new(false, 0, 0, 1),
                new(true,  0, 1, 0),
                new(true,  0, 1, 1),
                new(true,  1, 0, 0),
                new(true,  1, 0, 1),
                new(false, 1, 1, 0),
                new(false, 1, 1, 1),
            });
    }

    public static IEnumerable<object[]> ComparisonCases() =>
        new object[][]
        {
            new object[] { new SampleStruct { LoginAttempts = 123 }, "loginatt == 123", true },
            new object[] { new SampleStruct { LoginAttempts = 123 }, "loginatt == 729", false },
            new object[] { new SampleStruct { LoginAttempts = 123 }, "loginatt != 0",   true },
            new object[] { new SampleStruct { LoginAttempts = 123 }, "loginatt != 123", false },
            new object[] { new SampleStruct { LoginAttempts = 123 }, "loginatt > -100", true },
            new object[] { new SampleStruct { LoginAttempts = 123 }, "loginatt >  123", false },
            new object[] { new SampleStruct { LoginAttempts = 123 }, "loginatt >= 123", true },
            new object[] { new SampleStruct { LoginAttempts = 123 }, "loginatt >= 124", false },
            new object[] { new SampleStruct { LoginAttempts = 123 }, "loginatt < 9999", true },
            new object[] { new SampleStruct { LoginAttempts = 123 }, "loginatt <  123", false },
            new object[] { new SampleStruct { LoginAttempts = 123 }, "loginatt <= 123", true },
            new object[] { new SampleStruct { LoginAttempts = 123 }, "loginatt <= 122", false },
            new object[]
            {
                new SampleStruct { LoginAttempts = 1234, FirstName = "jan" },
                """first == "jan" && loginatt >= 122""",
                true,
            },
            new object[]
            {
                new SampleStruct { LoginAttempts = 1234, FirstName = "jan" },
                """first != "jan" && loginatt < 122""",
                false,
            },
        };

    [Theory]
    [MemberData(nameof(ComparisonCases))]
    public void ComparisonOperators(SampleStruct record, string query, bool expectedFilterResult)
    {
        var filter = Compile<SampleStruct>(query);
        var filterResult = filter(record);
        Assert.Equal(expectedFilterResult, filterResult);
    }

    [Fact]
    public void Like()
    {
        // TODO(jh) Make it so that if the last token was a special string operator
        // the next token gets interpreted as a string literal until the next whitespace (?)
        var filter = Compile<SampleStruct>("first =? \"*n\" || last =? \"k*a*\"");
        Assert.True(filter(new SampleStruct { FirstName = "jan" }));
        Assert.True(filter(new SampleStruct { FirstName = "jahn" }));
        Assert.False(filter(new SampleStruct { FirstName = "jam" }));
        Assert.True(filter(new SampleStruct { LastName = "karl" }));
        Assert.False(filter(new SampleStruct { LastName = "carl" }));
        Assert.True(filter(new SampleStruct { LastName = "karateee!" }));
    }

    [Fact]
    public void Matches()
    {
        var filter = Compile<SampleStruct>("first =~ \"^[0-9a-z]*_\\s+(a){1,2}$\"");
        Assert.False(filter(new SampleStruct { FirstName = "" }));
        Assert.True(filter(new SampleStruct { FirstName = "_ a" }));
        Assert.True(filter(new SampleStruct { FirstName = "a0b1c2_  \taa" }));
        Assert.False(filter(new SampleStruct { FirstName = "a0b1c2_  aaa" }));
        Assert.False(filter(new SampleStruct { FirstName = "a__ \ta" }));
    }

    [Fact]
    public void NestedQueryOperator()
    {
        var filter = Compile<SampleStruct>("list any { number > 3 }");
        Assert.True(filter(
            new SampleStruct
            {
                List = new()
                {
                    new StructField { NumberOfBananas = 0 },
                    new StructField { NumberOfBananas = 0 },
                    new StructField { NumberOfBananas = 4 },
                }
            }));

        Assert.False(filter(
            new SampleStruct
            {
                List = new()
                {
                    new StructField { NumberOfBananas = 0 },
                    new StructField { NumberOfBananas = 0 },
                    new StructField { NumberOfBananas = 2 },
                }
            }));
    }

    [Fact]
    public void AllInOne()
    {
        // TODO(jh)
        // Enough poking around! Let's do one where all the features are combined
        // in one query.
        // This query should feature:
        // * Binary operator precedence
        // * Parenthesis
        // * Comparisons
        // * Binary operator alternate
        // * Case insensitive (nested) shortcuts
        // * All kinds of literals
        // * Member comparison (with each other, not only literals)
        // * Nested query operators
        // * Special binary operators (string match/regex & DateTime)
        // * Unary operators
    }
}
