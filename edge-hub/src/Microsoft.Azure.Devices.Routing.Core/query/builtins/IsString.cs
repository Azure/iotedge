// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Query.Builtins
{
    using System.Linq.Expressions;
    using System.Reflection;
    using Microsoft.Azure.Devices.Routing.Core.Query.Types;

    public class IsString : Builtin
    {
        protected override BuiltinExecutor[] Executors => new[]
        {
            // is_string(string)
            new BuiltinExecutor
            {
                InputArgs = new Args(typeof(string)),
                IsQueryValueSupported = true,
                ExecutorFunc = Create
            },
            // is_number(_)
            new BuiltinExecutor
            {
                InputArgs = new AnyArgs(1),
                ExecutorFunc = (args, contextArgs) => False
            }
        };

        static Expression Create(Expression[] args, Expression[] contextArgs)
        {
            return Expression.Call(typeof(IsString).GetMethod("Runtime", BindingFlags.NonPublic | BindingFlags.Static), args);
        }

        // ReSharper disable once UnusedMember.Local
        static Bool Runtime(QueryValue input)
        {
            if (input?.ValueType != QueryValueType.String)
            {
                return Bool.False;
            }

            string inputString = (string)input.Value;

            return Undefined.IsDefined(inputString) && (Bool)(inputString != null);
        }
    }
}
