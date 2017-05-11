// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core.Query.Builtins
{
    using System;
    using System.Linq;
    using System.Linq.Expressions;
    using Antlr4.Runtime;
    using Microsoft.Azure.Devices.Routing.Core.Query.Types;
    using Microsoft.Azure.Devices.Routing.Core.Util;

    public abstract class Builtin : IBuiltin
    {
        protected static Expression True { get; } = Expression.Constant(Bool.True);

        protected static Expression False { get; } = Expression.Constant(Bool.False);

        protected abstract BuiltinExecutor[] Executors { get; }

        public virtual bool IsBodyQuery => false;

        public virtual bool IsValidMessageSource(MessageSource source)
        {
            return true;
        }

        public virtual bool IsEnabled(RouteCompilerFlags routeCompilerFlags)
        {
            return true;
        }

        public Expression Get(IToken token, Expression[] args, Expression[] contextArgs, ErrorListener errors)
        {
            string errorDetails = null;
            Type[] types = args.Select(arg => arg.Type).ToArray();

            try
            {
                BuiltinExecutor executor = this.Executors.FirstOrDefault(ex => ex.InputArgs.Match(types, ex.IsQueryValueSupported));
                // contextArgs currently are very straightforward. BodyQuery and TwinChangeIncludes use them to get message body.
                // Not doing a match on internal args to retrieve the executor as of yet

                if (executor != null)
                {
                    if (executor.IsQueryValueSupported)
                    {
                        // Wrap all args as QueryValue
                        args = WrapArgsAsQueryValue(args);
                    }

                    return executor.ExecutorFunc(args, contextArgs);
                }
            }
            catch (Exception ex) when (!ex.IsFatal())
            {
                errorDetails = ex.Message;
            }

            errors.ArgumentError(token, this.Executors.Select(ex => ex.InputArgs).ToArray(), types, errorDetails);
            return Expression.Constant(Undefined.Instance);
        }

        static Expression[] WrapArgsAsQueryValue(Expression[] expressions)
        {
            return expressions.Select(exp =>
            {
                if (exp.Type == typeof(QueryValue))
                {
                    return exp;
                }
                else
                {
                    return Expression.Convert(exp, typeof(QueryValue));
                }
            }).ToArray();
        }
    }
}
