using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using static Bundlor.QueryCompiler.TokenConstants;

namespace Bundlor.QueryCompiler;

internal record ParserContext(
    int Depth,
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

    internal Expression ParseExpression(int previousPrecendence = int.MinValue)
    {
        var left = ParsePrimaryExpression();

        while (true)
        {
            if (_scanner.TryPop(TokenKind.NestedQueryOperator) is { } token)
            {
                _scanner.Require(TokenKind.BlockOpen);

                // TODO(jh) Make function for this

                var elementType = left.Type.GetInterfaces()
                    .FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    ?.GetGenericArguments()?.Single()
                    ?? throw new InvalidOperationException($"{left.Type} is not a subclass of IEnumerable<>");
                var filterExpression = QueryCompiler.CompileFilterExpression(elementType, _scanner, _context.Depth + 1);
                var enumerableType = typeof(IEnumerable<>).MakeGenericType(elementType);
                var lambdaParameterType = typeof(Func<,>).MakeGenericType(elementType, typeof(bool));
                var method = token.Text.ToLower() switch
                {
                    "any" => typeof(Enumerable).GetMethod("Any", BindingFlags.Static, new[] { enumerableType, lambdaParameterType })!,
                    "all" => typeof(Enumerable).GetMethod("All", BindingFlags.Static, new[] { enumerableType, lambdaParameterType })!,
                    _ => throw new InvalidOperationException($"Could not find nested query operator function for {token.Text}"),
                };

                _scanner.Require(TokenKind.BlockClose);

                left = Expression.Call(left, method, filterExpression);

                continue;
            }

            token = _scanner.Peek();
            var operatorInfo = TryGetBinaryOperatorInfo(token.Kind);
            if (operatorInfo == null || operatorInfo.Precedence <= previousPrecendence)
                return left;

            _scanner.Pop();

            var right = ParseExpression(operatorInfo.Precedence);

            if (operatorInfo.ExpressionType != null)
            {
                left = Expression.MakeBinary(operatorInfo.ExpressionType.Value, left, right);
            }
            else
            {
                left = Expression.Call(null, operatorInfo.Method!, left, right);
            }
        }
    }

    private Expression ParsePrimaryExpression()
    {
        var token = _scanner.Pop();
        switch (token.Kind)
        {
            case TokenKind.Identifier:
                var memberInfo = GetMemberInfo(token);
                return Expression.MakeMemberAccess(_context.InputParameter, memberInfo);

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
                _scanner.ThrowError(token.Start, $"Invalid expression token {token.Kind}");
                break;
        }

        throw new();
    }

    private MemberInfo GetMemberInfo(Token token)
    {
        if (_context.Members.TryGetValue(token.Text, out var memberInfo))
            return memberInfo;

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

        return shortcutPossibilities[0].Value;
    }
}
