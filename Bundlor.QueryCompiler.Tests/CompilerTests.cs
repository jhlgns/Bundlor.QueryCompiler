using static Bundlor.QueryCompiler.QueryCompiler;

namespace Bundlor.QueryCompiler.Tests;

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
        public string String1;
        public string String2 { get; set; }
        public int Integer1;
        public int Integer2 { get; set; }
        public double Double1;
        public double Double2 { get; set; }
        public bool Bool1;
        public bool Bool2 { get; set; }
        public StructField Record;
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

    public static IEnumerable<object[]> ComparisonCases() =>
        new object[][]
        {
            new object[] { new SampleStruct { Integer1 = 1 }, "integer1 == 1", true },
            new object[] { new SampleStruct { Integer1 = 1 }, "integer1 == 0", false },
            new object[] { new SampleStruct { Integer1 = 1 }, "integer1 == 782349", false },
            new object[] { new SampleStruct { Integer1 = 1 }, "integer1 > 0", true },
            new object[] { new SampleStruct { Integer1 = 1 }, "integer1 > 1", false},
            new object[] { new SampleStruct { Integer1 = 1 }, "integer1 > -111", true},
        };

    [Theory]
    [MemberData(nameof(ComparisonCases))]
    public void StringComparison(SampleStruct record, string query, bool expectedFilterResult)
    {
        var filter = Compile<SampleStruct>(query);
        var filterResult = filter(record);
        Assert.Equal(expectedFilterResult, filterResult);
    }
}
