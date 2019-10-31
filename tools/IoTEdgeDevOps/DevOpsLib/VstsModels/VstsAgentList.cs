// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib.VstsModels
{
    using System.Collections.Generic;

    class VstsAgentList
    {
        public int Count { get; set; }

        public IList<VstsAgent> Value { get; set; }
    }
}
