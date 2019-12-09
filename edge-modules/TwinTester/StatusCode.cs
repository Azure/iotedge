// Copyright (c) Microsoft. All rights reserved.
namespace TwinTester
{
    enum StatusCode
    {
        Success = 200,
        DesiredPropertyUpdateNoCallbackReceived = 501,
        DesiredPropertyUpdateNotInEdgeTwin = 502,
        DesiredPropertyUpdateTotalFailure = 503,
        ReportedPropertyUpdateCallFailure = 504,
        ReportedPropertyUpdateNotInCloudTwin = 505
    }
}
