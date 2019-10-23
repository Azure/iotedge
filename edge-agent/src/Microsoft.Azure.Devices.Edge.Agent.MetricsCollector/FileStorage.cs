using Microsoft.Azure.Devices.Edge.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Microsoft.Azure.Devices.Edge.Agent.MetricsCollector
{
    public interface IFileStorage
    {
        void AddScrapeResult(string data);
        IDictionary<DateTime, Func<string>> GetData();
        IDictionary<DateTime, Func<string>> GetData(DateTime start);
        IDictionary<DateTime, Func<string>> GetData(DateTime start, DateTime end);
        void RemoveOldEntries(DateTime keepAfter);
    }

    public class FileStorage : IFileStorage
    {
        private readonly ISystemTime systemTime;

        private string directory;

        public FileStorage(string directory, ISystemTime systemTime = null)
        {
            this.directory = directory;
            this.systemTime = systemTime ?? SystemTime.Instance;
        }

        public void AddScrapeResult(string data)
        {
            Directory.CreateDirectory(directory);
            string file = Path.Combine(directory, systemTime.UtcNow.Ticks.ToString());
            File.WriteAllText(file, data);
        }

        public IDictionary<DateTime, Func<string>> GetData()
        {
            return GetData(_ => true);
        }
        public IDictionary<DateTime, Func<string>> GetData(DateTime start)
        {
            return GetData(ticks => start.Ticks <= ticks);
        }
        public IDictionary<DateTime, Func<string>> GetData(DateTime start, DateTime end)
        {
            return GetData(ticks => start.Ticks <= ticks && ticks <= end.Ticks);
        }
        private IDictionary<DateTime, Func<string>> GetData(Func<long, bool> inTimeRange)
        {
            return Directory.GetFiles(directory)
                .Select(Path.GetFileName)
                .SelectWhere(fileName => (long.TryParse(fileName, out long timestamp), timestamp))
                .Where(inTimeRange)
                .ToDictionary(
                    ticks => new DateTime(ticks),
                    ticks => (Func<string>)(() =>
                    {
                        string file = Path.Combine(directory, ticks.ToString());
                        if (File.Exists(file))
                        {
                            return File.ReadAllText(file);
                        }
                        return "";
                    })
                );
        }

        public void RemoveOldEntries(DateTime keepAfter)
        {
            GetData()
            .Select(d => d.Key)
            .Where(timestamp => timestamp < keepAfter)
            .Select(timestamp => Path.Combine(directory, timestamp.Ticks.ToString()))
            .ToList()
            .ForEach(File.Delete);
        }
    }
    public static class SelectWhereClass
    {
        public static IEnumerable<TResult> SelectWhere<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, (bool, TResult)> selector)
        {
            foreach (TSource s in source)
            {
                (bool include, TResult result) = selector(s);
                if (include)
                {
                    yield return result;
                }
            }
        }
    }
}
