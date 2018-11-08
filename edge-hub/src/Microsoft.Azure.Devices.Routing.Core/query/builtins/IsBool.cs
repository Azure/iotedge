// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Query.Builtins
{
    using System.Linq.Expressions;
    using System.Reflection;

    using Microsoft.Azure.Devices.Routing.Core.Query.Types;

    public class IsBool : Builtin
    {
        protected override BuiltinExecutor[] Executors => new[]
        {
            // is_bool(bool)
            new BuiltinExecutor
            {
                InputArgs = new Args(typeof(Bool)),
                IsQueryValueSupported = true,
                ExecutorFunc = Create
            },

            // is_bool(_)
            new BuiltinExecutor
            {
                InputArgs = new AnyArgs(1),
                ExecutorFunc = (args, contextArgs) => False
            },
        };

        static Expression Create(Expression[] args, Expression[] contextArgs)
        {
            return Expression.Call(typeof(IsBool).GetMethod("Runtime", BindingFlags.NonPublic | BindingFlags.Static), args);
        }

        // ReSharper disable once UnusedMember.Local
        static Bool Runtime(QueryValue input)
        {
            if (Undefined.IsDefined(input))
            {
                if (input?.ValueType != QueryValueType.Bool)
                {
                    return Bool.False;
                }

                return Undefined.IsDefined((Bool)input.Value);
            }

            return Bool.Undefined;
        }
    }
}
