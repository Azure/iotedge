// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core.Query.Builtins
{
    using System;
    using System.Linq;
    using Microsoft.Azure.Devices.Routing.Core.Query.Types;
    using Microsoft.Azure.Devices.Routing.Core.Util;

    public class Args : IArgs
    {
        public Type[] Types { get; }

        public int Arity => this.Types.Length;

        public Args(params Type[] args)
        {
            this.Types = Preconditions.CheckNotNull(args);
        }

        public bool Match(Type[] args, bool matchQueryValue)
        {
            return this.Types.Length == args.Length && this.Types.Zip(args,
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
