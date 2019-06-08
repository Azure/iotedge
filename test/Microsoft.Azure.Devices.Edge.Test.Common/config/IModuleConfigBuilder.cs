// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Config
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IModuleConfigBuilder
    {
        string Name { get; }

        bool System { get; }

        IModuleConfigBuilder WithDesiredProperties(IEnumerable<(string, object)> properties);

        IModuleConfigBuilder WithDesiredProperties(IDictionary<string, object> properties);

        IModuleConfigBuilder WithEnvironment(IEnumerable<(string, string)> env);

        IModuleConfigBuilder WithProxy(Option<Uri> proxy, Protocol protocol);

        ModuleConfiguration Build();
    }
}
