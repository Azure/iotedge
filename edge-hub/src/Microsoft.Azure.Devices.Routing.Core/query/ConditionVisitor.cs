// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Routing.Core.Query
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using Antlr4.Runtime;
    using Microsoft.Azure.Devices.Routing.Core.Query.Builtins;
    using Microsoft.Azure.Devices.Routing.Core.Query.Types;
    using Microsoft.Azure.Devices.Routing.Core.Util;

    public class ConditionVisitor : ConditionBaseVisitor<Expression>
    {
        static readonly Expression[] NoArgs = new Expression[0];
        static readonly Expression UndefinedExpression = Expression.Constant(Undefined.Instance);

        readonly ParameterExpression message;
        readonly ErrorListener errors;
        readonly Route route;
        readonly RouteCompilerFlags routeCompilerFlags;

        static readonly IDictionary<string, IBuiltin> Builtins = new Dictionary<string, IBuiltin>(StringComparer.OrdinalIgnoreCase)
        {
            // math
            { "abs", new Abs() },
            { "exp", new Exp() },
            { "power", new Power() },
            { "square", new Square() },
            { "ceiling", new Ceiling() },
            { "floor", new Floor() },
            { "sign", new Sign() },
            { "sqrt", new Sqrt() },

            // type checking
            { "as_number", new AsNumber() },
            { "is_bool", new IsBool() },
            { "is_defined", new IsDefined() },
            { "is_null", new IsNull() },
            { "is_number", new IsNumber() },
            { "is_string", new IsString() },

            // strings
            { "concat", new Concat() },
            { "length", new Length() },
            { "lower", new Lower() },
            { "upper", new Upper() },
            { "substring", new Substring() },
            { "index_of", new IndexOf() },
            { "starts_with", new StartsWith() },
            { "ends_with", new EndsWith() },
            { "contains", new Contains() },

            // body query
            { "twin_change_includes", new TwinChangeIncludes() },
        };

        public ConditionVisitor(ParameterExpression message, ErrorListener errors, Route route, RouteCompilerFlags routeCompilerFlags)
        {
            this.message = Preconditions.CheckNotNull(message);
            this.errors = Preconditions.CheckNotNull(errors);
            this.route = Preconditions.CheckNotNull(route);
            this.routeCompilerFlags = routeCompilerFlags;
        }

        // Literals
        public override Expression VisitBool(ConditionParser.BoolContext context)
        {
            bool result;
            return bool.TryParse(context.GetText(), out result) ? Expression.Constant((Bool)result) : UndefinedExpression;
        }

        public override Expression VisitHex(ConditionParser.HexContext context)
        {
            long result;
            return long.TryParse(context.GetText().Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result) ? Expression.Constant((double)result) : UndefinedExpression;
        }

        public override Expression VisitInteger(ConditionParser.IntegerContext context)
        {
            double result;
            return double.TryParse(context.GetText(), NumberStyles.Any, CultureInfo.InvariantCulture, out result) ? Expression.Constant(result) : UndefinedExpression;
        }

        public override Expression VisitReal(ConditionParser.RealContext context)
        {
            double result;
            return double.TryParse(context.GetText(), NumberStyles.Any, CultureInfo.InvariantCulture, out result) ? Expression.Constant(result) : UndefinedExpression;
        }

        public override Expression VisitString(ConditionParser.StringContext context)
        {
            // strip the quotes in order to escape the contents of the string
            string input = context.GetText().Substring(1, context.GetText().Length - 2);
            string value = Unescape(input);
            return Expression.Constant(value);
        }

        public override Expression VisitUndefined(ConditionParser.UndefinedContext context)
        {
            return UndefinedExpression;
        }

        public override Expression VisitNull(ConditionParser.NullContext context)
        {
            return Expression.Constant(Null.Instance);
        }

        // Member Access
        public override Expression VisitProperty(ConditionParser.PropertyContext context)
        {
            string property = context.GetText();
            return this.GetProperty(Expression.Property(this.message, "Properties"), Expression.Constant(property));
        }

        public override Expression VisitSysProperty(ConditionParser.SysPropertyContext context)
        {
            // called if the property starts with a '$', it can be an app property or system property or a body query
            // the app property takes precedence
            // We must check whether the property first exists as a user property. If it does, return it, else
            // try to get it from the body query or the system properties

            string propertyName = context.GetText();

            Expression alternative;
            bool isBodyQuery = false;

            if (this.IsBodyQueryExpression(propertyName, context.SYS_PROP().Symbol, out alternative))
            {
                isBodyQuery = alternative.Type != typeof(Undefined);
            }

            // Property names do not support '[' or ']'. Return body query in this case
            if (propertyName.Contains("[") || propertyName.Contains("]"))
            {
                return alternative;
            }

            if (!isBodyQuery)
            {
                alternative = this.GetSysProperty(propertyName.Substring(1));
            }

            Expression property = Expression.Constant(propertyName);
            Expression propertyBag = Expression.Property(this.message, "Properties");

            return this.GetPropertyOrElse(propertyBag, property, alternative, isBodyQuery);
        }

        public override Expression VisitSysPropertyEscaped(ConditionParser.SysPropertyEscapedContext context)
        {
            // Handles escaped system property `{$<propname>}`
            string propertyName = context.prop.Text;

            Expression sysPropertyExpression;
            if (!this.IsBodyQueryExpression(propertyName, context.SYS_PROP().Symbol, out sysPropertyExpression))
            {
                sysPropertyExpression = this.GetSysProperty(propertyName.Substring(1));
            }

            return sysPropertyExpression;
        }

        Expression GetProperty(Expression propertyBag, Expression property)
        {
            return this.GetPropertyOrElse(propertyBag, property, UndefinedExpression, false);
        }

        Expression GetPropertyOrElse(Expression propertyBag, Expression property, Expression alternative, bool isBodyQuery)
        {
            Expression expression;

            if (propertyBag.Type != typeof(Undefined))
            {
                MethodInfo method = typeof(IReadOnlyDictionary<string, string>).GetMethod("ContainsKey");
                MethodCallExpression contains = Expression.Call(propertyBag, method, property);
                IndexExpression value = Expression.Property(propertyBag, "Item", property);

                // NOTE: Arguments to Expression.Condition need to be of the same type. So we will wrap to QueryValue if it could be a bodyquery

                // Review following cases before making changes here - 
                // 1. It is an app property with no conflict => Convert to String and return value and alternative
                // 2. It is a system property with no conflict => Same as case 1. Property bag supplied by caller is sys property
                // 3. It is a Body query with no conflict => ifFalse Expression will be returned. Type is already QueryValue.
                // 4. It looks like a body query but is an app property ($body.propertyname) => 
                //              ifTrue will be evaluated. Just wrap contents to QueryValue so that Expression.Condition does not complain.
                // 5. Body Query and a conflicting app property => App property wins
                // 6. Body Query escaped and a conflicting app property => Body Query wins

                Expression ifTrue = isBodyQuery ? Expression.Convert(value, typeof(QueryValue)) : Expression.Convert(value, typeof(string));
                Expression ifFalse = isBodyQuery ? alternative : Expression.Convert(alternative, typeof(string));

                expression = Expression.Condition(
                    contains,
                    ifTrue,
                    ifFalse);
            }
            else
            {
                expression = UndefinedExpression;
            }
            return expression;
        }

        Expression GetSysProperty(string propertyName)
        {
            // SystemProperty name containing '[' or ']' can reach here if it looked like body query, and was parsed successfully using Condition.g4.
            // In this case, return Undefined because it is not a supported SystemProperty.
            if (propertyName.Contains("[") || propertyName.Contains("]"))
            {
                return UndefinedExpression;
            }

            Expression propertyBag = Expression.Property(this.message, "SystemProperties");
            Expression property = Expression.Constant(propertyName);
            return this.GetProperty(propertyBag, property);
        }

        // Functions
        public override Expression VisitFunc(ConditionParser.FuncContext context)
        {
            IToken funcToken = context.fcall().func;
            string funcText = funcToken.Text;

            IBuiltin builtin;
            if (!this.TryGetSupportedBuiltin(funcText, out builtin))
            {
                this.errors.InvalidBuiltinError(funcToken);
                return UndefinedExpression;
            }
            else
            {
                Expression[] args;
                Expression[] contextArgs = null;
                if (builtin.IsBodyQuery)
                {
                    args = new Expression[]
                    {
                        Expression.Constant(context.fcall().exprList()?.GetText() ?? string.Empty)
                    };

                    contextArgs = new Expression[]
                    {
                        this.message,
                        Expression.Constant(this.route)
                    };
                }
                else
                {
                    args = context.fcall().exprList() != null ? context.fcall().exprList().expr().Select(expr => this.Visit(expr)).ToArray() : NoArgs;
                }

                return builtin.Get(funcToken, args, contextArgs, this.errors);
            }
        }

        // Unary Ops
        public override Expression VisitNegate(ConditionParser.NegateContext context)
        {
            Expression expr = this.Visit(context.expr());
            return this.CheckOperand(context.op, typeof(double), expr)
                ? Expression.Negate(expr)
                : UndefinedExpression;
        }

        public override Expression VisitNot(ConditionParser.NotContext context)
        {
            Expression expr = this.Visit(context.expr());
            // "not null" is a valid expression. We check for null and assume
            // the operand is a bool if it's not null.
            Type type = expr.Type == typeof(Null) ? typeof(Null) : typeof(Bool);
            return this.CheckOperand(context.op, type, expr)
                ? Expression.Convert(Expression.Not(expr), typeof(Bool))
                : UndefinedExpression;
        }

        // Binary Ops
        public override Expression VisitMulDivMod(ConditionParser.MulDivModContext context)
        {
            Expression result;
            Expression left = this.Visit(context.left);
            Expression right = this.Visit(context.right);

            if (this.CheckOperands(context.op, typeof(double), ref left, ref right))
            {
                switch (context.op.Type)
                {
                    case ConditionParser.OP_MUL:
                        result = Expression.Multiply(left, right);
                        break;
                    case ConditionParser.OP_DIV:
                        result = Expression.Divide(left, right);
                        break;
                    case ConditionParser.OP_MOD:
                        result = Expression.Modulo(left, right);
                        break;
                    default:
                        throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Unrecognized op token: {0}", context.op.Text));
                }
            }
            else
            {
                result = UndefinedExpression;
            }
            return result;
        }

        public override Expression VisitAddSubConcat(ConditionParser.AddSubConcatContext context)
        {
            Expression result;
            Expression left = this.Visit(context.left);
            Expression right = this.Visit(context.right);

            switch (context.op.Type)
            {
                case ConditionParser.PLUS:
                    result = this.CheckOperands(context.op, typeof(double), ref left, ref right) ? Expression.Add(left, right) : UndefinedExpression;
                    break;
                case ConditionParser.MINUS:
                    result = this.CheckOperands(context.op, typeof(double), ref left, ref right) ? Expression.Subtract(left, right) : UndefinedExpression;
                    break;
                case ConditionParser.OP_CONCAT:
                    result = this.CheckOperands(context.op, typeof(string), ref left, ref right) ? this.GetBuiltin("concat", context.op, left, right) : UndefinedExpression;
                    break;
                default:
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Unrecognized op token: {0}", context.op.Text));
            }
            return result;
        }

        public override Expression VisitCompare(ConditionParser.CompareContext context)
        {
            Expression result;
            Expression left = this.Visit(context.left);
            Expression right = this.Visit(context.right);

            if (this.CheckOperands(context.op, left.Type, ref left, ref right))
            {
                MethodInfo method = typeof(ComparisonOperators).GetMethod("Compare", new[] { typeof(CompareOp), left.Type, right.Type });

                switch (context.op.Type)
                {
                    case ConditionParser.OP_LT:
                        result = Expression.Call(method, Expression.Constant(CompareOp.Lt), left, right);
                        break;
                    case ConditionParser.OP_LE:
                        result = Expression.Call(method, Expression.Constant(CompareOp.Le), left, right);
                        break;
                    case ConditionParser.OP_GT:
                        result = Expression.Call(method, Expression.Constant(CompareOp.Gt), left, right);
                        break;
                    case ConditionParser.OP_GE:
                        result = Expression.Call(method, Expression.Constant(CompareOp.Ge), left, right);
                        break;
                    default:
                        throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Unrecognized op token: {0}", context.op.Text));
                }
            }
            else
            {
                result = UndefinedExpression;
            }
            return result;
        }

        public override Expression VisitEquality(ConditionParser.EqualityContext context)
        {
            Expression result;
            Expression left = this.Visit(context.left);
            Expression right = this.Visit(context.right);

            if (this.CheckOperands(context.op, left.Type, ref left, ref right))
            {
                MethodInfo method = typeof(ComparisonOperators).GetMethod("Compare", new[] { typeof(CompareOp), left.Type, right.Type });

                switch (context.op.Type)
                {
                    case ConditionParser.OP_EQ:
                        result = Expression.Call(method, Expression.Constant(CompareOp.Eq), left, right);
                        break;
                    case ConditionParser.OP_NE1:
                    case ConditionParser.OP_NE2:
                        result = Expression.Call(method, Expression.Constant(CompareOp.Ne), left, right);
                        break;
                    default:
                        throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Unrecognized op token: {0}", context.op.Text));
                }
            }
            else
            {
                result = UndefinedExpression;
            }
            return result;
        }

        public override Expression VisitAnd(ConditionParser.AndContext context)
        {
            Expression left = this.Visit(context.left);
            Expression right = this.Visit(context.right);

            Type type = left.Type == typeof(Null) || right.Type == typeof(Null) ? typeof(Null) : typeof(Bool);
            return this.CheckBooleanOperands(context.op, ref left, ref right)
                ? Expression.Convert(Expression.And(left, right), type)
                : UndefinedExpression;
        }

        public override Expression VisitOr(ConditionParser.OrContext context)
        {
            Expression left = this.Visit(context.left);
            Expression right = this.Visit(context.right);

            Type type = left.Type == typeof(Null) && right.Type == typeof(Null) ? typeof(Null) : typeof(Bool);
            return this.CheckBooleanOperands(context.op, ref left, ref right)
                ? Expression.Convert(Expression.Or(left, right), type)
                : UndefinedExpression;
        }

        public override Expression VisitCoalesce(ConditionParser.CoalesceContext context)
        {
            Expression result;
            Expression left = this.Visit(context.left);
            Expression right = this.Visit(context.right);

            if (this.CheckOperands(context.op, left.Type, ref left, ref right))
            {
                Expression isDefined = Expression.Convert(this.GetBuiltin("is_defined", context.op, left), typeof(bool));
                result = left.Type != typeof(Undefined) ? Expression.Condition(isDefined, left, right) : right;
            }
            else
            {
                result = UndefinedExpression;
            }
            return result;
        }

        public override Expression VisitNested(ConditionParser.NestedContext context)
        {
            return this.Visit(context.nested_expr().expr());
        }

        public override Expression VisitEof(ConditionParser.EofContext context)
        {
            return UndefinedExpression;
        }

        // Errors
        public override Expression VisitSyntaxError(ConditionParser.SyntaxErrorContext context)
        {
            this.errors.UnrecognizedSymbolError(context.token.UNKNOWN_CHAR().Symbol);
            return UndefinedExpression;
        }

        public override Expression VisitSyntaxErrorUnaryOp(ConditionParser.SyntaxErrorUnaryOpContext context)
        {
            this.errors.UnrecognizedSymbolError(context.token.UNKNOWN_CHAR().Symbol);
            return UndefinedExpression;
        }

        public override Expression VisitSyntaxErrorBinaryOp(ConditionParser.SyntaxErrorBinaryOpContext context)
        {
            this.errors.UnrecognizedSymbolError(context.token.UNKNOWN_CHAR().Symbol);
            return UndefinedExpression;
        }

        public override Expression VisitSyntaxErrorExtraParens(ConditionParser.SyntaxErrorExtraParensContext context)
        {
            this.errors.SyntaxError(context.paren, "unmatched closing ')'");
            return UndefinedExpression;
        }

        public override Expression VisitSyntaxErrorExtraParensFunc(ConditionParser.SyntaxErrorExtraParensFuncContext context)
        {
            this.errors.SyntaxError(context.paren, "unmatched closing ')'");
            return UndefinedExpression;
        }

        public override Expression VisitSyntaxErrorMissingParen(ConditionParser.SyntaxErrorMissingParenContext context)
        {
            this.errors.SyntaxError(context.paren, "missing closing ')'");
            return UndefinedExpression;
        }

        public override Expression VisitUnterminatedString(ConditionParser.UnterminatedStringContext context)
        {
            this.errors.SyntaxError(context.UNTERMINATED_STRING().Symbol, "unterminated string");

            // strip the leading quote in order to escape the contents of the string
            string input = context.GetText().Substring(1);
            string value = Unescape(input);
            return Expression.Constant(value);
        }

        static string Unescape(string input)
        {
            string result1 = Regex.Replace(input, @"\\[Uu]([0-9A-Fa-f]{4})", m => char.ToString((char)ushort.Parse(m.Groups[1].Value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture)));
            string result2 = Regex.Replace(result1, @"\\([""'\\])", m => m.Groups[1].Value);
            return result2;
        }

        Expression GetBuiltin(string name, IToken token, params Expression[] args)
        {
            IBuiltin builtin;
            if (!this.TryGetSupportedBuiltin(name, out builtin))
            {
                this.errors.InvalidBuiltinError(token);
                return UndefinedExpression;
            }
            else
            {
                return builtin.Get(token, args, null, this.errors);
            }
        }

        bool TryGetSupportedBuiltin(string name, out IBuiltin builtin)
        {
            return Builtins.TryGetValue(name, out builtin) &&
                builtin.IsEnabled(this.routeCompilerFlags) &&
                builtin.IsValidMessageSource(this.route.Source);
        }

        bool CheckOperand(IToken token, Type expected, Expression expr)
        {
            var required = new Args(expected);
            Type[] given = new[] { expr.Type };

            bool isValid = required.Match(given, true);
            if (!isValid)
            {
                this.errors.OperandError(token, required, given);
            }
            return isValid;
        }

        bool CheckOperands(IToken token, Type expected, ref Expression left, ref Expression right)
        {
            var required = new Args(expected, expected);
            Type[] given = new[] { left.Type, right.Type };

            bool isValid = required.Match(given, true);
            if (!isValid)
            {
                this.errors.OperandError(token, required, given);
            }

            if (left.Type == typeof(QueryValue) && right.Type != typeof(QueryValue))
            {
                right = Expression.Convert(right, typeof(QueryValue));
            }
            if (right.Type == typeof(QueryValue) && left.Type != typeof(QueryValue))
            {
                left = Expression.Convert(left, typeof(QueryValue));
            }

            return isValid;
        }

        bool CheckBooleanOperands(IToken token, ref Expression left, ref Expression right)
        {
            bool leftValid = left.Type == typeof(Null) || left.Type == typeof(Bool) || left.Type == typeof(Undefined) || left.Type == typeof(QueryValue);
            bool rightValid = right.Type == typeof(Null) || right.Type == typeof(Bool) || right.Type == typeof(Undefined) || left.Type == typeof(QueryValue);
            bool isValid = leftValid && rightValid;
            if (!isValid)
            {
                var required = new Args(typeof(Bool), typeof(Bool));
                Type[] given = new[] { left.Type, right.Type };
                this.errors.OperandError(token, required, given);
            }

            if (left.Type == typeof(QueryValue) && right.Type != typeof(QueryValue))
            {
                right = Expression.Convert(right, typeof(QueryValue));
            }
            if (right.Type == typeof(QueryValue) && left.Type != typeof(QueryValue))
            {
                left = Expression.Convert(left, typeof(QueryValue));
            }

            return isValid;
        }

        bool IsBodyQueryExpression(string query, IToken token, out Expression queryValueExpression)
        {
            const string BodyQueryPrefix = "$body.";

            queryValueExpression = UndefinedExpression;

            if (query.StartsWith(BodyQueryPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var bodyQueryBuiltin = new BodyQuery();
                if (bodyQueryBuiltin.IsEnabled(this.routeCompilerFlags))
                {
                    string bodyQuery = query.Substring(BodyQueryPrefix.Length);

                    var args = new Expression[]
                    {
                        Expression.Constant(bodyQuery)
                    };

                    var contextArgs = new Expression[]
                    {
                        this.message,
                        Expression.Constant(this.route)
                    };

                    queryValueExpression = bodyQueryBuiltin.Get(token, args, contextArgs, this.errors);
                    return true;
                }
            }

            return false;
        }
    }
}
