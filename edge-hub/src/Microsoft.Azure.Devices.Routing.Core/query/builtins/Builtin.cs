// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Query.Builtins
{
    using System;
    using System.Linq;
    using System.Linq.Expressions;

    using Antlr4.Runtime;

    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Microsoft.Azure.Devices.Routing.Core.Query.Types;

    public abstract class Builtin : IBuiltin
    {
        public virtual bool IsBodyQuery => false;

        protected static Expression False { get; } = Expression.Constant(Bool.False);

        protected static Expression True { get; } = Expression.Constant(Bool.True);

        protected abstract BuiltinExecutor[] Executors { get; }

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

        public virtual bool IsEnabled(RouteCompilerFlags routeCompilerFlags)
        {
            return true;
        }

        public virtual bool IsValidMessageSource(IMessageSource source)
        {
            return true;
        }

        static Expression[] WrapArgsAsQueryValue(Expression[] expressions)
        {
            return expressions.Select(
                exp =>
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
