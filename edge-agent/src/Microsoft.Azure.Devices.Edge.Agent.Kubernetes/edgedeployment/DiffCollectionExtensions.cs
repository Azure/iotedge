// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public static class DiffCollectionExtensions
    {
        public static Diff<T> Diff<T>(this IEnumerable<T> desired, IEnumerable<T> existing, Func<T, string> selector)
        {
            var either = new Set<T>(desired.ToDictionary(selector));
            var other = new Set<T>(existing.ToDictionary(selector));

            return either.Diff(other);
        }
    }
}
