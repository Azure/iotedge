// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Config
{
    using Microsoft.Azure.Devices.Edge.Util;

    public class ModuleConfigBuilder : BaseModuleConfigBuilder
    {
        public ModuleConfigBuilder(string name, string image)
            : this(name, image, Option.None<string>())
        {
        }

        public ModuleConfigBuilder(string name, string image, Option<string> createOptions)
            : base(name, image)
        {
            this.Deployment["restartPolicy"] = "always";
            this.Deployment["status"] = "running";
            createOptions.ForEach(s => this.Settings["createOptions"] = s);
        }
    }
}
