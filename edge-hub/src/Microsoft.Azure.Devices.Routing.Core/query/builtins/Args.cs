// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Query.Builtins
{
    using System;
    using System.Linq;

    using Microsoft.Azure.Devices.Routing.Core.Query.Types;
    using Microsoft.Azure.Devices.Routing.Core.Util;

    public class Args : IArgs
    {
        public Args(params Type[] args)
        {
            this.Types = Preconditions.CheckNotNull(args);
        }

        public int Arity => this.Types.Length;

        public Type[] Types { get; }

        public bool Match(Type[] args, bool matchQueryValue)
        {
            return this.Types.Length == args.Length && this.Types.Zip(
                       args,
                       (t, g) => t == g || IsMatchInternal(t, g, matchQueryValue)).All(_ => _);
        }

        static bool IsMatchInternal(Type type1, Type type2, bool matchQueryValue)
        {
            if (matchQueryValue)
            {
                return type1 == type2 ||
                       (type2 == typeof(QueryValue) && QueryValue.IsSupportedType(type1)) ||
                       (type1 == typeof(QueryValue) && QueryValue.IsSupportedType(type2));
            }
            else
            {
                return type1 == type2;
            }
        }
    }
}
