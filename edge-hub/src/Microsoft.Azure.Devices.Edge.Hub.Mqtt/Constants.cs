// Copyright (c) Microsoft. All rights reserved.
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
    }
}