// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    public class Constants
    {
        public const string SegmentSeparator = "/";
        public const string ServicePrefix = "$iothub" + SegmentSeparator;
        public const string OutboundUriC2D = "C2D";
        public const string OutboundUriModuleEndpoint = "ModuleEndpoint";
        public const string OutboundUriTwinEndpoint = "TwinEndpoint";
        public const string OutboundUriTwinDesiredPropertyUpdate = "TwinDesiredPropertyUpdate";
        public const string TwinPrefix = ServicePrefix + "twin" + SegmentSeparator;
        public const string MethodPrefix = ServicePrefix + "methods" + SegmentSeparator + "res" + SegmentSeparator;
        public const string ModuleIdTemplateParameter = "moduleId";
        public const string TwinLockToken = "r";
        public const string WebSocketSubProtocol = "mqtt";
    }
}
