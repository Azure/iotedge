// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core.Query.Builtins
{
    using System;
    using System.Linq.Expressions;
    using System.Reflection;
    using Microsoft.Azure.Devices.Routing.Core.Query.Types;

    public class EndsWith : Builtin
    {
        protected override BuiltinExecutor[] Executors => new[]
        {
            // ends_with(string, string)
            new BuiltinExecutor
            {
                InputArgs = new Args(typeof(string), typeof(string)),
                IsQueryValueSupported = true,
                ExecutorFunc = Create
            },
        };

        static Expression Create(Expression[] args, Expression[] contextArgs)
        {
            return Expression.Call(typeof(EndsWith).GetMethod("Runtime", BindingFlags.NonPublic | BindingFlags.Static), args);
        }

        // ReSharper disable once UnusedMember.Local
        static Bool Runtime(QueryValue input, QueryValue fragment)
        {
            if (input?.ValueType != QueryValueType.String || fragment?.ValueType != QueryValueType.String)
            {
                return Undefined.Instance;
            }

            string inputString = (string)input.Value;
            string fragmentString = (string)fragment.Value;

            bool isValid = !inputString.IsNullOrUndefined() && !fragmentString.IsNullOrUndefined();
            return isValid ? (Bool)inputString.EndsWith(fragmentString, StringComparison.Ordinal) : Undefined.Instance;
        }
    }
}
