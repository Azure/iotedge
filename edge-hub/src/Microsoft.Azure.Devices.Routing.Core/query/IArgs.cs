// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core.Query
{
    using System;

    public interface IArgs
    {
        Type[] Types { get; }

        int Arity { get; }

        bool Match(Type[] args, bool matchQueryValue);
    }
}