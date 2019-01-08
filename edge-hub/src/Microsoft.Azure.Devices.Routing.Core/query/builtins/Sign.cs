// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Query.Builtins
{
    using System;
    using System.Linq.Expressions;
    using System.Reflection;
    using Microsoft.Azure.Devices.Routing.Core.Query.Types;

    public class Sign : Builtin
    {
        protected override BuiltinExecutor[] Executors => new[]
        {
            // sign(number)
            new BuiltinExecutor
            {
                InputArgs = new Args(typeof(double)),
                IsQueryValueSupported = true,
                ExecutorFunc = Create
            }
        };

        static Expression Create(Expression[] args, Expression[] contextArgs)
        {
            return Expression.Call(typeof(Sign).GetMethod("Runtime", BindingFlags.NonPublic | BindingFlags.Static), args);
        }

        // ReSharper disable once UnusedMember.Local
        static double Runtime(QueryValue input)
        {
            if (input?.ValueType != QueryValueType.Double)
            {
                return Undefined.Instance;
            }

            double inputValue = (double)input.Value;

            return Undefined.IsDefined(inputValue) ? Math.Sign(inputValue) : (double)Undefined.Instance;
        }
    }
}
