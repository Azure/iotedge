// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Query.Builtins
{
    using System.Linq.Expressions;
    using System.Reflection;

    using Microsoft.Azure.Devices.Routing.Core.Query.Types;

    public class Lower : Builtin
    {
        protected override BuiltinExecutor[] Executors => new[]
        {
            // lower(string)
            new BuiltinExecutor
            {
                InputArgs = new Args(typeof(string)),
                IsQueryValueSupported = true,
                ExecutorFunc = Create
            }
        };

        static Expression Create(Expression[] args, Expression[] contextArgs)
        {
            return Expression.Call(typeof(Lower).GetMethod("Runtime", BindingFlags.NonPublic | BindingFlags.Static), args);
        }

        // ReSharper disable once UnusedMember.Local
        static string Runtime(QueryValue input)
        {
            if (input?.ValueType != QueryValueType.String)
            {
                return Undefined.Instance;
            }

            string inputString = (string)input.Value;

            return !inputString.IsNullOrUndefined() ? inputString.ToLowerInvariant() : Undefined.Instance;
        }
    }
}
