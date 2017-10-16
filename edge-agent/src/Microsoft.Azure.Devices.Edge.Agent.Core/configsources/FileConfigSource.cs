// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.ConfigSources
{
    using System;
    using System.IO;
    using System.Reactive;
    using System.Reactive.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    public class FileConfigSource : IConfigSource
    {
        const double FileChangeWatcherDebounceInterval = 500;

        readonly FileSystemWatcher watcher;
        readonly string configFilePath;
        readonly IDisposable watcherSubscription;
        readonly AtomicReference<AgentConfig> current;
        readonly AsyncLock sync;        

        FileConfigSource(FileSystemWatcher watcher, AgentConfig initial, IConfiguration configuration)
        {
            this.watcher = Preconditions.CheckNotNull(watcher, nameof(watcher));
            this.Configuration = Preconditions.CheckNotNull(configuration, nameof(configuration));
            this.current = new AtomicReference<AgentConfig>(Preconditions.CheckNotNull(initial, nameof(initial)));
            
            this.configFilePath = Path.Combine(this.watcher.Path, this.watcher.Filter);

            this.sync = new AsyncLock();
            this.watcherSubscription = Observable
                .FromEventPattern<FileSystemEventArgs>(this.watcher, "Changed")
                // Rx.NET's "Throttle" is really "Debounce". An unfortunate naming mishap.
                .Throttle(TimeSpan.FromMilliseconds(FileChangeWatcherDebounceInterval))
                .Subscribe(this.WatcherOnChanged);
            this.watcher.EnableRaisingEvents = true;
            Events.Created(this.configFilePath);
        }

        public static async Task<FileConfigSource> Create(string configFilePath, IConfiguration configuration)
        {
            string path = Preconditions.CheckNonWhiteSpace(Path.GetFullPath(configFilePath), nameof(configFilePath));
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Invalid config file path", path);
            }

            string directoryName = Path.GetDirectoryName(path);
            string fileName = Path.GetFileName(path);

            AgentConfig initial = await ReadFromDisk(path);
            var watcher = new FileSystemWatcher(directoryName, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite
            };
            return new FileConfigSource(watcher, initial, configuration);
        }

        public IConfiguration Configuration { get; }

        static async Task<AgentConfig> ReadFromDisk(string path)
        {
            string json = await DiskFile.ReadAllAsync(path);
            var agentConfig = json.FromJson<AgentConfig>();
            return agentConfig;
        }

        void UpdateCurrent(AgentConfig updated)
        {
            AgentConfig snapshot = this.current.Value;
            if (!this.current.CompareAndSet(snapshot, updated))
            {
                throw new InvalidOperationException("Invalid update current moduleset operation.");
            }
        }

        async void WatcherOnChanged(EventPattern<FileSystemEventArgs> args)
        {
            if ((args.EventArgs.ChangeType & WatcherChangeTypes.Changed) != WatcherChangeTypes.Changed)
                return;

            try
            {
                using (await this.sync.LockAsync())
                {
                    AgentConfig newConfig = await ReadFromDisk(this.configFilePath);
                    this.UpdateCurrent(newConfig);
                }
            }
            catch (Exception ex) when (!ex.IsFatal())
            {
                Events.NewConfigurationFailed(ex, this.configFilePath);
            }
        }

        public Task<AgentConfig> GetAgentConfigAsync() => Task.FromResult(this.current.Value);

        public void Dispose()
        {
            this.watcherSubscription.Dispose();
            this.watcher.Dispose();
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<FileConfigSource>();
            const int IdStart = AgentEventIds.FileConfigSource;

            enum EventIds
            {
                Created = IdStart,
                NewConfigurationFailed
            }

            public static void Created(string filename)
            {
                Log.LogDebug((int)EventIds.Created, $"FileConfigSource created with filename {filename}");
            }

            public static void NewConfigurationFailed(Exception exception, string filename)
            {
                Log.LogError((int)EventIds.NewConfigurationFailed, exception, $"FileConfigSource failed reading new configuration file, {filename}");
            }

        }
    }
}
