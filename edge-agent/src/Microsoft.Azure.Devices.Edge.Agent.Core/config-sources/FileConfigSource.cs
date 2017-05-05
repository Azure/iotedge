namespace Microsoft.Azure.Devices.Edge.Agent.Core.ConfigSources
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reactive;
    using System.Reactive.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;

    public class FileConfigSource : IConfigSource
    {
        const double FileChangeWatcherDebounceInterval = 500;

        readonly ModuleSetSerde moduleSetSerde;
        readonly FileSystemWatcher watcher;
        readonly string configFilePath;
        readonly IDisposable watcherSubscription;
        AtomicReference<ModuleSet> current;
        readonly AsyncLock sync;

        FileConfigSource(string configFilePath, ModuleSetSerde moduleSetSerde)
        {
            this.moduleSetSerde = Preconditions.CheckNotNull(moduleSetSerde, nameof(moduleSetSerde));
            this.configFilePath = Preconditions.CheckNonWhiteSpace(Path.GetFullPath(configFilePath), nameof(configFilePath));
            if (!File.Exists(this.configFilePath))
            {
                throw new FileNotFoundException("Invalid config file path", this.configFilePath);
            }

            string directoryName = Path.GetDirectoryName(this.configFilePath);
            string fileName = Path.GetFileName(this.configFilePath);
            this.sync = new AsyncLock();
            this.watcher = new FileSystemWatcher(directoryName, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite
            };
            this.watcherSubscription = Observable
                .FromEventPattern<FileSystemEventArgs>(this.watcher, "Changed")
                // Rx.NET's "Throttle" is really "Debounce". An unfortunate naming mishap.
                .Throttle(TimeSpan.FromMilliseconds(FileChangeWatcherDebounceInterval))
                .Subscribe(this.WatcherOnChanged);
        }

        public static async Task<FileConfigSource> Create(string configFilePath, ModuleSetSerde moduleSetSerde)
        {
            var configSource = new FileConfigSource(configFilePath, moduleSetSerde);

            // NOTE: We don't need to acquire a lock on `this.sync` here because at this point the
            // file watcher has not been started yet - so there is no way `WatcherOnChanged` will get
            // invoked.
            configSource.current = new AtomicReference<ModuleSet>(await configSource.GetConfigAsync());

            // This starts the file watcher.
            configSource.watcher.EnableRaisingEvents = true;

            return configSource;
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

        public async Task<ModuleSet> GetConfigAsync()
        {
            using (var reader = new StreamReader(File.OpenRead(this.configFilePath)))
            {
                string configJson = await reader.ReadToEndAsync();
                return this.moduleSetSerde.Deserialize(configJson);
            }
        }

        public event EventHandler<Diff> Changed;

        protected virtual void OnChanged(Diff diff)
        {
            this.Changed?.Invoke(this, diff);
        }

        public void Dispose()
        {
            this.watcherSubscription.Dispose();
            this.watcher.Dispose();
        }
    }
}
