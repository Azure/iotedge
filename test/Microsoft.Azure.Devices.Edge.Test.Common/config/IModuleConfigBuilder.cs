// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Config
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IModuleConfigBuilder
    {
        string Name { get; }

        IModuleConfigBuilder WithDesiredProperties(IDictionary<string, object> properties);

        IModuleConfigBuilder WithEnvironment(params (string, string)[] env);

        IModuleConfigBuilder WithProxy(Option<Uri> proxy);

        IModuleConfigBuilder WithSettings(params (string, string)[] settings);

        ModuleConfiguration Build();
    }
}
