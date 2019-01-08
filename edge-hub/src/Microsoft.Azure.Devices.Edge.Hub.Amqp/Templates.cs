// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using Microsoft.Azure.Devices.Common;

    public static class Templates
    {
        public const string DevicePathPrefix = "/devices/";
        public const string ModulesPathPrefix = "/modules/";

        public const string DeviceIdTemplateParameterName = "deviceid";
        public const string ModuleIdTemplateParameterName = "moduleid";

        public const string DeviceTelemetryStreamUriFormat = "/devices/{0}/messages/events";
        public const string ModuleTelemetryStreamUriFormat = "/devices/{0}/modules/{1}/messages/events";

        public const string DeviceC2DStreamUriFormat = "/devices/{0}/messages/deviceBound";
        public const string ModuleC2DStreamUriFormat = "/devices/{0}/modules/{1}/messages/deviceBound";

        public static readonly UriPathTemplate CbsReceiveTemplate = new UriPathTemplate("/$cbs");
        public static readonly UriPathTemplate DeviceResourceTemplate = new UriPathTemplate(DevicePathPrefix + "{{{0}}}".FormatInvariant(DeviceIdTemplateParameterName));
        public static readonly UriPathTemplate ModuleResourceTemplate = new UriPathTemplate((DevicePathPrefix + "{{{0}}}" + ModulesPathPrefix + "{{{1}}}").FormatInvariant(DeviceIdTemplateParameterName, ModuleIdTemplateParameterName));
        public static readonly UriPathTemplate DeviceEventsTemplate = new UriPathTemplate(DeviceTelemetryStreamUriFormat.FormatInvariant("{" + DeviceIdTemplateParameterName + "}"));
        public static readonly UriPathTemplate ModuleEventsTemplate = new UriPathTemplate(ModuleTelemetryStreamUriFormat.FormatInvariant("{" + DeviceIdTemplateParameterName + "}", "{" + ModuleIdTemplateParameterName + "}"));
        public static readonly UriPathTemplate DeviceFromDeviceBoundTemplate = new UriPathTemplate(DeviceC2DStreamUriFormat.FormatInvariant("{" + DeviceIdTemplateParameterName + "}"));
        public static readonly UriPathTemplate ModuleFromDeviceBoundTemplate = new UriPathTemplate(ModuleC2DStreamUriFormat.FormatInvariant("{" + DeviceIdTemplateParameterName + "}", "{" + ModuleIdTemplateParameterName + "}"));

        public static class Twin
        {
            public const string DeviceTwinMessageStreamUriFormat = "/devices/{0}/twin";
            public const string ModuleTwinMessageStreamUriFormat = "/devices/{0}/modules/{1}/twin";
            public const string DeviceBoundMethodCallUriFormat = "/devices/{0}/methods/deviceBound";
            public const string ModuleDeviceBoundMethodCallUriFormat = "/devices/{0}/modules/{1}/methods/deviceBound";
            public static readonly UriPathTemplate DeviceBoundMethodCallTemplate = new UriPathTemplate(DeviceBoundMethodCallUriFormat.FormatInvariant("{" + DeviceIdTemplateParameterName + "}"));
            public static readonly UriPathTemplate ModuleDeviceBoundMethodCallTemplate = new UriPathTemplate(ModuleDeviceBoundMethodCallUriFormat.FormatInvariant("{" + DeviceIdTemplateParameterName + "}", "{" + ModuleIdTemplateParameterName + "}"));
            public static readonly UriPathTemplate TwinStreamTemplate = new UriPathTemplate(DeviceTwinMessageStreamUriFormat.FormatInvariant("{" + DeviceIdTemplateParameterName + "}"));
            public static readonly UriPathTemplate ModuleTwinStreamTemplate = new UriPathTemplate(ModuleTwinMessageStreamUriFormat.FormatInvariant("{" + DeviceIdTemplateParameterName + "}", "{" + ModuleIdTemplateParameterName + "}"));
        }
    }
}
