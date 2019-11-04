// Copyright (c) Microsoft. All rights reserved.
using Xunit;

[assembly: TestCollectionOrderer("Microsoft.Azure.Devices.Edge.Hub.E2E.Test.DisplayNameOrderer", "Microsoft.Azure.Devices.Edge.Hub.E2E.Test")]
[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly, DisableTestParallelization = true)]
