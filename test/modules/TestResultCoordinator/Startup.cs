// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Storage.RocksDb;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using TestResultCoordinator.Services;
    using TestResultCoordinator.Storage;

    public class Startup
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger(nameof(Startup));

        public Startup(IConfiguration configuration)
        {
            this.Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            Logger.LogInformation("Calling Startup.ConfigureServices");

            services.AddControllers().AddNewtonsoftJson();

            HashSet<string> sources = Settings.Current.GetResultSourcesAsync(Logger).Result;
            Logger.LogInformation($"Result sources defined:{Environment.NewLine} {string.Join(Environment.NewLine + Enumerable.Repeat(" ", 5), sources)}");

            IStoreProvider storeProvider;

            try
            {
                IDbStoreProvider dbStoreprovider = DbStoreProvider.Create(
                    new RocksDbOptionsProvider(
                        new SystemEnvironment(),
                        Settings.Current.OptimizeForPerformance,
                        Option.None<ulong>(),
                        Option.None<int>(),
                        Option.None<StorageLogLevel>()),
                    this.GetStoragePath(Settings.Current.StoragePath),
                    sources);

                storeProvider = new StoreProvider(dbStoreprovider);
            }
            catch (Exception ex) when (!ExceptionEx.IsFatal(ex))
            {
                Logger.LogError(ex, "Error creating RocksDB store. Falling back to in-memory store.");
                storeProvider = new StoreProvider(new InMemoryDbStoreProvider());
            }

            services.AddSingleton<ITestOperationResultStorage>(
                TestOperationResultStorage.CreateAsync(
                    storeProvider,
                    sources).Result);

            Settings.Current.TestResultReportingServiceSettings.ForEach(() => services.AddHostedService<TestResultReportingService>());
            Settings.Current.TestResultEventReceivingServiceSettings.ForEach(() => services.AddHostedService<TestResultEventReceivingService>());

            Logger.LogInformation("Calling Startup.ConfigureServices Completed.");
        }

        // TODO: Figure out how to use developer exception page with IWebHostEnvironment
#pragma warning disable 612, 618
        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
#pragma warning restore 612, 618

        string GetStoragePath(string baseStoragePath)
        {
            if (string.IsNullOrWhiteSpace(baseStoragePath) || !Directory.Exists(baseStoragePath))
            {
                baseStoragePath = Path.GetTempPath();
            }

            string storagePath = Path.Combine(baseStoragePath, "TestResultCoordinator");
            Directory.CreateDirectory(storagePath);
            return storagePath;
        }
    }
}
