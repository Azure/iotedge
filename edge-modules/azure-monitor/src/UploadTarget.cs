// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Azure.Devices.Edge.Azure.Monitor
{
    using System;

    public enum UploadTarget
    {
        IotMessage,
        AzureMonitor
    }

    public static class UploadTargetHelper
    {
        public static UploadTarget ToUploadTarget(this string value) =>
            !string.IsNullOrWhiteSpace(value) && Enum.TryParse(value, true, out UploadTarget uploadTarget)
                ? uploadTarget
                : UploadTarget.AzureMonitor;
    }
}