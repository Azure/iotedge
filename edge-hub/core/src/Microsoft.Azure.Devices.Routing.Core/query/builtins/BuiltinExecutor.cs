// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Query.Builtins
{
    using System;
    using System.Linq.Expressions;

    public class BuiltinExecutor
    {
        public IArgs InputArgs { get; set; }

        public IArgs ContextArgs { get; set; }

        public bool IsQueryValueSupported { get; set; }

        public Func<Expression[], Expression[], Expression> ExecutorFunc { get; set; }
    }
}
