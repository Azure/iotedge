// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Query.Builtins
{
    using System.Globalization;
    using System.Linq.Expressions;
    using System.Reflection;

    using Microsoft.Azure.Devices.Routing.Core.Query.Types;

    public class AsNumber : Builtin
    {
        protected override BuiltinExecutor[] Executors => new[]
        {
            new BuiltinExecutor
            {
                InputArgs = new Args(typeof(string)),
                IsQueryValueSupported = true,
                ExecutorFunc = Create
            },
            new BuiltinExecutor
            {
                InputArgs = new Args(typeof(double)),
                ExecutorFunc = (args, contextArgs) => args[0]
            }
        };

        static Expression Create(Expression[] args, Expression[] contextArgs)
        {
            return Expression.Call(typeof(AsNumber).GetMethod("Runtime", BindingFlags.NonPublic | BindingFlags.Static), args);
        }

        // ReSharper disable once UnusedMember.Local
        static double Runtime(QueryValue input)
        {
            if (input?.ValueType == QueryValueType.String)
            {
                double answer;
                return double.TryParse((string)input.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out answer) ? answer : Undefined.Instance;
            }

            if (input?.ValueType == QueryValueType.Double)
            {
                return (double)input.Value;
            }

            return Undefined.Instance;
        }
    }
}
