// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.ConfigSources
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Reactive;
    using System.Reactive.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    public class FileConfigSource : BaseConfigSource
    {
        const double FileChangeWatcherDebounceInterval = 500;

        readonly ISerde<ModuleSet> moduleSetSerde;
        readonly FileSystemWatcher watcher;
        readonly string configFilePath;
        readonly IDisposable watcherSubscription;
        readonly AtomicReference<ModuleSet> current;
        readonly AsyncLock sync;

        FileConfigSource(FileSystemWatcher watcher, ModuleSet initial, ISerde<ModuleSet> moduleSetSerde, IConfiguration configuration)
            : base(configuration)
        {
            this.watcher = Preconditions.CheckNotNull(watcher, nameof(watcher));
            this.current = new AtomicReference<ModuleSet>(Preconditions.CheckNotNull(initial, nameof(initial)));
            this.moduleSetSerde = Preconditions.CheckNotNull(moduleSetSerde, nameof(moduleSetSerde));

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

        public static async Task<FileConfigSource> Create(string configFilePath, ISerde<ModuleSet> moduleSetSerde, IConfiguration configuration)
        {
            string path = Preconditions.CheckNonWhiteSpace(Path.GetFullPath(configFilePath), nameof(configFilePath));
            Preconditions.CheckNotNull(moduleSetSerde, nameof(moduleSetSerde));
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Invalid config file path", path);
            }

            string directoryName = Path.GetDirectoryName(path);
            string fileName = Path.GetFileName(path);

            string json = await ReadFileAsync(path);
            ModuleSet initial = moduleSetSerde.Deserialize(json);

            var watcher = new FileSystemWatcher(directoryName, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite
            };
            return new FileConfigSource(watcher, initial, moduleSetSerde, configuration);
        }

        void AssignCurrentModuleSet(ModuleSet updated)
        {
            ModuleSet snapshot = this.current.Value;
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
                ModuleSet newConfig = await this.GetModuleSetAsync();
                Diff diff;
                using (await this.sync.LockAsync())
                {
                    ModuleSet snapshot = this.current.Value;
                    diff = snapshot == null
                        ? Diff.Create(newConfig.Modules.Values.ToArray())
                        : newConfig.Diff(snapshot);
                    this.AssignCurrentModuleSet(newConfig);
                }
                this.OnChanged(diff);
            }
            catch (Exception ex) when (!ex.IsFatal())
            {
                Events.NewConfigurationFailed(ex, this.configFilePath);
                this.OnFailed(ex);
            }
        }

        public override async Task<ModuleSet> GetModuleSetAsync()
        {
            string json = await ReadFileAsync(this.configFilePath);
            return this.moduleSetSerde.Deserialize(json);
        }

        static async Task<string> ReadFileAsync(string configFilePath)
        {
            using (var reader = new StreamReader(File.OpenRead(configFilePath)))
            {
                return await reader.ReadToEndAsync();
            }
        }

        public override event EventHandler<Diff> ModuleSetChanged;

        protected virtual void OnChanged(Diff diff)
        {
            this.ModuleSetChanged?.Invoke(this, diff);
        }

        public override event EventHandler<Exception> ModuleSetFailed;

        protected virtual void OnFailed(Exception ex)
        {
            this.ModuleSetFailed?.Invoke(this, ex);
        }

        public override void Dispose()
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
