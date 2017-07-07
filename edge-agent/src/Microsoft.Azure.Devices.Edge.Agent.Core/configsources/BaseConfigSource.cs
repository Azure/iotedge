// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.ConfigSources
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;

    public abstract class BaseConfigSource : IConfigSource
    {
        protected BaseConfigSource(IConfiguration configuration)
        {
            this.Configuration = Preconditions.CheckNotNull(configuration, nameof(configuration));
        }

        public abstract event EventHandler<Diff> ModuleSetChanged;
        public abstract event EventHandler<Exception> ModuleSetFailed;

        public abstract void Dispose();

        public abstract Task<ModuleSet> GetModuleSetAsync();

        public IConfiguration Configuration { get; }
    }
}
