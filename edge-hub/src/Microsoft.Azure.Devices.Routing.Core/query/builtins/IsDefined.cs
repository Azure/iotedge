// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Query.Builtins
{
    using System.Linq.Expressions;
    using System.Reflection;

    using Microsoft.Azure.Devices.Routing.Core.Query.Types;

    public class IsDefined : Builtin
    {
        protected override BuiltinExecutor[] Executors => new[]
        {
            // is_defined(string)
            new BuiltinExecutor
            {
                InputArgs = new Args(typeof(string)),
                ExecutorFunc = CreateString
            },

            // is_defined(number)
            new BuiltinExecutor
            {
                InputArgs = new Args(typeof(double)),
                ExecutorFunc = CreateDouble
            },

            // is_defined(bool)
            new BuiltinExecutor
            {
                InputArgs = new Args(typeof(Bool)),
                ExecutorFunc = CreateBool
            },

            // is_defined(undefined)
            new BuiltinExecutor
            {
                InputArgs = new Args(typeof(Undefined)),
                ExecutorFunc = (args, contextArgs) => False
            },

            // is_defined(null)
            new BuiltinExecutor
            {
                InputArgs = new Args(typeof(Null)),
                ExecutorFunc = (args, contextArgs) => True
            },

            // is_defined(QueryValue)
            new BuiltinExecutor
            {
                InputArgs = new Args(typeof(QueryValue)),
                ExecutorFunc = CreateQueryValue
            },
        };

        static Expression CreateBool(Expression[] args, Expression[] contextArgs)
        {
            return Expression.Call(typeof(IsDefined).GetMethod("RuntimeBool", BindingFlags.NonPublic | BindingFlags.Static), args);
        }

        static Expression CreateDouble(Expression[] args, Expression[] contextArgs)
        {
            return Expression.Call(typeof(IsDefined).GetMethod("RuntimeDouble", BindingFlags.NonPublic | BindingFlags.Static), args);
        }

        static Expression CreateQueryValue(Expression[] args, Expression[] contextArgs)
        {
            return Expression.Call(typeof(IsDefined).GetMethod("RuntimeQueryValue", BindingFlags.NonPublic | BindingFlags.Static), args);
        }

        static Expression CreateString(Expression[] args, Expression[] contextArgs)
        {
            return Expression.Call(typeof(IsDefined).GetMethod("RuntimeString", BindingFlags.NonPublic | BindingFlags.Static), args);
        }

        static Bool RuntimeString(string input) => Undefined.IsDefined(input);

        static Bool RuntimeDouble(double input) => Undefined.IsDefined(input);

        static Bool RuntimeBool(Bool input) => Undefined.IsDefined(input);

        static Bool RuntimeQueryValue(QueryValue input) => Undefined.IsDefined(input);
    }
}
