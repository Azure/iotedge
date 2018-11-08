// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Query.Builtins
{
    using System;
    using System.Linq.Expressions;

    public class BuiltinExecutor
    {
        public IArgs ContextArgs { get; set; }

        public Func<Expression[], Expression[], Expression> ExecutorFunc { get; set; }

        public IArgs InputArgs { get; set; }

        public bool IsQueryValueSupported { get; set; }
    }
}
