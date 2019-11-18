namespace MetricsCollector
{
    using System.Threading.Tasks;

    interface IMetricsSync
    {
        Task ScrapeAndSync();
    }
}
