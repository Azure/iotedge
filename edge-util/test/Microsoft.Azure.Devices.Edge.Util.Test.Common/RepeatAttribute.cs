// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Test.Common
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Xunit.Sdk;

    [TraitDiscoverer("Microsoft.Azure.Devices.Edge.Util.Test.Common.IntegrationDiscoverer", "Microsoft.Azure.Devices.Edge.Util.Test.Common")]
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class RepeatAttribute : DataAttribute, ITraitAttribute
    {
        private readonly int _count;

        public RepeatAttribute(int count)
        {
            const int minimumCount = 1;
            if (count < minimumCount)
            {
                throw new ArgumentOutOfRangeException(
                    paramName: nameof(count),
                    message: "Repeat count must be greater than 0.");
            }
            _count = count;
        }

        public override IEnumerable<object[]> GetData(MethodInfo testMethod)
        {
            foreach (var iterationNumber in Enumerable.Range(start: 1, count: _count))
            {
                yield return new object[] { iterationNumber };
            }
        }
    }
}
