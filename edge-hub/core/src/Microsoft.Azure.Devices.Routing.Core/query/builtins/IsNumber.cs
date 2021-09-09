// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Query.Builtins
{
    using System.Linq.Expressions;
    using System.Reflection;
    using Microsoft.Azure.Devices.Routing.Core.Query.Types;

    public class IsNumber : Builtin
    {
        protected override BuiltinExecutor[] Executors => new[]
        {
            // is_number(number)
            new BuiltinExecutor
            {
                InputArgs = new Args(typeof(double)),
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
            return Expression.Call(typeof(IsNumber).GetMethod("Runtime", BindingFlags.NonPublic | BindingFlags.Static), args);
        }

        // ReSharper disable once UnusedMember.Local
        static Bool Runtime(QueryValue input)
        {
            if (input?.ValueType != QueryValueType.Double)
            {
                return Bool.False;
            }

            double inputValue = (double)input.Value;
            return (Bool)(!double.IsInfinity(inputValue) && !double.IsNaN(inputValue) && Undefined.IsDefined(inputValue));
        }
    }
}
