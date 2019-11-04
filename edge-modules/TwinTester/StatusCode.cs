// Copyright (c) Microsoft. All rights reserved.
namespace TwinTester
{
    enum StatusCode
    {
        Success = 200,
        DesiredPropertyUpdateNoCallbackReceived = 100,
        DesiredPropertyUpdateNotInEdgeTwin = 101,
        DesiredPropertyUpdateTotalFailure = 102,
        ReportedPropertyUpdateCallFailure = 103,
        ReportedPropertyUpdateNotInCloudTwin = 104
    }
}
