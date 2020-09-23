// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using Microsoft.Azure.Devices.Edge.Util;
    public interface INestedEdgeParentUriParser
    {
        public Option<string> ParseURI(string uri);
    }
}
