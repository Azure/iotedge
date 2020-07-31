// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Query.Builtins
{
    using System;
    using System.Linq.Expressions;
    using System.Reflection;
    using Microsoft.Azure.Devices.Routing.Core.Query.Types;

    public class Power : Builtin
    {
        protected override BuiltinExecutor[] Executors => new[]
        {
            // power(number, number)
            new BuiltinExecutor
            {
                InputArgs = new Args(typeof(double), typeof(double)),
                IsQueryValueSupported = true,
                ExecutorFunc = Create
            }
        };

        static Expression Create(Expression[] args, Expression[] contextArgs)
        {
            return Expression.Call(typeof(Power).GetMethod("Runtime", BindingFlags.NonPublic | BindingFlags.Static), args);
        }

        // ReSharper disable once UnusedMember.Local
        static double Runtime(QueryValue x, QueryValue y)
        {
            if (x?.ValueType != QueryValueType.Double || y?.ValueType != QueryValueType.Double)
            {
                return Undefined.Instance;
            }

            return Math.Pow((double)x.Value, (double)y.Value);
        }
    }
}
