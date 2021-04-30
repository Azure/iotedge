// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Config
{
    using Microsoft.Azure.Devices.Edge.Util;

    public class ModuleConfigBuilder : BaseModuleConfigBuilder
    {
        public ModuleConfigBuilder(string name, string image, bool shouldRestart)
            : this(name, image, Option.None<string>(), shouldRestart)
        {
        }

        public ModuleConfigBuilder(string name, string image, Option<string> createOptions, bool shouldRestart)
            : base(name, image)
        {
            string restartPolicy = string.Empty;
            if (shouldRestart)
            {
                restartPolicy = "always";
            }
            else
            {
                restartPolicy = "never";
            }

            this.WithDeployment(
                new[]
                {
                    ("restartPolicy", restartPolicy),
                    ("status", "running")
                });
            createOptions.ForEach(s => this.WithSettings(new[] { ("createOptions", s) }));
        }
    }
}
