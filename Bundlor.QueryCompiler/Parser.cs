using System.Linq.Expressions;
using System.Reflection;
using static Bundlor.QueryCompiler.TokenConstants;

namespace Bundlor.QueryCompiler;

internal record ParserContext(
    Dictionary<string, MemberInfo> Members,
    ParameterExpression InputParameter);

// TODO(jh) Unary for -, !, ~ etc.

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
            var token = _scanner.PeekToken();
            var operatorInfo = TryGetBinaryOperatorInfo(token.Kind);
            if (operatorInfo == null || operatorInfo.Precedence <= previousPrecendence)
                return left;

            _scanner.PopToken();

            var right = ParseExpression(operatorInfo.Precedence);
            left = Expression.MakeBinary(operatorInfo.ExpressionType, left, right);
        }
    }

    private Expression ParsePrimaryExpression()
    {
        var token = _scanner.PopToken();
        switch (token.Kind)
        {
            case TokenKind.Identifier:
                if (!_context.Members.TryGetValue(token.Text, out var memberInfo))
                {
                    // TODO(jh) Unify the StringComparison types everywhere
                    var shortcutPossibilities = _context.Members
                        .Where(x => x.Key.StartsWith(token.Text, StringComparison.OrdinalIgnoreCase))
                        .ToArray();  // TODO(jh) .Take(2)?

                    if (shortcutPossibilities.Length == 0)
                        throw new Exception($"TODO Member {token.Text} not found");

                    if (shortcutPossibilities.Length > 1)
                        throw new Exception($"TODO {token.Text} is ambiguous: {string.Join(", ", shortcutPossibilities)}");

                    memberInfo = shortcutPossibilities[0].Value;
                }

                return Expression.MakeMemberAccess(
                    _context.InputParameter,
                    memberInfo);

            case TokenKind.StringLiteral:
            case TokenKind.IntegerLiteral:
            case TokenKind.FloatingPointLiteral:
            case TokenKind.BooleanLiteral:
                return Expression.Constant(token.OpaqueLiteralValue);

            case TokenKind.ParenthesisOpen:
                var expression = ParseExpression();
                if (_scanner.PopToken().Kind != TokenKind.ParenthesisClose)
                    throw new Exception("TODO(jh) scanner.Require(predicate, errorMessage)");
                return expression;

            case TokenKind.Minus:
            case TokenKind.Not:
            case TokenKind.BitwiseNot:
                var operatorInfo = TryGetUnaryOperatorInfo(token.Kind)!;
                return Expression.MakeUnary(operatorInfo.ExpressionType, ParseExpression(), null!);

            // TODO(jh)

            default:
                throw new Exception("TODO(jh) Error message for invalid expression token");
        }
    }
}
