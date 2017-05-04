// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Edge.Util.Test.Common
{
    using System.Collections.Generic;
    using Xunit.Abstractions;
    using Xunit.Sdk;

    public class BvtDiscoverer : ITraitDiscoverer
    {
        public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
        {
            yield return new KeyValuePair<string, string>("Category", "Bvt");
        }
    }
}
