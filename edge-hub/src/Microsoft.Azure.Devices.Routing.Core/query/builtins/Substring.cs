// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Routing.Core.Query.Builtins
{
    using System.Linq.Expressions;
    using System.Reflection;
    using Microsoft.Azure.Devices.Routing.Core.Query.Types;

    public class Substring : Builtin
    {
        protected override BuiltinExecutor[] Executors => new[]
        {
            // substring(string, number)
            new BuiltinExecutor
            {
                InputArgs = new Args(typeof(string), typeof(double)),
                IsQueryValueSupported = true,
                ExecutorFunc = CreateStart
            },
            // substring(string, number, number)
            new BuiltinExecutor
            {
                InputArgs = new Args(typeof(string), typeof(double), typeof(double)),
                IsQueryValueSupported = true,
                ExecutorFunc = CreateStartLength
            },
        };

        static Expression CreateStart(Expression[] args, Expression[] contextArgs)
        {
            return Expression.Call(typeof(Substring).GetMethod("RuntimeStart", BindingFlags.NonPublic | BindingFlags.Static), args);
        }

        static Expression CreateStartLength(Expression[] args, Expression[] contextArgs)
        {
            return Expression.Call(typeof(Substring).GetMethod("RuntimeStartLength", BindingFlags.NonPublic | BindingFlags.Static), args);
        }

        // ReSharper disable once UnusedMember.Local
        static string RuntimeStart(QueryValue input, QueryValue start)
        {
            if (input?.ValueType != QueryValueType.String || start?.ValueType != QueryValueType.Double)
            {
                return Undefined.Instance;
            }

            string inputString = (string)input.Value;
            double startIndex = (double)start.Value;

            bool isValid = !inputString.IsNullOrUndefined() && startIndex.IsDefined() && startIndex <= inputString.Length && startIndex >= 0;
            return isValid ? inputString.Substring((int)startIndex) : Undefined.Instance;
        }

        // ReSharper disable once UnusedMember.Local
        static string RuntimeStartLength(QueryValue input, QueryValue start, QueryValue length)
        {
            if (input?.ValueType != QueryValueType.String || start?.ValueType != QueryValueType.Double || length?.ValueType != QueryValueType.Double)
            {
                return Undefined.Instance;
            }

            string inputString = (string)input.Value;
            double startIndex = (double)start.Value;
            double lengthValue = (double)length.Value;

            bool isValid = !inputString.IsNullOrUndefined() &&
                startIndex.IsDefined() && startIndex < inputString.Length && startIndex >= 0 &&
                lengthValue.IsDefined() && lengthValue >= 0 && lengthValue <= (inputString.Length - startIndex);

            return isValid ? inputString.Substring((int)startIndex, (int)lengthValue) : Undefined.Instance;
        }
    }
}
