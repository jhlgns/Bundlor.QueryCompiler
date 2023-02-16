using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using static Bundlor.QueryCompiler.TokenConstants;

namespace Bundlor.QueryCompiler;

// TODO(jh) Make StringComparison and automatic member expansion configurable

internal class ParserContext
{
    public ParserContext(ParserContext? parentContext, Type type, ParameterExpression inputParameter)
    {
        Depth = (parentContext?.Depth + 1) ?? 0;
        ParentContext = parentContext;
        Members = GetMembers(type).ToList();
        InputParameter = inputParameter;
    }

    public readonly int Depth;
    public readonly ParserContext? ParentContext;
    public readonly List<MemberInfo> Members;
    public readonly ParameterExpression InputParameter;

    public static IEnumerable<MemberInfo> GetMembers(Type type) =>
        type.GetProperties().Cast<MemberInfo>().Concat(type.GetFields());
}

internal class Parser
{
    private readonly Scanner _scanner;
    private readonly ParserContext _context;

    public Parser(Scanner scanner, ParserContext context) =>
        (_scanner, _context) = (scanner, context);

    internal Expression ParseExpression(int previousPrecendence = int.MinValue)
    {
        var left = ParsePrimaryExpression();
        left = ParseSuffixExpression(left);

        while (true)
        {
            if (_scanner.TryPop(TokenKind.NestedQueryOperator) is { } token)
            {
                left = ParseNestedQueryExpression(token, left);
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

    private Expression ParseNestedQueryExpression(Token token, Expression left)
    {
        _scanner.Require(TokenKind.BlockOpen);

        var elementType = left.Type.GetInterfaces()
            .FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            ?.GetGenericArguments()?.Single()
            ?? throw new InvalidOperationException($"{left.Type} does not implement IEnumerable<>");
        var filterExpression = QueryCompiler.CompileFilterExpression(elementType, _scanner, _context);
        var enumerableType = typeof(IEnumerable<>).MakeGenericType(elementType);
        var predicateType = typeof(Func<,>).MakeGenericType(elementType, typeof(bool));
        var method = token.Text.ToLower() switch
        {
            "any" => typeof(Enumerable).GetMethods()
                .First(x => x.Name == "Any" && x.GetParameters().Length == 2 && x.IsGenericMethod && x.GetGenericArguments().Length == 1)
                .MakeGenericMethod(elementType)!,
            "all" => typeof(Enumerable).GetMethods()
                .First(x => x.Name == "All" && x.GetParameters().Length == 2 && x.IsGenericMethod && x.GetGenericArguments().Length == 1)
                .MakeGenericMethod(elementType)!,
            "count" => typeof(Enumerable).GetMethods()
                .First(x => x.Name == "Count" && x.GetParameters().Length == 2 && x.IsGenericMethod && x.GetGenericArguments().Length == 1)
                .MakeGenericMethod(elementType)!,
            _ => throw new InvalidOperationException($"Could not find nested query operator function for {token.Text}"),
        };

        _scanner.Require(TokenKind.BlockClose);

        return Expression.Call(method, left, filterExpression);
    }

    private Expression ParseSuffixExpression(Expression left)
    {
        if (_scanner.TryPop(TokenKind.Dot) != null)
        {
            var memberIdentifier = _scanner.Require(TokenKind.Identifier);
            var memberInfo = FindMemberInfo(_scanner, ParserContext.GetMembers(left.Type), memberIdentifier);
            return ParseSuffixExpression(Expression.MakeMemberAccess(left, memberInfo));
        }

        // TODO(jh) Array access

        return left;
    }

    private Expression ParsePrimaryExpression()
    {
        var token = _scanner.Pop();
        switch (token.Kind)
        {
            case TokenKind.Identifier:
                if (token.Text.Equals("@now", StringComparison.OrdinalIgnoreCase))
                {
                    return Expression.Constant(DateTime.Now);
                }

                // We handle method calls here instead of in ParseSuffixExpression
                // since we need the identifier to find the method
                if (_scanner.TryPop(TokenKind.ParenthesisOpen) != null)
                {
                    var arguments = new List<Expression>();

                    do arguments.Add(ParseExpression());
                    while (_scanner.TryPop(TokenKind.Comma) != null);

                    _scanner.Require(TokenKind.ParenthesisClose);

                    var method = typeof(Functions)
                        .GetMethods()
                        .Where(x =>
                            x.Name.Equals(token.Text, StringComparison.OrdinalIgnoreCase) &&
                            x.GetParameters().Length == arguments.Count())
                        .SingleOrDefault();
                    if (method == null)
                    {
                        _scanner.ThrowError(token, $"Method '{token.Text}' is not defined");
                        throw new();
                    }

                    return ParseSuffixExpression(Expression.Call(null, method, arguments));
                }

                var memberInfo = FindMemberInfo(_scanner, _context.Members, token);
                return Expression.MakeMemberAccess(_context.InputParameter, memberInfo);

            case TokenKind.IteratorVariable:
                // $ is the current iterator variable, $$ is the parent, ...
                var context = _context;
                for (int i = 1; i < token.Text.Length; ++i)
                {
                    if (_context.ParentContext == null)
                    {
                        _scanner.ThrowError(
                            token,
                            $"Iterator variable of depth {token.Text.Length} exceeds the current expression depth of {i}");
                    }

                    context = _context.ParentContext;
                }

                return context.InputParameter;

            case TokenKind.Literal:
                return Expression.Constant(token.LiteralValue!.Value.Opaque);

            case TokenKind.ParenthesisOpen:
                var expression = ParseExpression();
                _scanner.Require(TokenKind.ParenthesisClose);

                return expression;

            case TokenKind.Minus:
            case TokenKind.Not:
            case TokenKind.BitNot:
                var operatorInfo = TryGetUnaryOperatorInfo(token.Kind)!;
                return Expression.MakeUnary(operatorInfo.ExpressionType, ParseExpression(), null!);

            default:
                _scanner.ThrowError(token, $"Invalid expression token {token.Kind}");
                break;
        }

        throw new();
    }

    private static MemberInfo FindMemberInfo(Scanner scanner, IEnumerable<MemberInfo> members, Token token)
    {
        Debug.Assert(token.Kind == TokenKind.Identifier);
        Debug.Assert(token.Text.Length >= 1);

        var memberInfo = members.FirstOrDefault(x => x.Name.Equals(token.Text, StringComparison.OrdinalIgnoreCase));
        if (memberInfo != null)
            return memberInfo;

        var shortcutPossibilities = members
            .Where(x => x.Name.StartsWith(token.Text, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (shortcutPossibilities.Length == 0)
            scanner.ThrowError(token, $"Member '{token.Text}' not found");

        if (shortcutPossibilities.Length > 1)
        {
            var head = shortcutPossibilities.Take(shortcutPossibilities.Length - 1).Select(x => $"'{x.Name}'");
            var last = $"'{shortcutPossibilities.Last().Name}'";
            var possibilityEnumeration = $"{string.Join(", ", head)} or {last}";
            scanner.ThrowError(token, $"'{token.Text}' is ambiguous: could be {possibilityEnumeration}");
        }

        return shortcutPossibilities[0];
    }
}
