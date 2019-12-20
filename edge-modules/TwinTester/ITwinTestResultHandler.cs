// Copyright (c) Microsoft. All rights reserved.
namespace TwinTester
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Shared;

    interface ITwinTestResultHandler
    {
        Task HandleDesiredPropertyUpdateAsync(string propertyKey);

        Task HandleDesiredPropertyReceivedAsync(TwinCollection properties);

        Task HandleTwinValidationStatusAsync(string status);

        Task HandleReportedPropertyUpdateAsync(string propertyKey);

        Task HandleReportedPropertyUpdateExceptionAsync(string propertyKey, string failureStatus);
    }
}
