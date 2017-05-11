// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core.Query.Builtins
{
    using System;

    public class AnyArgs : IArgs
    {
        public Type[] Types { get; } = new Type[0];

        public int Arity { get; }

        public AnyArgs(int arity)
        {
            this.Arity = arity;
        }

        public bool Match(Type[] args, bool forceCheckQueryValue)
        {
            return args.Length == this.Arity;
        }
    }
}