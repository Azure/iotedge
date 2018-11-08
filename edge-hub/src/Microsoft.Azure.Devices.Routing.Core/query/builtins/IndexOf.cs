// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Query.Builtins
{
    using System;
    using System.Linq.Expressions;
    using System.Reflection;

    using Microsoft.Azure.Devices.Routing.Core.Query.Types;

    public class IndexOf : Builtin
    {
        protected override BuiltinExecutor[] Executors => new[]
        {
            // index_of(string, string)
            new BuiltinExecutor
            {
                InputArgs = new Args(typeof(string), typeof(string)),
                IsQueryValueSupported = true,
                ExecutorFunc = Create
            }
        };

        static Expression Create(Expression[] args, Expression[] contextArgs)
        {
            return Expression.Call(typeof(IndexOf).GetMethod("Runtime", BindingFlags.NonPublic | BindingFlags.Static), args);
        }

        // ReSharper disable once UnusedMember.Local
        static double Runtime(QueryValue input, QueryValue fragment)
        {
            if (input?.ValueType != QueryValueType.String || fragment?.ValueType != QueryValueType.String)
            {
                return Undefined.Instance;
            }

            string inputString = (string)input.Value;
            string fragmentString = (string)fragment.Value;

            bool isValid = !inputString.IsNullOrUndefined() && !fragmentString.IsNullOrUndefined();
            return isValid ? (double)inputString.IndexOf(fragmentString, StringComparison.Ordinal) : Undefined.Instance;
        }
    }
}
