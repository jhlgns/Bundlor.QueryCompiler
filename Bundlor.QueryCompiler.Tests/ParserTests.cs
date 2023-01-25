using System.Linq.Expressions;
using static Bundlor.QueryCompiler.QueryCompiler;

namespace Bundlor.QueryCompiler.Tests;

public class ParserTests
{
    private record TestRecord(bool A, bool B, bool C, bool D, bool E, bool F);
    private record ShortcutTest(int Apples, double Bananas, string Password, string Path);
    private record SomethingWithIntList(int Value, List<int> Integers);
    private record SomethingWithDateTime(DateTime CreatedAt);

    private static Expression Simplify(Expression expression)
    {
        if (expression is LambdaExpression lambda)
        {
            return Simplify(lambda.Body);
        }

        if (expression is BinaryExpression binary)
        {
            return Expression.MakeBinary(
                binary.NodeType,
                Simplify(binary.Left),
                Simplify(binary.Right));
        }

        if (expression is MethodCallExpression methodCall)
        {
            return Expression.Call(
                methodCall.Object == null ? null : Simplify(methodCall.Object),
                methodCall.Method,
                methodCall.Arguments.Select(Simplify));
        }

        if (expression is MemberExpression member)
        {
            // Here is the simplification: convert MemberExpressions ("x0.Field")
            // to a ParameterExpression (just "Field") to that the tests are easier
            // to read and write.
            return Expression.Parameter(member.Type, member.Member.Name);
        }

        return expression;
    }

    private static Expression Parse<T>(string query) =>
        Simplify(CompileFilterExpression<T>(query));

    [Fact]
    public void BinaryOperator()
    {
        var expression = Parse<TestRecord>("a && b");
        var isCorrect = expression is BinaryExpression
        {
            Left: ParameterExpression { Name: "A" },
            NodeType: ExpressionType.AndAlso,
            Right: ParameterExpression { Name: "B" },
        };

        Assert.True(isCorrect);
    }

    [Fact]
    public void BinaryOperatorPrecendence1()
    {
        //                                         ||
        //                                      __/  \
        //                                    &&      |
        //                                   /  \     |
        var expression = Parse<TestRecord>("a && b || c");
        var isCorrect = expression is BinaryExpression
        {
            Left: BinaryExpression
            {
                Left: ParameterExpression { Name: "A" },
                NodeType: ExpressionType.AndAlso,
                Right: ParameterExpression { Name: "B" },
            },
            NodeType: ExpressionType.OrElse,
            Right: ParameterExpression { Name: "C" },
        };

        Assert.True(isCorrect);
    }

    [Fact]
    public void BinaryOperatorPrecendence2()
    {
        //                                    ||
        //                                   /  \__
        //                                  |      &&
        //                                  |     /  \
        var expression = Parse<TestRecord>("a || b && c");
        var isCorrect = expression is BinaryExpression
        {
            Left: ParameterExpression { Name: "A" },
            NodeType: ExpressionType.OrElse,
            Right: BinaryExpression
            {
                Left: ParameterExpression { Name: "B" },
                NodeType: ExpressionType.AndAlso,
                Right: ParameterExpression { Name: "C" },
            },
        };

        Assert.True(isCorrect);
    }

    [Fact]
    public void BinaryOperatorPrecendence3()
    {
        //                                        ||
        //                                      _/  \
        //                                    ||     \
        //                                   /  \     |
        var expression = Parse<TestRecord>("a || b || c");
        var isCorrect = expression is BinaryExpression
        {
            Left: BinaryExpression
            {
                Left: ParameterExpression { Name: "A" },
                NodeType: ExpressionType.OrElse,
                Right: ParameterExpression { Name: "B" },
            },
            NodeType: ExpressionType.OrElse,
            Right: ParameterExpression { Name: "C" },
        };

        Assert.True(isCorrect);
    }

    [Fact]
    public void ComplexExpressionTree1()
    {
        //                                              ||
        //                                           __/  \__
        //                                         &&        &&
        //                                      __/  \      /  \
        //                                    &&      |    |    |
        //                                   /  \     |    |    |
        var expression = Parse<TestRecord>("a && b && c || d && e");
        var isCorrect = expression is BinaryExpression
        {
            Left: BinaryExpression
            {
                Left: BinaryExpression
                {
                    Left: ParameterExpression { Name: "A" },
                    NodeType: ExpressionType.AndAlso,
                    Right: ParameterExpression { Name: "B" },
                },
                NodeType: ExpressionType.AndAlso,
                Right: ParameterExpression { Name: "C" },
            },
            NodeType: ExpressionType.OrElse,
            Right: BinaryExpression
            {
                Left: ParameterExpression { Name: "D" },
                NodeType: ExpressionType.AndAlso,
                Right: ParameterExpression { Name: "E" },
            },
        };

        Assert.True(isCorrect);
    }

    [Fact]
    public void ComplexExpressionTree2()
    {
        //                                                  ||
        //                                                _/  \
        //                                              ||     \
        //                                      _______/  \     |
        //                                    ||           |    |
        //                                   /  \__        |    |
        //                                  |      &&      |    |
        //                                  |     /  \     |    |
        var expression = Parse<TestRecord>("a || b && c || d || e");
        var isCorrect = expression is BinaryExpression
        {
            Left: BinaryExpression
            {
                Left: BinaryExpression
                {
                    Left: ParameterExpression { Name: "A" },
                    NodeType: ExpressionType.OrElse,
                    Right: BinaryExpression
                    {
                        Left: ParameterExpression { Name: "B" },
                        NodeType: ExpressionType.AndAlso,
                        Right: ParameterExpression { Name: "C" },
                    },
                },
                NodeType: ExpressionType.OrElse,
                Right: ParameterExpression { Name: "D" },
            },
            NodeType: ExpressionType.OrElse,
            Right: ParameterExpression { Name: "E" },
        };

        Assert.True(isCorrect);
    }

    [Fact]
    public void Parenthesis1()
    {
        //                                          ||
        //                                       __/  \
        //                                     &&      |
        //                                    /  \     |
        var expression = Parse<TestRecord>("(a && b) || c");
        var isCorrect = expression is BinaryExpression
        {
            Left: BinaryExpression
            {
                Left: ParameterExpression { Name: "A" },
                NodeType: ExpressionType.AndAlso,
                Right: ParameterExpression { Name: "B" },
            },
            NodeType: ExpressionType.OrElse,
            Right: ParameterExpression { Name: "C" },
        };

        Assert.True(isCorrect);
    }

    [Fact]
    public void Parenthesis2()
    {
        //                                    ||
        //                                   /  \___
        //                                  |       &&
        //                                  |      /  \
        var expression = Parse<TestRecord>("a && (b || c)");
        var isCorrect = expression is BinaryExpression
        {
            Left: ParameterExpression { Name: "A" },
            NodeType: ExpressionType.AndAlso,
            Right: BinaryExpression
            {
                Left: ParameterExpression { Name: "B" },
                NodeType: ExpressionType.OrElse,
                Right: ParameterExpression { Name: "C" },
            },
        };

        Assert.True(isCorrect);
    }

    [Fact]
    public void StringSpecialBinaryOperators()
    {
        var expression = Parse<ShortcutTest>("path =? \"*.dll\" || Path =~ password");
        var isCorrect = expression is BinaryExpression
        {
            Left: MethodCallExpression
            {
                Method.Name: nameof(SpecialBinaryOperatorFunctions.Like),
                Arguments:
                [
                    ParameterExpression { Name: "Path" },
                    ConstantExpression { Value: "*.dll" },
                ]
            },
            NodeType: ExpressionType.OrElse,
            Right: MethodCallExpression
            {
                Method: { Name: nameof(SpecialBinaryOperatorFunctions.MatchesRegex) },
                Arguments:
                [
                    ParameterExpression { Name: "Path" },
                    ParameterExpression { Name: "Password" },
                ]
            },
        };

        Assert.True(isCorrect);
    }

    [Fact]
    public void CaseInsensitiveShortcuts()
    {
        // TODO(jh) Nested shortcuts for member access

        var expression = Simplify(
            CompileFilterExpression<ShortcutTest>("a != 0 || paSS == \"c0w4bung4\""));
        var isCorrect = expression is BinaryExpression
        {
            Left: BinaryExpression
            {
                Left: ParameterExpression { Name: "Apples" },
                NodeType: ExpressionType.NotEqual,
                Right: ConstantExpression { Value: 0 },
            },
            NodeType: ExpressionType.OrElse,
            Right: BinaryExpression
            {
                Left: ParameterExpression { Name: "Password" },
                NodeType: ExpressionType.Equal,
                Right: ConstantExpression { Value: "c0w4bung4" },
            },
        };

        Assert.True(isCorrect);
    }

    [Fact]
    public void IteratorVariable()
    {
        var expression = CompileFilterExpression<int>("$ == 2023").Body;
        var isCorrect = expression is BinaryExpression
        {
            Left: ParameterExpression { Name: "it0" },
            NodeType: ExpressionType.Equal,
            Right: ConstantExpression { Value: 2023 },
        };
        Assert.True(isCorrect);

        Assert.Equal(
            CompileFilterExpression<TestRecord>("$.a").Body.ToString(),
            CompileFilterExpression<TestRecord>("a").Body.ToString());

        var ex = Assert.Throws<QueryCompilationException>(() =>
            CompileFilterExpression<int>("$$ == 2023"));
        Assert.Contains("depth", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NestedIteratorVariable()
    {
        // it0 => it0.Integers.Any((int it1) => it1 == it0.Value)
        var expression = CompileFilterExpression<SomethingWithIntList>("integers any { $ == $$.value }").Body;
        var isCorrect = expression is MethodCallExpression
        {
            Method.Name: "Any",
            Arguments:
            [
                MemberExpression
                {
                    Expression: ParameterExpression { Name: "it0" },
                    Member.Name: "Integers",
                },
                LambdaExpression
                {
                    Parameters: [ ParameterExpression { Type.Name: "Int32" , Name: "it1" } ],
                    Body: BinaryExpression
                    {
                        Left: ParameterExpression { Name: "it1" },
                        NodeType: ExpressionType.Equal,
                        Right: MemberExpression
                        {
                            Expression: ParameterExpression { Name: "it0" },
                            Member.Name: "Value",
                        }
                    },
                },
            ]
        };
        Assert.True(isCorrect);
    }

    [Fact]
    public void Now()
    {
        var expression = Simplify(CompileFilterExpression<SomethingWithDateTime>("createdat == @now"));
        var isCorrect = expression is BinaryExpression
        {
            Left: ParameterExpression { Name: "CreatedAt" },
            NodeType: ExpressionType.Equal,
            Right: ConstantExpression { Value: DateTime d }
        } && DateTime.Now - d < TimeSpan.FromMilliseconds(10);
        Assert.True(isCorrect);
    }

    [Theory]
    [InlineData("(a != b")]
    [InlineData("a && b)")]
    [InlineData("a ! && b)")]
    [InlineData("==")]
    [InlineData("a ||")]
    public void ErrorReporting(string query)
    {
        Assert.Throws<QueryCompilationException>(() =>
            CompileFilterExpression<TestRecord>(query));
    }
}
