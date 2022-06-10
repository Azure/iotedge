// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Microsoft.Azure.Devices.Edge.Azure.Monitor.Test
{
    public class MetricFilterTest
    {
        private readonly static DateTime testTime = DateTime.UnixEpoch;

        private readonly static Dictionary<string, string> someTags = new Dictionary<string, string>() {["label"] = "value", ["label2"] = "", ["__a"] = "度量"};

        private readonly IEnumerable<Metric> goodTestMetrics = new List<Metric>() {
            new Metric(testTime, "metricname", 5, new Dictionary<string, string>()),
            new Metric(testTime, "metricName", 0, new Dictionary<string, string>() {{"label", "value"}, {"label2", ""}}),
            new Metric(testTime, "foobar", 5, new Dictionary<string, string>() {{"quartile", "0.5"}}),
            new Metric(testTime, "a9", 5, new Dictionary<string, string>() {{"a", "a"}, {"b", "a"}, {"c", "a"}, {"d", "a"}, {"e", "a"}, {"f", "a"}, {"g", "a"}, {"h", "a"}, {"i", "a"}, {"j", "a"}, {"k", "a"}, {"l", "a"}}),
            new Metric(testTime, ":_", 5, new Dictionary<string, string>() { ["__a"] = "度量"})
        };

        private readonly IEnumerable<Metric> invalidTestMetrics = new List<Metric>() {
            new Metric(testTime, "", 5, new Dictionary<string, string>()),
            new Metric(testTime, "9asdf", 0, new Dictionary<string, string>() {{"label", "value"}, {"label2", ""}}),
            new Metric(testTime, "validname", 5, new Dictionary<string, string>() { ["9[]"] = "asdf"})
        };

        [Fact]
        public void TestDoesntThrowExceptions()
        {
            MetricFilter filter = new MetricFilter("");
            foreach (Metric metric in goodTestMetrics) {
                filter.Matches(metric);
            }
        }

        [Fact]
        public void TestEmpty()
        {
            MetricFilter filter = new MetricFilter("");
            foreach (Metric metric in goodTestMetrics) {
                Assert.False(filter.Matches(metric));
            }
        }

        [Fact]
        public void TestMetricNameOnly()
        {
            MetricFilter filter = new MetricFilter("metricName");
            Assert.False(filter.Matches(new Metric(testTime, "metricname", 5, new Dictionary<string, string>() {})));
            Assert.True(filter.Matches(new Metric(testTime, "metricName", 5, new Dictionary<string, string>())));
            Assert.False(filter.Matches(new Metric(testTime, "foobar", 0, new Dictionary<string, string>())));
            Assert.False(filter.Matches(new Metric(testTime, "", 5, new Dictionary<string, string>())));
        }

        [Fact]
        public void TestMetricNameWildcardStar()
        {
            {
                MetricFilter filter = new MetricFilter("metric*");
                Assert.True(filter.Matches(new Metric(testTime, "metricname", 5, new Dictionary<string, string>())));
                Assert.True(filter.Matches(new Metric(testTime, "metricName", 5, new Dictionary<string, string>())));
                Assert.False(filter.Matches(new Metric(testTime, "foobar", 0, new Dictionary<string, string>())));
                Assert.False(filter.Matches(new Metric(testTime, "", 5, new Dictionary<string, string>())));
            }
            {
                MetricFilter filter = new MetricFilter("*foobar{}");
                Assert.True(filter.Matches(new Metric(testTime, "foobar", 5, new Dictionary<string, string>())));
                Assert.True(filter.Matches(new Metric(testTime, "ffoobar", 5, new Dictionary<string, string>())));
                Assert.True(filter.Matches(new Metric(testTime, "_:foobar", 5, new Dictionary<string, string>())));

                Assert.False(filter.Matches(new Metric(testTime, "foob", 5, new Dictionary<string, string>())));
                Assert.False(filter.Matches(new Metric(testTime, "", 5, new Dictionary<string, string>())));
            }
            foreach (MetricFilter filter in new List<MetricFilter>() {new MetricFilter("*"), new MetricFilter("*{}")})
            {
                Assert.True(filter.Matches(new Metric(testTime, "metricName", 5, someTags)));
                Assert.True(filter.Matches(new Metric(testTime, "foobar", 0, new Dictionary<string, string>())));
                Assert.True(filter.Matches(new Metric(testTime, "", 5, new Dictionary<string, string>())));
                Assert.True(filter.Matches(new Metric(testTime, "__:__", 5, new Dictionary<string, string>())));
            }
        }

        [Fact]
        public void TestMetricNameWildcardQuestionmark()
        {
            foreach (MetricFilter filter in new List<MetricFilter>() {new MetricFilter("?"), new MetricFilter("?{}")})
            {
                Assert.True(filter.Matches(new Metric(testTime, "a", 5, new Dictionary<string, string>())));
                Assert.True(filter.Matches(new Metric(testTime, "b", 0, new Dictionary<string, string>())));
                Assert.True(filter.Matches(new Metric(testTime, "_", 5, new Dictionary<string, string>())));
                Assert.True(filter.Matches(new Metric(testTime, ":", 5, new Dictionary<string, string>())));
                Assert.False(filter.Matches(new Metric(testTime, "aa", 5, new Dictionary<string, string>())));
                Assert.False(filter.Matches(new Metric(testTime, "::", 5, new Dictionary<string, string>())));
            }

            foreach (MetricFilter filter in new List<MetricFilter>() {new MetricFilter("?asdf??"), new MetricFilter("?asdf??{}")})
            {
                Assert.True(filter.Matches(new Metric(testTime, "aasdfff", 5, new Dictionary<string, string>())));
                Assert.True(filter.Matches(new Metric(testTime, ":asdf__", 0, new Dictionary<string, string>())));
                Assert.False(filter.Matches(new Metric(testTime, "asdf", 5, new Dictionary<string, string>())));
                Assert.False(filter.Matches(new Metric(testTime, "aasdff", 5, new Dictionary<string, string>())));
                Assert.False(filter.Matches(new Metric(testTime, "", 5, new Dictionary<string, string>())));
            }
        }

        [Fact]
        public void TestMultiMetricNameEmptyBraces()
        {
            MetricFilter filter = new MetricFilter("metricName{} foobar foobar foobar2");
            Assert.True(filter.Matches(new Metric(testTime, "metricName", 5, new Dictionary<string, string>())));
            Assert.True(filter.Matches(new Metric(testTime, "foobar", 5, new Dictionary<string, string>())));
            Assert.True(filter.Matches(new Metric(testTime, "foobar2", 5, new Dictionary<string, string>())));
            Assert.False(filter.Matches(new Metric(testTime, "foobar3", 0, new Dictionary<string, string>())));
            Assert.False(filter.Matches(new Metric(testTime, "", 5, new Dictionary<string, string>())));

            filter = new MetricFilter("metricName{} foobar? foobar foobar4 ?foobar*");
            Assert.True(filter.Matches(new Metric(testTime, "metricName", 5, new Dictionary<string, string>())));
            Assert.True(filter.Matches(new Metric(testTime, "foobar", 5, someTags)));
            Assert.True(filter.Matches(new Metric(testTime, "foobar2", 5, new Dictionary<string, string>())));
            Assert.True(filter.Matches(new Metric(testTime, "ffoobar3", 0, new Dictionary<string, string>())));
            Assert.True(filter.Matches(new Metric(testTime, "foobar4", 0, new Dictionary<string, string>())));
            Assert.True(filter.Matches(new Metric(testTime, "_foobar_asdf:asdf", 0, new Dictionary<string, string>())));
            Assert.False(filter.Matches(new Metric(testTime, "foobarasdfasdf", 0, new Dictionary<string, string>())));
            Assert.False(filter.Matches(new Metric(testTime, "", 5, new Dictionary<string, string>())));
        }

        [Fact]
        public void TestCommaSeparatedMetricSelectors()
        {
            MetricFilter filter = new MetricFilter("metricName{}, foobar[foobar], foobar, foobar2");
            Assert.True(filter.Matches(new Metric(testTime, "metricName", 5, new Dictionary<string, string>())));
            Assert.True(filter.Matches(new Metric(testTime, "foobar", 5, new Dictionary<string, string>())));
            Assert.True(filter.Matches(new Metric(testTime, "foobar2", 5, new Dictionary<string, string>())));
            Assert.False(filter.Matches(new Metric(testTime, "foobar3", 0, new Dictionary<string, string>())));
            Assert.False(filter.Matches(new Metric(testTime, "", 5, new Dictionary<string, string>())));

            filter = new MetricFilter("metricName{}, foobar?, foobar  ,  foobar4 \t\t ?foobar*");
            Assert.True(filter.Matches(new Metric(testTime, "metricName", 5, new Dictionary<string, string>())));
            Assert.True(filter.Matches(new Metric(testTime, "foobar", 5, someTags)));
            Assert.True(filter.Matches(new Metric(testTime, "foobar2", 5, new Dictionary<string, string>())));
            Assert.True(filter.Matches(new Metric(testTime, "ffoobar3", 0, new Dictionary<string, string>())));
            Assert.True(filter.Matches(new Metric(testTime, "foobar4", 0, new Dictionary<string, string>())));
            Assert.True(filter.Matches(new Metric(testTime, "_foobar_asdf:asdf", 0, new Dictionary<string, string>())));
            Assert.False(filter.Matches(new Metric(testTime, "foobarasdfasdf", 0, new Dictionary<string, string>())));
            Assert.False(filter.Matches(new Metric(testTime, "", 5, new Dictionary<string, string>())));
        }

        [Fact]
        public void TestLabelEquality()
        {
            MetricFilter filter = new MetricFilter("foobar{label=\"asdf\"}");
            Assert.True(filter.Matches(new Metric(testTime, "foobar", 5, new Dictionary<string, string>() {["label"] = "asdf"})));
            Assert.False(filter.Matches(new Metric(testTime, "foobar", 5, new Dictionary<string, string>() {["label"] = "Asdf"})));
            Assert.False(filter.Matches(new Metric(testTime, "foobar", 5, new Dictionary<string, string>() {["Label"] = "asdf"})));
            Assert.False(filter.Matches(new Metric(testTime, "foobar", 0, new Dictionary<string, string>())));
            Assert.False(filter.Matches(new Metric(testTime, "", 5, new Dictionary<string, string>())));

            filter = new MetricFilter("foobar{label!=\"asdf\"}");
            Assert.False(filter.Matches(new Metric(testTime, "foobar", 5, new Dictionary<string, string>() {["label"] = "asdf"})));
            Assert.True(filter.Matches(new Metric(testTime, "foobar", 5, new Dictionary<string, string>() {["label"] = "Asdf"})));
            Assert.False(filter.Matches(new Metric(testTime, "foobar", 5, new Dictionary<string, string>() {["Label"] = "asdf"})));
            Assert.False(filter.Matches(new Metric(testTime, "foobar", 0, new Dictionary<string, string>())));
            Assert.False(filter.Matches(new Metric(testTime, "", 5, new Dictionary<string, string>())));

            filter = new MetricFilter("foobar{label=\"asdf\",__asdf__=\"_5\"}");
            Assert.True(filter.Matches(new Metric(testTime, "foobar", 5, new Dictionary<string, string>() {["label"] = "asdf", ["__asdf__"] = "_5"})));
            Assert.True(filter.Matches(new Metric(testTime, "foobar", 5, new Dictionary<string, string>() {["label"] = "asdf", ["__asdf__"] = "_5", ["extraLabel"] = "true"})));
            Assert.False(filter.Matches(new Metric(testTime, "foobar", 5, new Dictionary<string, string>() {["label"] = "asdf", ["extraLabel"] = "true"})));
            Assert.False(filter.Matches(new Metric(testTime, "metricname", 5, new Dictionary<string, string>() {["label"] = "Asdf", ["__asdf__"] = "_5"})));
            Assert.False(filter.Matches(new Metric(testTime, "metricname", 5, new Dictionary<string, string>() {["label"] = "asdf", ["__Asdf__"] = "_5"})));
            Assert.False(filter.Matches(new Metric(testTime, "foobar", 5, new Dictionary<string, string>() {["label"] = "Asdf"})));
            Assert.False(filter.Matches(new Metric(testTime, "foobar", 0, new Dictionary<string, string>())));
            Assert.False(filter.Matches(new Metric(testTime, "", 5, new Dictionary<string, string>())));
        }

        [Fact]
        public void TestLabelRegex()
        {
            MetricFilter filter = new MetricFilter("foobar{label=~\"asdf\"}");
            Assert.True(filter.Matches(new Metric(testTime, "foobar", 5, new Dictionary<string, string>() {["label"] = "asdf"})));
            Assert.False(filter.Matches(new Metric(testTime, "foobar", 5, new Dictionary<string, string>() {["label"] = "Asdf"})));
            Assert.False(filter.Matches(new Metric(testTime, "foobar", 5, new Dictionary<string, string>() {["Label"] = "asdf"})));
            Assert.False(filter.Matches(new Metric(testTime, "foobar", 0, new Dictionary<string, string>())));
            Assert.False(filter.Matches(new Metric(testTime, "", 5, new Dictionary<string, string>())));

            filter = new MetricFilter("foobar{label=~\"asdf|foo\"}");
            Assert.True(filter.Matches(new Metric(testTime, "foobar", 5, new Dictionary<string, string>() {["label"] = "asdf"})));
            Assert.True(filter.Matches(new Metric(testTime, "foobar", 5, new Dictionary<string, string>() {["label"] = "foo"})));
            Assert.False(filter.Matches(new Metric(testTime, "foobar", 5, new Dictionary<string, string>() {["label"] = "Asdf"})));
            Assert.False(filter.Matches(new Metric(testTime, "foobar", 5, new Dictionary<string, string>() {["Label"] = "asdffoo"})));
            Assert.False(filter.Matches(new Metric(testTime, "foobar", 0, new Dictionary<string, string>())));
            Assert.False(filter.Matches(new Metric(testTime, "", 5, new Dictionary<string, string>())));

            filter = new MetricFilter("foobar{label!~\"[a-zA-Z_][a-zA-Z0-9_]*\"}");
            Assert.False(filter.Matches(new Metric(testTime, "foobar", 5, new Dictionary<string, string>() {["label"] = "asdf"})));
            Assert.False(filter.Matches(new Metric(testTime, "foobar", 5, new Dictionary<string, string>() {["label"] = "___asdf0985FOO"})));
            Assert.False(filter.Matches(new Metric(testTime, "foobar", 5, new Dictionary<string, string>() {["label"] = "_"})));
            Assert.True(filter.Matches(new Metric(testTime, "foobar", 5, new Dictionary<string, string>() {["label"] = ""})));
            Assert.True(filter.Matches(new Metric(testTime, "foobar", 5, new Dictionary<string, string>() {["label"] = "9notvalid"})));
            Assert.False(filter.Matches(new Metric(testTime, "foobar", 5, someTags)));  // (contains non-askii characters)
            Assert.False(filter.Matches(new Metric(testTime, "foobar", 5, new Dictionary<string, string>() {["Label"] = "a"})));
            Assert.False(filter.Matches(new Metric(testTime, "foobar", 0, new Dictionary<string, string>())));
            Assert.False(filter.Matches(new Metric(testTime, "", 5, new Dictionary<string, string>())));

            filter = new MetricFilter("foobar{label=~\"asdf|[a-d]+\",__asdf__!~\"_|__\"}");
            Assert.True(filter.Matches(new Metric(testTime, "foobar", 5, new Dictionary<string, string>() {["label"] = "asdf", ["__asdf__"] = "___"})));
            Assert.True(filter.Matches(new Metric(testTime, "foobar", 5, new Dictionary<string, string>() {["label"] = "abcd", ["__asdf__"] = "asdf", ["extraLabel"] = "true"})));
            Assert.True(filter.Matches(new Metric(testTime, "foobar", 5, new Dictionary<string, string>() {["label"] = "a", ["__asdf__"] = "asdf"})));
            Assert.True(filter.Matches(new Metric(testTime, "foobar", 5, new Dictionary<string, string>() {["label"] = "asdf", ["__asdf__"] = ""})));
            Assert.False(filter.Matches(new Metric(testTime, "foobar", 5, new Dictionary<string, string>() {["label"] = "a"})));
            Assert.False(filter.Matches(new Metric(testTime, "foobar", 5, new Dictionary<string, string>() {["label"] = "asdf", ["__asdf__"] = "_"})));
            Assert.False(filter.Matches(new Metric(testTime, "foobar", 0, someTags)));
            Assert.False(filter.Matches(new Metric(testTime, "metricname", 5, new Dictionary<string, string>() {["label"] = "asdf", ["__asdf__"] = "___"})));
            Assert.False(filter.Matches(new Metric(testTime, "", 5, new Dictionary<string, string>())));
        }

        [Fact]
        public void TestEndpoint()
        {
            MetricFilter filter = new MetricFilter("metricName[http://VeryNoisyModule:9001/metrics]");
            Assert.True(filter.Matches(new Metric(testTime, "metricName", 5, someTags, "http://VeryNoisyModule:9001/metrics")));
            Assert.False(filter.Matches(new Metric(testTime, "metricname", 5, someTags, "http://VeryNoisyModule:9001/metrics")));
            Assert.False(filter.Matches(new Metric(testTime, "metricname", 5, new Dictionary<string, string>(), "http://veryNoisyModule:9001/metrics")));
            Assert.False(filter.Matches(new Metric(testTime, "metricname", 5, new Dictionary<string, string>(), "https://VeryNoisyModule:9001/metrics")));
            Assert.False(filter.Matches(new Metric(testTime, "metricName", 5, new Dictionary<string, string>())));
            Assert.False(filter.Matches(new Metric(testTime, "foobar", 0, new Dictionary<string, string>())));
            Assert.False(filter.Matches(new Metric(testTime, "", 5, new Dictionary<string, string>())));

            filter = new MetricFilter("metricname");
            Assert.True(filter.Matches(new Metric(testTime, "metricname", 5, someTags, "http://VeryNoisyModule:9001/metrics")));
            Assert.True(filter.Matches(new Metric(testTime, "metricname", 5, new Dictionary<string, string>(), "http://veryNoisyModule:9001/metrics")));
            Assert.True(filter.Matches(new Metric(testTime, "metricname", 5, new Dictionary<string, string>(), "https://VeryNoisyModule:9001/metrics")));
            Assert.True(filter.Matches(new Metric(testTime, "metricname", 5, new Dictionary<string, string>())));
        }

        [Fact]
        public void TestAllowedMetricFilterWithExample()
        {
            // setup

            MetricFilter filter = new MetricFilter("edgehub_gettwin_total{edge_device=\"Ubuntu-20\"}[http://VeryNoisyModule:9001/metrics]");

            Metric metric1 = new Metric(DateTime.UnixEpoch, "edgehub_gettwin_total", 5, new Dictionary<string, string>());
            Metric metric2 = new Metric(DateTime.UnixEpoch, "edgehub_gettwin_total", 5, someTags, "http://VeryNoisyModule:9001/metrics");

            Dictionary<string, string> tag = new Dictionary<string, string>() { { "edge_device", "Ubuntu-20" } };
            Metric metric3 = new Metric(DateTime.UnixEpoch, "edgehub_gettwin_total", 5, tag, "http://VeryNoisyModule:9001/metrics");
            Metric metric4 = new Metric(DateTime.UnixEpoch, "edgehub_gettwin_total", 5, tag, "http://Google:9001/metrics");

            IEnumerable<Metric> metrics = new List<Metric>() { metric1, metric2, metric3, metric4 };

            // test that string of metrics is filtered correctly.
            metrics = metrics.Where(x => filter.Matches(x));

            // assert
            Assert.True(metrics.Count() == 1);
            Assert.Equal("http://VeryNoisyModule:9001/metrics", metrics.ElementAt(0).Endpoint);
            Assert.Equal("edgehub_gettwin_total", metrics.ElementAt(0).Name);

            // change metric filter to the other endpoint
            filter = new MetricFilter("edgehub_gettwin_total{edge_device=\"Ubuntu-20\"}[http://Google:9001/metrics]");

            // run test again
            metrics = metrics.Where(x => filter.Matches(x));

            Assert.True(metrics.Count() == 1);
            Assert.Equal("http://Google:9001/metrics", metrics.ElementAt(0).Endpoint);
            Assert.Equal("edgehub_gettwin_total", metrics.ElementAt(0).Name);
        }

        [Fact]
        public void TestParsingRules()
        {
            // test name parsing
            MetricFilter filter = new MetricFilter("metricName");
            filter = new MetricFilter("foobar_:909");
            filter = new MetricFilter("_");
            filter = new MetricFilter(":");

            Assert.Throws<ArgumentException>(() => new MetricFilter("1metricName"));
            Assert.Throws<ArgumentException>(() => new MetricFilter("metric你"));
            Assert.Throws<ArgumentException>(() => new MetricFilter("metric;Name"));
            Assert.Throws<ArgumentException>(() => new MetricFilter("metric;Name{}"));

            // test endpoint parsing
            filter = new MetricFilter("metricName{} [http://VeryNoisyModule:9001/metrics]");
            Assert.Throws<ArgumentException>(() => new MetricFilter("metricName {}{http://VeryNoisyModule:9001/metrics}"));
            Assert.Throws<ArgumentException>(() => new MetricFilter("metricName {}http://VeryNoisyModule:9001/metrics"));
            Assert.Throws<ArgumentException>(() => new MetricFilter("metricName {}[http://VeryNoisyModule:9001/metrics"));
            Assert.Throws<ArgumentException>(() => new MetricFilter("metricName {}http://VeryNoisyModule:9001/metrics]"));
            filter = new MetricFilter("metricName {} [http://VeryNoisyModule:9001/metrics]");
            filter = new MetricFilter("metricName [http://VeryNoisyModule:9001/metrics]");

            filter = new MetricFilter("metricName {} [http://asdfasdfasdfasdfasdf]");
            filter = new MetricFilter("metricName {}[nou]");
            filter = new MetricFilter("metricName{} [.asdf.]");
            filter = new MetricFilter("metricName {} [.asdf,]");
            filter = new MetricFilter("metricName {} [^34$#@!]");

            Assert.Throws<ArgumentException>(() => new MetricFilter("metricName{{}} [^34$#@!]"));
            Assert.Throws<ArgumentException>(() => new MetricFilter("metricName{{} [^34$#@!]"));

            // test variable spacing
            filter = new MetricFilter("metricName    {}     [^34$#@!]");
            filter = new MetricFilter("metricName\t{} \t [ ^34$#@!]");
            filter = new MetricFilter("metricName [  ^34$#@!  ]");
            filter = new MetricFilter("metricName\t[^34$#@!\t\t]");
            Assert.Throws<ArgumentException>(() => new MetricFilter("metricName [^34$#@ !]"));

            // test label parsing
            filter = new MetricFilter("metricName{asdf=\"asdf\"}   [^34$#@!]");
            filter = new MetricFilter("metricName{asdf=\"asdf\",foobar=\"asdf\"} [^34$#@!]");
            Assert.Throws<ArgumentException>(() => new MetricFilter("metricName{a=\"asdf\",a=\"asdf\"} [^34$#@!]"));
            filter = new MetricFilter("metricName{    a =\t\"asdf\"  , B = \t \"asdf\"} [^34$#@!]");
            filter = new MetricFilter("metricName{a=\"asdf\"\t,\tn=\"𝓉𝑒𝓈𝓉 𝓉𝑒𝓍𝓉 𝕔𝓪ⓝ 𝕙𝕒𝕧𝕖 𝕙𝕒𝕧𝕖 🅲🅷🅰🆁🅰🅲🆃🅴🆁🆂å̴̛̲̙̖͛̽͆̈́͆̐͝ḉ̶̜͎̟͓͙̻͈̉̈́ṱ̴̣̳̳̬̯̦̾̊͐̏̿͌̕͝ͅę̵̛̛̥̼̘̖͚̜̄́͂̌͌̕͜͠ͅr̵͒̉͂̓ͅs̷̘̦̖͕̘̙̠͑̌̊̍̈́  \t 🐯 مرحبا Բարեւ Ձեզ добры дзень হ্যালো Здравейте Përshëndetje 你好 העלא ନମସ୍କାର 여보세요\"} [^34$#@!]");

            Assert.Throws<ArgumentException>(() => new MetricFilter("metricName {"));
            Assert.Throws<ArgumentException>(() => new MetricFilter("metricName{6=\"hi\"}"));
            Assert.Throws<ArgumentException>(() => new MetricFilter("metricName{a==\"hi\"}"));
            Assert.Throws<ArgumentException>(() => new MetricFilter("metricName{a==\"hi\". foo=\"bar\"}"));
            Assert.Throws<ArgumentException>(() => new MetricFilter("metricName{+=\"hi\". foo=\"bar\"}"));
            Assert.Throws<ArgumentException>(() => new MetricFilter("metricName{a=\"hi\". foo=!\"bar\"}"));
            Assert.Throws<ArgumentException>(() => new MetricFilter("metricName{a=\"hi\". foo~!\"bar\"}"));
            Assert.Throws<ArgumentException>(() => new MetricFilter("metricName{a=\"hi\". foo~!\"bar\"}"));
            Assert.Throws<ArgumentException>(() => new MetricFilter("metricName{a=\"\"hi\"}"));
            Assert.Throws<ArgumentException>(() => new MetricFilter("metricName{a=\"hi\"\"}"));
            Assert.Throws<ArgumentException>(() => new MetricFilter("metricName{a=\"\"hi\"\"}"));
            Assert.Throws<ArgumentException>(() => new MetricFilter("metricName{\"a\"=\"\"hi\"\"}"));
            Assert.Throws<ArgumentException>(() => new MetricFilter("metricName{a='hi'}"));
            Assert.Throws<ArgumentException>(() => new MetricFilter("metricName{'a=hi'}"));
            Assert.Throws<ArgumentException>(() => new MetricFilter("metricName{a=hi}"));
            Assert.Throws<ArgumentException>(() => new MetricFilter("metricName{    :_ =\t\"asdf\"  , _ := \t \"asdf\"} [^34$#@!]"));
            Assert.Throws<ArgumentException>(() => new MetricFilter(":_=\"asdf\",_:=\"asdf\"} [^34$#@!]"));
        }

        [Fact]
        public void TestObservedProblems()
        {
            MetricFilter filter = new MetricFilter("docker_container_disk_*_bytes{name!~\"dgeHubDev\"}");
            Assert.True(filter.Matches(new Metric(testTime, "docker_container_disk_read_bytes", 5, new Dictionary<string, string>() {["name"] = "foobar"})));
            Assert.True(filter.Matches(new Metric(testTime, "docker_container_disk_read_bytes", 5, new Dictionary<string, string>() {["name"] = "EdgeHubDev"})));  // EdgeHubDev doesn't match because regexes are fully anchored, ^dgeHubDev$
            Assert.False(filter.Matches(new Metric(testTime, "docker_container_disk_read_bytes", 5, new Dictionary<string, string>())));
            Assert.True(filter.Matches(new Metric(testTime, "docker_container_disk__read_bytes", 5, new Dictionary<string, string>() {["name"] = "foobar"})));
            Assert.True(filter.Matches(new Metric(testTime, "docker_container_disk__read_bytes", 5, new Dictionary<string, string>() {["name"] = "EdgeHubDev"})));
            Assert.False(filter.Matches(new Metric(testTime, "docker_container_disk__read_bytes", 5, new Dictionary<string, string>())));

            filter = new MetricFilter("docker_container_disk_*_bytes{name!~\"*dgeHubDev\"}");
            Assert.True(filter.Matches(new Metric(testTime, "docker_container_disk_read_bytes", 5, new Dictionary<string, string>() {["name"] = "foobar"})));
            Assert.False(filter.Matches(new Metric(testTime, "docker_container_disk_read_bytes", 5, new Dictionary<string, string>() {["name"] = "EdgeHubDev"})));
            Assert.False(filter.Matches(new Metric(testTime, "docker_container_disk_read_bytes", 5, new Dictionary<string, string>())));
        }
    }
}
