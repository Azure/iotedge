// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Query.Builtins
{
    using System;
    using System.Linq;
    using Microsoft.Azure.Devices.Routing.Core.Query.Types;
    using Microsoft.Azure.Devices.Routing.Core.Util;

    /// <summary>
    /// This adds support for variable sized argument lists. The object takes a list
    /// of types, like Args, but repeats the last type indefinitely when matching.
    /// For instance to encode the c# parameter list `func(int, string, params string[])`
    /// you would define a VarArgs as `new VarArgs(typeof(int), typeof(string), typeof(string))`.
    /// </summary>
    public class VarArgs : IArgs
    {
        public Type[] Types { get; }

        public int Arity => this.Types.Length;

        public VarArgs(params Type[] args)
        {
            this.Types = Preconditions.CheckNotNull(args);
        }

        public bool Match(Type[] args, bool matchQueryValue)
        {
            int n = this.Types.Length - 1;
            return args.Length >= n &&
                args.Take(n).Zip(this.Types, (a, t) => a == t || (matchQueryValue && a == typeof(QueryValue))).Aggregate(true, (acc, item) => acc && item) &&
                args.Skip(n).Select(a => a == this.Types[n] || (matchQueryValue && a == typeof(QueryValue))).Aggregate(true, (acc, item) => acc && item);
        }
    }
}
