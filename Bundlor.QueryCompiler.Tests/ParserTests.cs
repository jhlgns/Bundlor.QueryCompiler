using System.Linq.Expressions;
using static Bundlor.QueryCompiler.QueryCompiler;

namespace Bundlor.QueryCompiler.Tests;

public class ParserTests
{
    private record TestRecord(bool A, bool B, bool C, bool D, bool E, bool F);
    private record ShortcutTest(int Apples, double Bananas, string Password);

    private static Expression Simplify(Expression expression)
    {
        if (expression is BinaryExpression binary)
        {
            return Expression.MakeBinary(
                binary.NodeType,
                Simplify(binary.Left),
                Simplify(binary.Right));
        }

        if (expression is MemberExpression member)
        {
            return Expression.Parameter(member.Type, member.Member.Name);
        }

        return expression;
    }

    private static Expression Parse(string query) =>
        Simplify(CompileFilterExpression<TestRecord>(query).expression);

    [Fact]
    public void BinaryOperator()
    {
        var expression = Parse("a && b");
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
        //                             ||
        //                          __/  \
        //                        &&      |
        //                       /  \     |
        var expression = Parse("a && b || c");
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
        //                        ||
        //                       /  \__
        //                      |      &&
        //                      |     /  \
        var expression = Parse("a || b && c");
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
        //                            ||
        //                          _/  \
        //                        ||     \
        //                       /  \     |
        var expression = Parse("a || b || c");
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
        //                                  ||
        //                               __/  \__
        //                             &&        &&
        //                          __/  \      /  \
        //                        &&      |    |    |
        //                       /  \     |    |    |
        var expression = Parse("a && b && c || d && e");
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
        //                                      ||
        //                                    _/  \
        //                                  ||     \
        //                          _______/  \     |
        //                        ||           |    |
        //                       /  \__        |    |
        //                      |      &&      |    |
        //                      |     /  \     |    |
        var expression = Parse("a || b && c || d || e");
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
    public void CaseInsensitiveShortcuts()
    {
        // TODO(jh) Nested shortcuts

        var expression = Simplify(CompileFilterExpression<ShortcutTest>("a != 0").expression);
        var isCorrect = expression is BinaryExpression
        {
            Left: ParameterExpression { Name: "Apples" },
            NodeType: ExpressionType.NotEqual,
            Right: ConstantExpression { Value: 0 },
        };

        Assert.True(isCorrect);
    }
}
