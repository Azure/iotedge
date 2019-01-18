// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Query.Builtins
{
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using Microsoft.Azure.Devices.Routing.Core.Query.Types;

    public class Concat : Builtin
    {
        protected override BuiltinExecutor[] Executors => new[]
        {
            // concat(string, string, string...)
            new BuiltinExecutor
            {
                InputArgs = new VarArgs(typeof(string), typeof(string), typeof(string)),
                IsQueryValueSupported = true,
                ExecutorFunc = Create
            },
        };

        static Expression Create(Expression[] args, Expression[] contextArgs)
        {
            NewArrayExpression arrayArgs = Expression.NewArrayInit(typeof(QueryValue), args);
            return Expression.Call(typeof(Concat).GetMethod("Runtime", BindingFlags.NonPublic | BindingFlags.Static), arrayArgs);
        }

        // ReSharper disable once UnusedMember.Local
        static string Runtime(QueryValue[] input)
        {
            return input.All(s => s?.ValueType == QueryValueType.String && Undefined.IsDefined((string)s.Value))
                ? string.Concat(input.Select(_ => _.Value))
                : Undefined.Instance;
        }
    }
}
