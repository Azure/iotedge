// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Query
{
    using System;

    public interface IArgs
    {
        int Arity { get; }

        Type[] Types { get; }

        bool Match(Type[] args, bool matchQueryValue);
    }
}
