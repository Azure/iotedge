// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service
{
    using System.Runtime.Serialization;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ServiceIdentityStatus
    {
        [EnumMember(Value = "enabled")]
        Enabled,

        [EnumMember(Value = "disabled")]
        Disabled
    }
}
