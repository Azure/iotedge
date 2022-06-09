// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator
{
    enum TestMode
    {
        // Connectivity test mode is for connectivity tests, which are end-to-end
        // tests that start up edge on a device and simulate low bandwidth and spotty
        // connectivity scenarios
        Connectivity,
        // LongHaul test mode is for LongHaul tests, which are end-to-end tests
        // that start up edge on a device and perform all edge scenarios (e.g.
        // messages, twin updates, direct methods, etc.) over a non finite period of time
        // (usually a week)
        LongHaul
    }
}
