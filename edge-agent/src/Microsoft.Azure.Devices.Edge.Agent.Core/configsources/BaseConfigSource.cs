// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.ConfigSources
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public abstract class BaseConfigSource : IConfigSource
    {
        readonly IDictionary<string, object> configurationMap;

        protected BaseConfigSource(IDictionary<string, object> configurationMap)
        {
            this.configurationMap = Preconditions.CheckNotNull(configurationMap);
        }

        public abstract event EventHandler<Diff> Changed;
        public abstract event EventHandler<Exception> Failed;

        public abstract void Dispose();

        public abstract Task<ModuleSet> GetModuleSetAsync();

        public bool ContainsKey(string key) => this.configurationMap.ContainsKey(Preconditions.CheckNonWhiteSpace(key, nameof(key)));

        public Option<T> GetValue<T>(string key) => Option.Some<T>((T)this.GetValue(key, typeof(T)).OrDefault());

        public Option<object> GetValue(string key, Type type)
        {
            Preconditions.CheckNonWhiteSpace(key, nameof(key));
            Preconditions.CheckNotNull(type, nameof(type));

            if(this.ContainsKey(key) == false || this.configurationMap[key].GetType() != type)
            {
                return Option.None<object>();
            }

            return Option.Some<object>(this.configurationMap[key]);
        }
    }
}
