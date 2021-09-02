// Copyright (c) Microsoft. All rights reserved.
namespace SimulatedTemperatureSensor
{
    using System;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;
    using ExponentialBackoff = Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling.ExponentialBackoff;

    class Program
    {
        const string MessageCountConfigKey = "MessageCount";
        const string SendDataConfigKey = "SendData";
        const string SendIntervalConfigKey = "SendInterval";

        static readonly ITransientErrorDetectionStrategy DefaultTimeoutErrorDetectionStrategy =
            new DelegateErrorDetectionStrategy(ex => ex.HasTimeoutException());

        static readonly RetryStrategy DefaultTransientRetryStrategy =
            new ExponentialBackoff(
                5,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(60),
                TimeSpan.FromSeconds(4));

        static readonly Guid BatchId = Guid.NewGuid();
        static readonly AtomicBoolean Reset = new AtomicBoolean(false);
        static readonly Random Rnd = new Random();
        static TimeSpan messageDelay;
        static bool sendData = true;

        public enum ControlCommandEnum
        {
            Reset = 0,
            NoOperation = 1
        }

        public static int Main() => MainAsync().Result;

        static async Task<int> MainAsync()
        {
            Console.WriteLine("SimulatedTemperatureSensor Main() started.");

            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config/appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            messageDelay = configuration.GetValue("MessageDelay", TimeSpan.FromSeconds(5));
            int messageCount = configuration.GetValue(MessageCountConfigKey, 500);
            var simulatorParameters = new SimulatorParameters
            {
                MachineTempMin = configuration.GetValue<double>("machineTempMin", 21),
                MachineTempMax = configuration.GetValue<double>("machineTempMax", 100),
                MachinePressureMin = configuration.GetValue<double>("machinePressureMin", 1),
                MachinePressureMax = configuration.GetValue<double>("machinePressureMax", 10),
                AmbientTemp = configuration.GetValue<double>("ambientTemp", 21),
                HumidityPercent = configuration.GetValue("ambientHumidity", 25)
            };

            Console.WriteLine(
                $"Initializing simulated temperature sensor to send {(SendUnlimitedMessages(messageCount) ? "unlimited" : messageCount.ToString())} "
                + $"messages, at an interval of {messageDelay.TotalSeconds} seconds.\n"
                + $"To change this, set the environment variable {MessageCountConfigKey} to the number of messages that should be sent (set it to -1 to send unlimited messages).");

            TransportType transportType = configuration.GetValue("ClientTransportType", TransportType.Amqp_Tcp_Only);

            string certificate = @"-----BEGIN CERTIFICATE-----
MIIEYDCCAkigAwIBAgICSCMwDQYJKoZIhvcNAQELBQAwHzEdMBsGA1UEAwwUaW90
ZWRnZWQgd29ya2xvYWQgY2EwHhcNMjEwODIwMTg1MjM3WhcNMjExMDI3MDAxMzMx
WjAPMQ0wCwYDVQQDDAR3aW4yMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKC
AQEAmq7nxndwD9jt3dnYmL38W9rAXKlHvcorXIn8nPDcna9m1rtc5BFqwX/cv3Y/
KC62QbRl4NKDpy4MKKhkbN0YhnlKvchZ5khz1YgffEQuu68Pnxd0FNNagXfQfCTG
noGkeNnEXGXK2HO0yc2/8w2LwBnfaop8R8hqxPaLVOuQiiPUHNHQnszWO3XxmORA
VoLbMDhV48DfA/A5ntq3+Y2vPyJl0nTzXYrelDTPoLi9kf6kX1Cq8J9TUNAqVNez
OgDvsY7SiGnwkedVWDIE8sh7FD1V9ogCfZet28QkhsvuXyOHcP1NwdgRYUK5qPTZ
/UuzmnIgzSqTzYcreo1Ix+d5QwIDAQABo4G1MIGyMAkGA1UdEwQCMAAwDgYDVR0P
AQH/BAQDAgP4MBMGA1UdJQQMMAoGCCsGAQUFBwMBMBgGA1UdEQQRMA+CBHdpbjKC
B2VkZ2VodWIwHQYDVR0OBBYEFCK9WxIS9bZzfKIEdJzTzcBZIYT0MEcGA1UdIwRA
MD6AFNe9bY4gt70tu8ywI26wxCKGPU0poSKkIDAeMRwwGgYDVQQDDBNUZXN0IEVk
Z2UgRGV2aWNlIENBggIYvjANBgkqhkiG9w0BAQsFAAOCAgEAE6rTJ7ZwsvZvOYAg
lVCAp5jfYEmI2nXxABbX7HmTo6MEGz+iavH/Doa+h2WT+VZdLFcYuy13a6Yz1TIF
rG1eWfK2ngAz2CSXaGLXtudQ2Nx0/TmonM96d4ZwjXulCOZntlaZGkhJBXpfaw1q
BhgaqEM/54DqpeRcQO8kZT/Ywm94iI9ksoMLO3Xz5O5yU7VUungx7P2RIy9YOIR7
mDeWnytTsCTlaQb3xQzRrEdEaquDCjJMrTwDtJnxq/Uh1gpbJWZSgj6sZ4MF4UpC
lVNFNx7DlwHKNg1n6rCNSMLCsOsxuN74/86Pxbzgh6/zdX8RTnKgap5BlnlUV7q3
HGKJxgwRNP/usK1zbhYekrx2KMvi1aWXmaSqQ+7MIBqdV3+wzPLa+vuXx+362GjX
NDnWnCTvzqFEV6fXV2jBaGwypFOzMD1303V6cMGFIkQAgD8OQOd1KKDzTOIh5Oqu
xRZPr5GwY+EuPSRf7v1YxriJNMDiXV10Osy/DgFv5ZZs71/maDDlU5WvgA5IT66i
x39DVxT7rZEkGSq0BX5EBD6OE708w07jEYnDeXCwed8vU7kcfaWMpqBYbVyg2bGK
L+rdjo44c//kyk2d6WUOhy7CVeku1ixAdNhuFTCGWN1U5dPc8TDPGisE+if/3z3l
JYCaOCyDAV86zUMpGJ7IL2gHFX0=
-----END CERTIFICATE-----
-----BEGIN CERTIFICATE-----
MIIFSDCCAzCgAwIBAgICGL4wDQYJKoZIhvcNAQELBQAwHjEcMBoGA1UEAwwTVGVz
dCBFZGdlIERldmljZSBDQTAeFw0yMTA3MjkwMDEzMzNaFw0yMTEwMjcwMDEzMzFa
MB8xHTAbBgNVBAMMFGlvdGVkZ2VkIHdvcmtsb2FkIGNhMIICIjANBgkqhkiG9w0B
AQEFAAOCAg8AMIICCgKCAgEArK3RAU6BzdxjwVCEPHFDdky+jLyGHRXcyZjnpI7f
nCW6QkgdjoYrl/wkwB6FoY0CPCXd3roqxyzQx9GL1OgDnQp2gI64fxAV1Fvz/1G1
2w7qMZyrbgFW0W1RlnBYPRsNyD0HqDUKk1EMc6X9nhJUtRUTgQPxOnh9/gjPP8cw
QNMicjthGKEuSpEpEUMs8KsvzR5tvxjNHQ8VT4RGz+GjkzS7nDPd9BR94TXnioqN
fWeuzdFz5ZtF5kjVo9TaKYWf8Y8HEAJmyTBERIlx9yQKaWqb6rpzHe6rZNzTOTZv
UjjSRjGtMWJGIm36iabPF0bRqi+mB5ImTAvxlLNbFL5cz/xLQOAQ4VKU6o/i5xd5
OUnNbVvKQlAo6yPFznE1GT99hyUy67+4qzAKSW+EQ/GRJCY1CLzT7eStBRZIhOUR
9ko2oOZa8CeDOaiVNV5Ev/ri/gh9n0s/USRBIdnBYf7k1OQom6fAeJv46LHuLTb+
e5Nm7NT4oayT7EcUtKVIavdiirmr7BwyuXrWOknj6CpcK8KEOsZdrdKVBC+rPRpw
IP5fUfTNHUaw8EiAWo5PqWSG1qOYNjdH2aWse3+bPce9I9KeBLRFan2BubF0Jxw7
DH/dhdX/XvQU+2puKZsuSsZDNJnl4jdnRFmX0fIO/s/qZxTOR9svbasFaDnXiYn9
W08CAwEAAaOBjjCBizASBgNVHRMBAf8ECDAGAQH/AgEAMA4GA1UdDwEB/wQEAwIC
hDAdBgNVHQ4EFgQU171tjiC3vS27zLAjbrDEIoY9TSkwRgYDVR0jBD8wPYAU+Ibf
cow4wRlk2KvCWgkCU+bA18WhIaQfMB0xGzAZBgNVBAMMElRlc3QgRWRnZSBPd25l
ciBDQYICSCMwDQYJKoZIhvcNAQELBQADggIBADuh37NJOOyB3HE/ldhk+qJb5WK4
bnADmrr2XrkZixBOxi9XMCHM0y4z9U6XSxM84fk2uCvfjJ3V7aECQ3kpEo/xOob6
jCKXjmlDtJfWHKOa7bLwLYMRu8RS5mDbExqfbjIXALA4AaikbBnE/G9H2V9f+xtH
70pzrCbyrNR/t0L/eibkh8+X/GtX3BOHz904m8pQRNHZIyJqLLU7AGN1ElUjUqC8
Sapp6WgHMjoA864rjWRLqJUZQLhLoflihgFnI98y8MT40WWg1r6OGkRUgiI5tweU
pT9bK+nuk9hhHr4OwNymp37qJ6Wf4TXqwix98c6YIFc+UyZJbf3HQdri5V2EOSiW
lChsDOn7i/SvgnklvYDLsu5zLkqw30MjXZLqSfWvC/ipeOsWkHrIsP4IZEvHqdiK
J+vGXBuYEo5bavLlEWrmvbaC3E8zL0HLT9YdZ1BYaSqrD7rzq4GwDSO3njx8CSVQ
sCuHLHbvMvejx0T0sfsShImCVAq0CiFzJ43IdxvW1UmrbtdFtnGOhxDnuvcqAH7L
L1PBGEhytk9zszTtS+tKSGAEopibTZwr202on6CMpPI56n88If5Z0Lu+JUreAkt/
OomS57mHLux5rilGZVigGnKHo9anmfDCGTYnPczI5JDLmwoW8zuNrFVawcNL7kAF
//ALPEGmaCxpo2X5
-----END CERTIFICATE-----
-----BEGIN CERTIFICATE-----
MIIFRTCCAy2gAwIBAgICSCMwDQYJKoZIhvcNAQELBQAwHTEbMBkGA1UEAwwSVGVz
dCBFZGdlIE93bmVyIENBMB4XDTIxMDcyOTAwMTMzMloXDTIxMTAyNzAwMTMzMVow
HjEcMBoGA1UEAwwTVGVzdCBFZGdlIERldmljZSBDQTCCAiIwDQYJKoZIhvcNAQEB
BQADggIPADCCAgoCggIBANtbYq0f757cn/TIX/kUO6p80Zgs2KSagvGfx6VkAvFJ
qMOVqgtWHyvjfp2m/xvv34npYQCTJGF6Yv/7StH2NMkin3FxIF3+Irr6PLiEIytk
VjvdvTj617YQ7oqG4RjUTh5Eo60ftKpRTFNQA6voe5kiJwSIDjItQ39CSN/WFkmB
stOGuPr1Fm55jLEOEHacTlb7Gt9uzKyEts1YU86NHdkSLHEpFOWR00b3bdUrfKAj
u38dTtoscx1sqPslj4ih+g17e/EY5zqymYF3HDwxS9v3d6D1DoKjfwjc+K9FEy7O
vEslqtXp0ruxQZlYKS+FzV434aZs87FFJMXyu/qKw08lzAGB7SoccCUxPtS0M9Fs
gCtoG1eTMEvAn1J/M1MValj0lBxMadO9sWafkhegtTTDVM26NeVnLVB5jjW0zARK
qf20iW4W7Fz7supUwt+bXy2vWMtUZ3qT1wH8qJaHyG3u/rsLXjmGHvVqYKbRPPkj
scCoLA4pSNEADoepQmEl8Amg2wMVMblvT7TqWVSL8H76riPUJcfv63wdDeeRa3Aw
8p1083DHnvdfXSbuoah9S7gQCbBSTo2dP6uPuas6Qw4kTpQD0sarMD2azzNfdvvx
VpzfvniHUDCxEVkdmZ9uh8f/HvCIkhh3oxfT99UoGjOfwRVgXNcvsTER5uw9PYYX
AgMBAAGjgY0wgYowEgYDVR0TAQH/BAgwBgEB/wIBAjAOBgNVHQ8BAf8EBAMCAoQw
HQYDVR0OBBYEFPiG33KMOMEZZNirwloJAlPmwNfFMEUGA1UdIwQ+MDyAFPdyQy3y
rb7qrPYTKmnjN+EZtgqXoSGkHzAdMRswGQYDVQQDDBJUZXN0IEVkZ2UgT3duZXIg
Q0GCASkwDQYJKoZIhvcNAQELBQADggIBAGmnk9PaeL+Mg0fRBk8RRI6hZz8gCFnK
nw2HCGqxy+V4Z2vumXkVsWGExVK5KwNqZZC/GYCfJjO3+cddlg/o/kh51wcXB0ud
cjguxZ0ER1i91hyNO7hK4flvvuZHZvZ5qUPZwA8bpl88bB11qSdxaW+Sfir9GN7X
Bvyl5WziUvBDM8O5cM0CNDNqeKH2io0z6cgTUdznUIKhcl0ZDG9PB4leKIpPO/it
/QG8ZVQGY7yOb1bL1oGGuz/mOT5C5iADMwhUihinrWWly2B6Myyhl0v2Vn//qFKG
/iuSsWXs0MPzkiytc+syRB5heQRArChgKq3dxL1ljpGi1eEZ7YQXyh6FZYnU1o4R
2UK/K4JulHynR2QC80hjt5tUYLtoCcdQkjsdKGSYEnWN90PoT4ePVHEqCPrD6c3q
XN29n30n9p7RdQIy7nyfEVeAPC3jNTCK9YXy9tk6lgfaG6J45Q5GbiOCU/fH2fSy
nRz5A3amHaiVWM0HFDAmr2/ZGHZG6D7QcujV/aGC12yIM5tIH2uAkzQPANgxmhU4
TnZ4zZcDaOOzRcCELgUGsjSqncIDQSt3ALXyT7wucpTXgjGfhZuAUZHCBvJsPZM1
ooZ5LG1GWkkPAXX1dnNfLx2gjPxI5ZdDhgUtyhMg/j4KU3ZxFO2ybdVsRq8+YveC
P4MrmHpwf4w6
-----END CERTIFICATE-----
-----BEGIN CERTIFICATE-----
MIIFQzCCAyugAwIBAgIBKTANBgkqhkiG9w0BAQsFADAdMRswGQYDVQQDDBJUZXN0
IEVkZ2UgT3duZXIgQ0EwHhcNMjEwNzI5MDAxMzMxWhcNMjExMDI3MDAxMzMxWjAd
MRswGQYDVQQDDBJUZXN0IEVkZ2UgT3duZXIgQ0EwggIiMA0GCSqGSIb3DQEBAQUA
A4ICDwAwggIKAoICAQDbS4Bn1h0tG3h9RdOxnHECn6bfyDAuKvjSO3YWxilTLzpJ
UbuGEX58h+Z5eR6QdOOcHpvecVejIh6whvXKWMm6AOFk9BawfWdFbZC0zPVNExIB
bWo2e6e/GdVT0mVIJa57gwvltYR+UZ+ypinGAxFSXODNnZU4QlmkMekpc1b1sAZW
eoCG0RvODvawiVi5Tey/9G9LbFUcAp0QCn58kKQNTRXlSGug1sPoEEzKnGiO0xu2
4a50rqCKhDF/mJJpq0W7fl1XwefoIf7SXJqeWyXsdqzja1iagLuVMbUMWxWt9iqb
rbX7vSkV6qzvP8AVUA9RY41xCDc2f2fZCNok5crixKsSm0C+Hb7WIHV0m7Yc/snr
sHUwJ37o3MdUsLhhz/0AxL/g1KGDZSohjD73HwQ1G8SzUQdeUAn8fmBhGrPSg07N
V4sUZ67CHYqZtZAS/9sM5ijjqqPWv5y+To6dYgInS0kXxC2bM5e1BhEPDWs568uL
/lZ9d1Ygu2KX1UaEHVRL+3M79iAdMgB9Khu2zeP/0D5h4H2iWf+YdqTuD5nW0Ku8
jXkTw51VQM5UYJzY/uC0BGfvEnV9Lxxr/yATAjGXx9j9/akyM1e30SqfO70KER02
+5FZtVqekaJJqJh1gGkYn5iLGdVQmYGtxK2d0l5oZvxaQK50/E6cNvtLb1hbNwID
AQABo4GNMIGKMBIGA1UdEwEB/wQIMAYBAf8CAQMwDgYDVR0PAQH/BAQDAgKEMB0G
A1UdDgQWBBT3ckMt8q2+6qz2Eypp4zfhGbYKlzBFBgNVHSMEPjA8gBT3ckMt8q2+
6qz2Eypp4zfhGbYKl6EhpB8wHTEbMBkGA1UEAwwSVGVzdCBFZGdlIE93bmVyIENB
ggEpMA0GCSqGSIb3DQEBCwUAA4ICAQAG05rF5elNVrKq6Nfd6IoY+0BT2JyKUMeb
szlVbx2QJq5iepnnuPDYLFdFw6F4O65cpOC5ubK2LkHNcf+5ItHhHDB4/CWmpxmJ
1+tGDBr3V9+xhNVT6Jxwv94DarGw7ZB443GZrRrzMa5YnxPuC1MhgGQz06iQxCFQ
1KkgGb7AOSXKK93JSrnf145VFAvaaND9RqGy0Fnn4w+jfHh8gS++Ve1now0/VSVS
lCFH3ezHOKTsmOKIs3HGRtGtacCkJxG1MY1jLkt//DTR2IZBJ3oVkCmK0CU6IqXv
5TOrdah31PKqRGfHAsjgVP5ToeyjfHNXT6UQ9Jx+F5CaA16Hb9Ur44ZTJU9WnFZo
1CLWzI5cXwVTeI72DwymM+4yxWRqSD1Unx1v6H7pvWMJUjIFRp2LqFbuqtPiq1PZ
ZDTFSOzpa4pCESs3NvwFIbbcUaMm65CaSGMCwO9LBW+H2ss9qP0rM2amFEgt3RRJ
gcNJj+wS5LsXh13GOofAaAl+21+pmLdMGcg56EIFqevmx8ty3msZVBes9pHSMk0j
jbUbp5ynSns1ewVueNhU2zOlvg5EKc7oNUjk+vLSfs1SuiPB8XT8YwpaL4zodVgv
P6ckqMrx1i/e+9B2VOJfDnIPTiW//LU+29ZWF2MH1oop0ofRc9tP0iNEVmOPa0hy
6tZNUkzuWg==
-----END CERTIFICATE-----";
            string privateKey = @"-----BEGIN PRIVATE KEY-----
MIIEvAIBADANBgkqhkiG9w0BAQEFAASCBKYwggSiAgEAAoIBAQCarufGd3AP2O3d
2diYvfxb2sBcqUe9yitcifyc8Nydr2bWu1zkEWrBf9y/dj8oLrZBtGXg0oOnLgwo
qGRs3RiGeUq9yFnmSHPViB98RC67rw+fF3QU01qBd9B8JMaegaR42cRcZcrYc7TJ
zb/zDYvAGd9qinxHyGrE9otU65CKI9Qc0dCezNY7dfGY5EBWgtswOFXjwN8D8Dme
2rf5ja8/ImXSdPNdit6UNM+guL2R/qRfUKrwn1NQ0CpU17M6AO+xjtKIafCR51VY
MgTyyHsUPVX2iAJ9l63bxCSGy+5fI4dw/U3B2BFhQrmo9Nn9S7OaciDNKpPNhyt6
jUjH53lDAgMBAAECggEACPlApPjk3WZ+VjJ/KE1NSJ7mLfn8GXyTC2lN6GTox5UX
aDmyZ+DCyrr6OXlIrLIZDLLEfkh4LsizF+C9ArvI4nRC68Olw4BMiAS/ntvtwiGj
zFz19QdV5tDmnW0cwLFQs1oe1CVroScFQ/fVvOn6Q4i8t1eVYiy0VPzglGqeFgVq
/3AmwN3jETAqm6bFkDlTOsaOoJL8an8HVOyDnG59PscDZ6uu3otEEX6nkwiNa8N2
QBEG2a/NRK4kgcwWKySEjWbpMaByXXIKMLtJgWabGqtCB80IoIZ+354Dsvdy+t4g
ouT5uxlvKvDf1F98wtWHpM6fj0pprDURZ/wry3gWoQKBgQDJzlBhT1rh0m3U+W06
/gxTn1UFh0QBA+yCMQOIkXdt7YeBAaI3ndVt55DpqchIcy86Ac1XCq+6tiKJrE38
l9X+IqbBy6b88ibH0UDisAazr3K8E5T9obb27VgdKhqCnzPa1JBQDi9+IYeSu6sW
Fzw7kQFvHLi8hnI+msSXu5JgqQKBgQDEOQQuvJ25whj1coc70XfHQZWEZp0ezysM
8fVQeIJp5xEeRaPVUfJnD9ciUOM6yelFOq7l0/s/bEIVjW4b9ESM99jxUvAzMZFx
u+adKhsHRjKmm8Ylek3VoKjxnWPbXdMokdAyntdrxD1SeYV7BdJu213Q8AJFy/J6
EV6W8nwCCwKBgGV1cZoK4IFKX2fE4zLWiPH92CwIXps4Es89vy4JHIdK9WZZoOnf
U2+HDac8cfJi3qqGP2t0dvcjHOgklazZ1X+IglhKgDEJuY+aV8ngf+4U1lVSbwS5
KhipKTS4d02WpuZtGgT1rND4IIYYDiL0GZdFBviK8yHtYkCxZQd4CVbJAoGALQ1G
N5DYydiVsG0OPZ63WIlnUdHZi0RXhw5am5I/px8FYCTvG8BH3n/VjixyL4JCS8HQ
fDYyfnpVpesl98caoh8ZsTawraBY23sf1L/hGsd0Q6qKUPqGumC7yVWwoqIlJDBu
U+ECZtzUk4YRLpDEou082gbxDqNn1bz9Mb0U2ccCgYATZi7OAZ5r86Jjy60/Be7T
QNZzYJPvukwXja82zbm0wnVUj/YXpX4slhAzih1bdUHAM0z4dv90WElhOYCtdkMK
zwVilwO6atudaMy+q8edHizXSt3ImIWY5uyv8LshG9yqrWQisYvvRNCLev1S8dD3
UqgFD5L2s1h1UsVT5ByzMg==
-----END PRIVATE KEY-----";
            //bool run = true;
            //while (run)
            //{
                CertificateHelper.ParseCertificateAndKey(certificate, privateKey);
                await Task.Delay(TimeSpan.FromMilliseconds(50));
            //}
            
            Console.WriteLine("SimulatedTemperatureSensor Main() finished.");
            return 0;
        }

        static bool SendUnlimitedMessages(int maximumNumberOfMessages) => maximumNumberOfMessages < 0;

        // Control Message expected to be:
        // {
        //     "command" : "reset"
        // }
        static Task<MessageResponse> ControlMessageHandle(Message message, object userContext)
        {
            byte[] messageBytes = message.GetBytes();
            string messageString = Encoding.UTF8.GetString(messageBytes);

            Console.WriteLine($"Received message Body: [{messageString}]");

            try
            {
                var messages = JsonConvert.DeserializeObject<ControlCommand[]>(messageString);

                foreach (ControlCommand messageBody in messages)
                {
                    if (messageBody.Command == ControlCommandEnum.Reset)
                    {
                        Console.WriteLine("Resetting temperature sensor..");
                        Reset.Set(true);
                    }
                }
            }
            catch (JsonSerializationException)
            {
                var messageBody = JsonConvert.DeserializeObject<ControlCommand>(messageString);

                if (messageBody.Command == ControlCommandEnum.Reset)
                {
                    Console.WriteLine("Resetting temperature sensor..");
                    Reset.Set(true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: Failed to deserialize control command with exception: [{ex}]");
            }

            return Task.FromResult(MessageResponse.Completed);
        }

        static Task<MethodResponse> ResetMethod(MethodRequest methodRequest, object userContext)
        {
            Console.WriteLine("Received direct method call to reset temperature sensor...");
            Reset.Set(true);
            var response = new MethodResponse((int)HttpStatusCode.OK);
            return Task.FromResult(response);
        }

        /// <summary>
        /// Module behavior:
        ///        Sends data periodically (with default frequency of 5 seconds).
        ///        Data trend:
        ///         - Machine Temperature regularly rises from 21C to 100C in regularly with jitter
        ///         - Machine Pressure correlates with Temperature 1 to 10psi
        ///         - Ambient temperature stable around 21C
        ///         - Humidity is stable with tiny jitter around 25%
        ///                Method for resetting the data stream.
        /// </summary>
        static async Task SendEvents(
            ModuleClient moduleClient,
            int messageCount,
            SimulatorParameters sim,
            CancellationTokenSource cts)
        {
            int count = 1;
            double currentTemp = sim.MachineTempMin;
            double normal = (sim.MachinePressureMax - sim.MachinePressureMin) / (sim.MachineTempMax - sim.MachineTempMin);

            while (!cts.Token.IsCancellationRequested && (SendUnlimitedMessages(messageCount) || messageCount >= count))
            {
                if (Reset)
                {
                    currentTemp = sim.MachineTempMin;
                    Reset.Set(false);
                }

                if (currentTemp > sim.MachineTempMax)
                {
                    currentTemp += Rnd.NextDouble() - 0.5; // add value between [-0.5..0.5]
                }
                else
                {
                    currentTemp += -0.25 + (Rnd.NextDouble() * 1.5); // add value between [-0.25..1.25] - average +0.5
                }

                if (sendData)
                {
                    var tempData = new MessageBody
                    {
                        Machine = new Machine
                        {
                            Temperature = currentTemp,
                            Pressure = sim.MachinePressureMin + ((currentTemp - sim.MachineTempMin) * normal),
                        },
                        Ambient = new Ambient
                        {
                            Temperature = sim.AmbientTemp + Rnd.NextDouble() - 0.5,
                            Humidity = Rnd.Next(24, 27)
                        },
                        TimeCreated = DateTime.UtcNow
                    };

                    string dataBuffer = JsonConvert.SerializeObject(tempData);
                    var eventMessage = new Message(Encoding.UTF8.GetBytes(dataBuffer));
                    eventMessage.Properties.Add("sequenceNumber", count.ToString());
                    eventMessage.Properties.Add("batchId", BatchId.ToString());
                    Console.WriteLine($"\t{DateTime.Now.ToLocalTime()}> Sending message: {count}, Body: [{dataBuffer}]");

                    await moduleClient.SendEventAsync("temperatureOutput", eventMessage);
                    count++;
                }

                await Task.Delay(messageDelay, cts.Token);
            }

            if (messageCount < count)
            {
                Console.WriteLine($"Done sending {messageCount} messages");
            }
        }

        static async Task OnDesiredPropertiesUpdated(TwinCollection desiredPropertiesPatch, object userContext)
        {
            // At this point just update the configure configuration.
            if (desiredPropertiesPatch.Contains(SendIntervalConfigKey))
            {
                messageDelay = TimeSpan.FromSeconds((int)desiredPropertiesPatch[SendIntervalConfigKey]);
            }

            if (desiredPropertiesPatch.Contains(SendDataConfigKey))
            {
                bool desiredSendDataValue = (bool)desiredPropertiesPatch[SendDataConfigKey];
                if (desiredSendDataValue != sendData && !desiredSendDataValue)
                {
                    Console.WriteLine("Sending data disabled. Change twin configuration to start sending again.");
                }

                sendData = desiredSendDataValue;
            }

            var moduleClient = (ModuleClient)userContext;
            var patch = new TwinCollection($"{{ \"SendData\":{sendData.ToString().ToLower()}, \"SendInterval\": {messageDelay.TotalSeconds}}}");
            await moduleClient.UpdateReportedPropertiesAsync(patch); // Just report back last desired property.
        }

        static async Task<ModuleClient> CreateModuleClientAsync(
            TransportType transportType,
            ITransientErrorDetectionStrategy transientErrorDetectionStrategy = null,
            RetryStrategy retryStrategy = null)
        {
            var retryPolicy = new RetryPolicy(transientErrorDetectionStrategy, retryStrategy);
            retryPolicy.Retrying += (_, args) => { Console.WriteLine($"[Error] Retry {args.CurrentRetryCount} times to create module client and failed with exception:{Environment.NewLine}{args.LastException}"); };

            ModuleClient client = await retryPolicy.ExecuteAsync(
                async () =>
                {
                    ITransportSettings[] GetTransportSettings()
                    {
                        switch (transportType)
                        {
                            case TransportType.Mqtt:
                            case TransportType.Mqtt_Tcp_Only:
                                return new ITransportSettings[] { new MqttTransportSettings(TransportType.Mqtt_Tcp_Only) };
                            case TransportType.Mqtt_WebSocket_Only:
                                return new ITransportSettings[] { new MqttTransportSettings(TransportType.Mqtt_WebSocket_Only) };
                            case TransportType.Amqp_WebSocket_Only:
                                return new ITransportSettings[] { new AmqpTransportSettings(TransportType.Amqp_WebSocket_Only) };
                            default:
                                return new ITransportSettings[] { new AmqpTransportSettings(TransportType.Amqp_Tcp_Only) };
                        }
                    }

                    ITransportSettings[] settings = GetTransportSettings();
                    Console.WriteLine($"[Information]: Trying to initialize module client using transport type [{transportType}].");
                    ModuleClient moduleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
                    await moduleClient.OpenAsync();

                    Console.WriteLine($"[Information]: Successfully initialized module client of transport type [{transportType}].");
                    return moduleClient;
                });

            return client;
        }

        class ControlCommand
        {
            [JsonProperty("command")]
            public ControlCommandEnum Command { get; set; }
        }

        class SimulatorParameters
        {
            public double MachineTempMin { get; set; }

            public double MachineTempMax { get; set; }

            public double MachinePressureMin { get; set; }

            public double MachinePressureMax { get; set; }

            public double AmbientTemp { get; set; }

            public int HumidityPercent { get; set; }
        }
    }
}
