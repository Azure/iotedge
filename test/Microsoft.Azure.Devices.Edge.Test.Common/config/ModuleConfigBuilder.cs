// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Config
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util;

    public class ModuleConfigBuilder : BaseModuleConfigBuilder
    {
        protected Option<string> CreateOptions { get; }

        protected string RestartPolicy { get; }

        protected string Status { get; }

        public ModuleConfigBuilder(string name, string image)
            : this(name, image, false, Option.None<string>())
        {
        }

        public ModuleConfigBuilder(string name, string image, bool system, Option<string> createOptions)
            : base(name, image, system)
        {
            this.CreateOptions = createOptions;
            this.RestartPolicy = "always";
            this.Status = "running";
        }

        protected override ModuleConfigurationInternal BuildInternal()
        {
            ModuleConfigurationInternal config = base.BuildInternal();

            var deployment = config.Deployment;
            deployment["status"] = this.Status;
            deployment["restartPolicy"] = this.RestartPolicy;

            this.CreateOptions.ForEach(
                s =>
                {
                    var settings = deployment["settings"] as IDictionary<string, object>;
                    if (settings == null)
                    {
                        throw new Exception("Object 'settings' not set in base class");
                    }
                    settings["createOptions"] = s;
                });

            return new ModuleConfigurationInternal(this.Name, this.System, deployment, config.DesiredProperties);
        }
    }
}
