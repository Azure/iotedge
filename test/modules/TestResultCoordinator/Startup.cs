// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator
{
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using TestResultCoordinator.Service;
    using TestResultCoordinator.Storage;

    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            this.Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public async void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
            services.AddHostedService<TestResultReportingService>();
            services.AddHostedService<TestResultEventReceivingService>();
            services.AddSingleton<ITestOperationResultStorage>(await TestOperationResultStorage.Create(
                Settings.Current.StoragePath,
                new SystemEnvironment(),
                Settings.Current.OptimizeForPerformance,
                Settings.Current.ResultSources));
        }

        // TODO: Remove warning disable for Obsolete when project is moved to dotnetcore 3.0
#pragma warning disable 612, 618
        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseMvc();
        }
#pragma warning restore 612, 618
    }
}
