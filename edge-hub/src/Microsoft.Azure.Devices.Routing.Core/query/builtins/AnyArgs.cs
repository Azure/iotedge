// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Query.Builtins
{
    using System;

    public class AnyArgs : IArgs
    {
        public AnyArgs(int arity)
        {
            this.Arity = arity;
        }

        public int Arity { get; }

        public Type[] Types { get; } = new Type[0];

        public bool Match(Type[] args, bool forceCheckQueryValue)
        {
            return args.Length == this.Arity;
        }
    }
}
