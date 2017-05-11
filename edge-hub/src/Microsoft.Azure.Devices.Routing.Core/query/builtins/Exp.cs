// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core.Query.Builtins
{
    using System;
    using System.Linq.Expressions;
    using System.Reflection;
    using Microsoft.Azure.Devices.Routing.Core.Query.Types;

    public class Exp : Builtin
    {
        protected override BuiltinExecutor[] Executors => new[]
        {
            // exp(number)
            new BuiltinExecutor
            {
                InputArgs = new Args(typeof(double)),
                IsQueryValueSupported = true,
                ExecutorFunc = Create
            }
        };

        static Expression Create(Expression[] args, Expression[] contextArgs)
        {
            return Expression.Call(typeof(Exp).GetMethod("Runtime", BindingFlags.NonPublic | BindingFlags.Static), args);
        }

        // ReSharper disable once UnusedMember.Local
        static double Runtime(QueryValue input)
        {
            if (input?.ValueType == QueryValueType.Double)
            {
                return Math.Exp((double)input.Value);
            }

            return Undefined.Instance;
        }
    }
}
