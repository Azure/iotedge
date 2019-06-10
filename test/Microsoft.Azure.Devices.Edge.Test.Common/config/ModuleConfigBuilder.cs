// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Config
{
    using Microsoft.Azure.Devices.Edge.Util;

    public class ModuleConfigBuilder : BaseModuleConfigBuilder
    {
        public ModuleConfigBuilder(string name, string image)
            : this(name, image, false, Option.None<string>())
        {
        }

        public ModuleConfigBuilder(string name, string image, bool system, Option<string> createOptions)
            : base(name, image, system)
        {
            this.Deployment["restartPolicy"] = "always";
            this.Deployment["status"] = "running";
            createOptions.ForEach(s => this.Settings["createOptions"] = s);
        }
    }
}
