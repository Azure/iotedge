// Copyright (c) Microsoft. All rights reserved.
using Xunit;

// Ordering tests based on collection names so as to make the exclusive tests run after the non-exclusive tests. This helps
// reduce the time taken to re-create test fixtures as the intermingling of runs for both exclusive and non-exclusive tests would otherwise
// lead to multiple re-creation of test fixtures unnecessarily which adds to the total time taken to run all the tests.
[assembly: TestCollectionOrderer("Microsoft.Azure.Devices.Edge.Util.Test.DisplayNameOrderer", "Microsoft.Azure.Devices.Edge.Util.Test")]
[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly, DisableTestParallelization = true)]
