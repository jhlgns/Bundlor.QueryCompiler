using System.Linq.Expressions;
using System.Reflection;
using static Bundlor.QueryCompiler.TokenConstants;

namespace Bundlor.QueryCompiler;

internal record ParserContext(
    Dictionary<string, MemberInfo> Members,
    ParameterExpression InputParameter);

// TODO(jh) Array indexing
// TODO(jh) Member access

internal class Parser
{
    private readonly Scanner _scanner;
    private readonly ParserContext _context;

    public Parser(Scanner scanner, ParserContext context) =>
        (_scanner, _context) = (scanner, context);

    internal Expression ParseExpression(int previousPrecendence = 0)
    {
        var left = ParsePrimaryExpression();

        while (true)
        {
            if (_scanner.TryPop(TokenKind.SpecialBinaryOperator) is { } token)
            {
                var specialOperatorInfo = TryGetSpecialOperatorInfo(token.Text)!;
                var specialRight = ParseExpression(int.MaxValue);
                left = Expression.Call(null, specialOperatorInfo.Method, left, specialRight);
                continue;
            }

            if (_scanner.TryPop(TokenKind.NestedQueryOperator) != null)
            {
                _scanner.Require(TokenKind.BlockOpen);
                // TODO(jh) Compile expression of type T of IEnumerable<T> field with the current scanner
                _scanner.Require(TokenKind.BlockClose);
                _scanner.ThrowError(0, "Nested queries are not implemented yet");
                continue;
            }

            token = _scanner.Peek();
            var operatorInfo = TryGetBinaryOperatorInfo(token.Kind);
            if (operatorInfo == null || operatorInfo.Precedence <= previousPrecendence)
                return left;

            _scanner.Pop();

            var right = ParseExpression(operatorInfo.Precedence);
            left = Expression.MakeBinary(operatorInfo.ExpressionType, left, right);
        }
    }

    private Expression ParsePrimaryExpression()
    {
        var token = _scanner.Pop();
        switch (token.Kind)
        {
            case TokenKind.Identifier:
                if (!_context.Members.TryGetValue(token.Text, out var memberInfo))
                {
                    // TODO(jh) Unify the StringComparison types everywhere
                    var shortcutPossibilities = _context.Members
                        .Where(x => x.Key.StartsWith(token.Text, StringComparison.OrdinalIgnoreCase))
                        .ToArray();

                    if (shortcutPossibilities.Length == 0)
                        _scanner.ThrowError(token.Start, $"Member '{token.Text}' not found");

                    if (shortcutPossibilities.Length > 1)
                    {
                        var possibilityEnumeration = string.Join(", ", shortcutPossibilities.Select(x => x.Key));
                        _scanner.ThrowError(token.Start, $"'{token.Text}' is ambiguous: {possibilityEnumeration}");
                    }

                    memberInfo = shortcutPossibilities[0].Value;
                }

                return Expression.MakeMemberAccess(
                    _context.InputParameter,
                    memberInfo);

            case TokenKind.Literal:
                return Expression.Constant(token.LiteralValue!.Value.Opaque);

            case TokenKind.ParenthesisOpen:
                var expression = ParseExpression();
                _scanner.Require(TokenKind.ParenthesisClose);

                return expression;

            case TokenKind.Minus:
            case TokenKind.Not:
                //case TokenKind.BitwiseNot:
                var operatorInfo = TryGetUnaryOperatorInfo(token.Kind)!;
                return Expression.MakeUnary(operatorInfo.ExpressionType, ParseExpression(), null!);

            default:
                _scanner.ThrowError(token.Start, "Invalid expression token");
                break;
        }

        throw new();
    }
}
