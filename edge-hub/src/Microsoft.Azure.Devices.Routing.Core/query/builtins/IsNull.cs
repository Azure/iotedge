// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core.Query.Builtins
{
    using System.Linq.Expressions;
    using System.Reflection;
    using Microsoft.Azure.Devices.Routing.Core.Query.Types;

    public class IsNull : Builtin
    {
        protected override BuiltinExecutor[] Executors => new[]
        {
            // is_null(string)
            new BuiltinExecutor
            {
                InputArgs = new Args(typeof(string)),
                ExecutorFunc = CreateString
            },
            // is_null(QueryValue)
            new BuiltinExecutor
            {
                InputArgs = new Args(typeof(QueryValue)),
                ExecutorFunc = CreateQueryValue
            },
            // is_null(null)
            new BuiltinExecutor
            {
                InputArgs = new Args(typeof(Null)),
                ExecutorFunc = (args, contextArgs) => True
            },
            // is_null(_)
            new BuiltinExecutor
            {
                InputArgs = new AnyArgs(1),
                ExecutorFunc = (args, contextArgs) => False
            },
        };

        static Expression CreateString(Expression[] args, Expression[] contextArgs)
        {
            return Expression.Call(typeof(IsNull).GetMethod("RuntimeString", BindingFlags.NonPublic | BindingFlags.Static), args);
        }

        static Expression CreateQueryValue(Expression[] args, Expression[] contextArgs)
        {
            return Expression.Call(typeof(IsNull).GetMethod("RuntimeQueryValue", BindingFlags.NonPublic | BindingFlags.Static), args);
        }

        // ReSharper disable once UnusedMember.Local
        static Bool RuntimeString(string input) => (Bool)(input == null);

        // ReSharper disable once UnusedMember.Local
        static Bool RuntimeQueryValue(QueryValue input)
        {
            if (Undefined.IsDefined(input))
            {
                return (Bool)(input != QueryValue.Null);
            }

            return Bool.Undefined;
        }
    }
}
