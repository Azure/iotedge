// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using Microsoft.AspNetCore.Builder;

    public class AuthAgentStartup
    {
        readonly AuthAgentProtocolHeadConfig config;

        public AuthAgentStartup(AuthAgentProtocolHeadConfig config)
        {
            this.config = config;
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapControllerRoute(
                    "authenticate",
                    this.config.BaseUrl,
                    defaults: new { controller = "AuthAgent", action = "HandleAsync" });
            });
        }
    }
}
