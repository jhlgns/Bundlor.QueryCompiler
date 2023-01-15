﻿using System.Linq.Expressions;
using static Bundlor.QueryCompiler.QueryCompiler;

namespace Bundlor.QueryCompiler.Tests;

public class ParserTests
{
    private record TestRecord(bool A, bool B, bool C, bool D, bool E, bool F);
    private record ShortcutTest(int Apples, double Bananas, string Password, string Path);

    private static Expression Simplify(Expression expression)
    {
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
            // Here is the simplification: convert MemberExpressions ("input.Field")
            // to a ParameterExpression (just "Field") to that the tests are easier
            // to read and write.
            return Expression.Parameter(member.Type, member.Member.Name);
        }

        return expression;
    }

    private static Expression Parse<T>(string query) =>
        Simplify(CompileFilterExpression<T>(query).expression);

    [Fact]
    public void BinaryOperator()
    {
        var expression = Parse<TestRecord>("a && b");
        var isCorrect = expression is BinaryExpression
        {
            Left: ParameterExpression { Name: "A" },
            NodeType: ExpressionType.And,
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
                NodeType: ExpressionType.And,
                Right: ParameterExpression { Name: "B" },
            },
            NodeType: ExpressionType.Or,
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
            NodeType: ExpressionType.Or,
            Right: BinaryExpression
            {
                Left: ParameterExpression { Name: "B" },
                NodeType: ExpressionType.And,
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
                NodeType: ExpressionType.Or,
                Right: ParameterExpression { Name: "B" },
            },
            NodeType: ExpressionType.Or,
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
                    NodeType: ExpressionType.And,
                    Right: ParameterExpression { Name: "B" },
                },
                NodeType: ExpressionType.And,
                Right: ParameterExpression { Name: "C" },
            },
            NodeType: ExpressionType.Or,
            Right: BinaryExpression
            {
                Left: ParameterExpression { Name: "D" },
                NodeType: ExpressionType.And,
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
                    NodeType: ExpressionType.Or,
                    Right: BinaryExpression
                    {
                        Left: ParameterExpression { Name: "B" },
                        NodeType: ExpressionType.And,
                        Right: ParameterExpression { Name: "C" },
                    },
                },
                NodeType: ExpressionType.Or,
                Right: ParameterExpression { Name: "D" },
            },
            NodeType: ExpressionType.Or,
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
                NodeType: ExpressionType.And,
                Right: ParameterExpression { Name: "B" },
            },
            NodeType: ExpressionType.Or,
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
            NodeType: ExpressionType.And,
            Right: BinaryExpression
            {
                Left: ParameterExpression { Name: "B" },
                NodeType: ExpressionType.Or,
                Right: ParameterExpression { Name: "C" },
            },
        };

        Assert.True(isCorrect);
    }

    [Fact]
    public void StringSpecialBinaryOperators()
    {
        var expression = Parse<ShortcutTest>("path LIke \"*.dll\" or Path matches password");
        var isCorrect = expression is BinaryExpression
        {
            Left: MethodCallExpression
            {
                Method: { Name: "Like" },
                Arguments:
                [
                    ParameterExpression { Name: "Path" },
                    ConstantExpression { Value: "*.dll" },
                ]
            },
            NodeType: ExpressionType.Or,
            Right: MethodCallExpression
            {
                Method: { Name: "Matches" },
                Arguments:
                [
                    ParameterExpression { Name: "Path" },
                    ParameterExpression { Name: "Password" },
                ]
            }
        };

        Assert.True(isCorrect);
    }

    [Fact]
    public void CaseInsensitiveShortcuts()
    {
        // TODO(jh) Nested shortcuts for member access

        var expression = Simplify(
            CompileFilterExpression<ShortcutTest>("a != 0 || paSS == \"c0w4bung4\"").expression);
        var isCorrect = expression is BinaryExpression
        {
            Left: BinaryExpression
            {
                Left: ParameterExpression { Name: "Apples" },
                NodeType: ExpressionType.NotEqual,
                Right: ConstantExpression { Value: 0 },
            },
            NodeType: ExpressionType.Or,
            Right: BinaryExpression
            {
                Left: ParameterExpression { Name: "Password" },
                NodeType: ExpressionType.Equal,
                Right: ConstantExpression { Value: "c0w4bung4" },

            }
        };

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
