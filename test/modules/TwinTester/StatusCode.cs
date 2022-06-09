// Copyright (c) Microsoft. All rights reserved.
namespace TwinTester
{
    enum StatusCode
    {
        ValidationSuccess = 200,
        DesiredPropertyUpdated = 201,
        DesiredPropertyReceived = 202,
        ReportedPropertyReceived = 203,
        ReportedPropertyUpdated = 204,
        DesiredPropertyUpdateNoCallbackReceived = 501,
        DesiredPropertyUpdateNotInEdgeTwin = 502,
        DesiredPropertyUpdateTotalFailure = 503,
        ReportedPropertyUpdateCallFailure = 504,
        ReportedPropertyUpdateNotInCloudTwin = 505,
        ReportedPropertyUpdateNotInCloudTwinAfterCleanUpThreshold = 506,
        ReportedPropertyReceivedAfterThreshold = 205,
    }
}
