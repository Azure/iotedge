//// Copyright (c) Microsoft. All rights reserved.
//namespace Microsoft.Azure.Devices.Edge.Util.metrics.Opencensus
//{
//    using System;
//    using System.Collections.Generic;
//    using OpenCensus.Exporter.Prometheus;
//    using OpenCensus.Stats;

//    public class OpenCensusMetrics : IDisposable
//    {
//        readonly PrometheusExporter prometheusExporter;

//        public OpenCensusMetrics()
//        {
//            this.prometheusExporter = new PrometheusExporter(
//                new PrometheusExporterOptions()
//                {
//                    Url = new Uri("http://localhost:9184/metrics/")
//                },
//                Stats.ViewManager);

//            this.prometheusExporter.Start();
//        }

//        public void Dispose()
//        {
//            this.prometheusExporter?.Stop();
//        }

//        class MetricsProvider : IMetricsProvider
//        {
//            public ICounter GetCounter(string name)
//            {

//            }
//        }

//        class Counter : Util.Metrics.ICounter
//        {
//            readonly IStatsRecorder statsRecorder;

//            public Counter(IStatsRecorder statsRecorder)
//            {
//                this.statsRecorder = statsRecorder;
//            }

//            public void Increment(long amount) => throw new NotImplementedException();

//            public void Decrement(long amount) => throw new NotImplementedException();

//            public void Increment(long amount, IDictionary<string, string> tags)
//            {
//                //this.statsRecorde
//            }

//            public void Decrement(long amount, IDictionary<string, string> tags) => throw new NotImplementedException();
//        }
//    }
//}
