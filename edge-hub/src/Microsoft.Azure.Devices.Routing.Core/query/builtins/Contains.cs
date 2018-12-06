// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Routing.Core.Query.Builtins
{
    using System.Linq.Expressions;
    using System.Reflection;
    using Microsoft.Azure.Devices.Routing.Core.Query.Types;

    public class Contains : Builtin
    {
        protected override BuiltinExecutor[] Executors => new[]
        {
            // Contains(string, string)
            new BuiltinExecutor
            {
                InputArgs = new Args(typeof(string), typeof(string)),
                IsQueryValueSupported = true,
                ExecutorFunc = Create
            },
        };

        static Expression Create(Expression[] expressions, Expression[] innerExpressions)
        {
            return Expression.Call(typeof(Contains).GetMethod("Runtime", BindingFlags.NonPublic | BindingFlags.Static), expressions);
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
            return isValid ? (Bool)inputString.Contains(fragmentString) : Undefined.Instance;
        }
    }
}
