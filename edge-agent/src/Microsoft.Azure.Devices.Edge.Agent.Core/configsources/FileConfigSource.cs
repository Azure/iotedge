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
    using Microsoft.Extensions.Logging;

    public class FileConfigSource : IConfigSource
    {
        const double FileChangeWatcherDebounceInterval = 500;

        readonly ISerde<ModuleSet> moduleSetSerde;
        readonly FileSystemWatcher watcher;
        readonly string configFilePath;
        readonly IDisposable watcherSubscription;
        readonly AtomicReference<ModuleSet> current;
        readonly AsyncLock sync;
        readonly ILogger logger;

        FileConfigSource(FileSystemWatcher watcher, ModuleSet initial, ISerde<ModuleSet> moduleSetSerde, ILoggerFactory loggingFactory)
        {
            this.logger = Preconditions.CheckNotNull(loggingFactory, nameof(loggingFactory))
                .CreateLogger<FileConfigSource>();
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
        }

        public static async Task<FileConfigSource> Create(string configFilePath, ISerde<ModuleSet> moduleSetSerde, ILoggerFactory loggingFactory)
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
            return new FileConfigSource(watcher, initial, moduleSetSerde, loggingFactory);
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

                ModuleSet newConfig = await this.GetConfigAsync();
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
                this.logger.LogError(0, ex, $"Error reading new configuration file, {this.configFilePath}");
                this.OnFailed(ex);
            }
        }

        public async Task<ModuleSet> GetConfigAsync()
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

        public event EventHandler<Diff> Changed;

        protected virtual void OnChanged(Diff diff)
        {
            this.Changed?.Invoke(this, diff);
        }

        public event EventHandler<Exception> Failed;

        protected virtual void OnFailed(Exception ex)
        {
            this.Failed?.Invoke(this, ex);
        }

        public void Dispose()
        {
            this.watcherSubscription.Dispose();
            this.watcher.Dispose();
        }
    }
}
