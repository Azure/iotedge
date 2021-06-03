// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Azure.Devices.Edge.Azure.Monitor
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Contracts;
    using System.Linq;
    using System.Text.RegularExpressions;

    /*
    A "context free gramar" for metric matchers:

    <matcher> -> <metricname>
    <matcher> -> <metricname><whitespace?>{<labelList>}
    <matcher> -> <metricname> <endpointExp>
    <matcher> -> <metricname><whitespace?>{<labelList>} <endpointExp>

    <metricname> -> <anything matching the regex [a-zA-Z_:\*\?][a-zA-Z0-9_:\*\?]*>

    <labelList> -> <label> , <labelList>
    <labelList> -> <label>
    <label> -> [a-zA-Z_][a-zA-Z0-9_]* <comparator> "<any sequence of valid unicode characters without quotes>"
    <comparator> -> =
    <comparator> -> !=
    <comparator> -> =~
    <comparator> -> !~

    <endpointExp> -> endpoint=<URI>

    <URI> -> one or more non-whitespace characters (regex: \S+)
    <whitespace?> -> any whitespace character or nothing

    note: URIs are defined in RFC3986 as ^(([^:/?#]+):)?(//([^/?#]*))?([^?#]*)(\?([^#]*))?(#(.*))? but we will match any sequence of characters excluding whitespace.
    note: Any sequence of whitespace characters are interpreted as a single space\
    */

    public class MetricFilter
    {
        public bool Empty => matchers.Count == 0;
        private readonly List<SingleMetricMatcher> matchers = new List<SingleMetricMatcher>();

        private String metricList;

        // Breaks apart a list of matchers into individual matchers and capture each component. 
        private readonly static Regex matcherSplitter = new Regex(@"(?<name>[a-zA-Z0-9_:\*\?]+)(\s*(?<labels>{([^{}""]*|""[^""]+"")*}))?(?n:[\s]*\[[\s]*(?<endpoint>[^\s]+)[\s]*\])?", RegexOptions.Compiled);

        public MetricFilter(string metricList)
        {
            Contract.Requires(metricList != null);

            this.metricList = metricList;

            // make sure all of input was consumed by the regex
            if (matcherSplitter.Replace(metricList, "").Replace(",", "").Trim() != "")
                throw new ArgumentException("invalid metric matcher in " + metricList);

            foreach (Match match in matcherSplitter.Matches(metricList))
            {
                string name = match.Groups["name"].Value;
                string labels = match.Groups["labels"].Value;
                string endpoint = match.Groups["endpoint"].Value;
                SingleMetricMatcher matcher = new SingleMetricMatcher(name, labels, endpoint);
                this.matchers.Add(matcher);
            }
        }

        /// <summary>
        /// Returns true if the passed metric matches a selector in this filter.
        /// Returns false if no metrics are in this filter.
        /// </summary>
        public bool Matches(Metric testMetric)
        {
            foreach (SingleMetricMatcher matcher in this.matchers)
            {
                if (matcher.Matches(testMetric))
                    return true;
            }
            return false;
        }

        public override string ToString()
        {
            return this.metricList;
        }

        private class SingleMetricMatcher
        {
            // this regex splits up a list of labels into name, comparitor, value groups
            private readonly static Regex labelSplitterRegex = new Regex(@"(?<label>(?<name>[a-zA-Z_][a-zA-Z0-9_]*)\s*(?<comparator>(=|!=|=~|!~))\s*""(?<value>[^""]*)""\s*(,|}))", RegexOptions.Compiled);
            // this regex validates metric names
            private readonly static Regex metricNameValidator = new Regex(@"^[a-zA-Z_:\*\?][a-zA-Z0-9_:\*\?]*$", RegexOptions.Compiled);
            private readonly Regex metricNameMatcher;
            private readonly Dictionary<string, Regex> tagMatchers = new Dictionary<string, Regex>();

            // inverting passed regex strings is hard, so store if the comparator has a not in it (!= and !~)
            private readonly Dictionary<string, bool> tagMatcherInvert = new Dictionary<string, bool>();
            private readonly string endpoint;
            public SingleMetricMatcher(string metricName, string labelsString, string endpoint)
            {
                Contract.Requires(metricName != null);
                Contract.Requires(labelsString != null);
                Contract.Requires(endpoint != null);
                Contract.Requires(metricName != "");

                this.endpoint = endpoint;

                // check if the metric name is valid and create a regex to match it
                if (metricNameValidator.Matches(metricName).Count != 1)
                    throw new ArgumentException("passed metric name " + metricName + " not valid");
                metricNameMatcher = new Regex("^" + metricName.Replace("*", "[a-zA-Z0-9_:]*").Replace("?", "[a-zA-Z0-9_:]") + "$", RegexOptions.Compiled);

                // Parse the labels
                if (labelsString != "" && labelsString != "{}")
                {
                    Debug.Assert(labelsString[0] == '{');
                    Debug.Assert(labelsString.Last() == '}');

                    labelsString = labelsString.Substring(1);  // get rid of leading {

                    // make sure all input is consumed
                    if (labelSplitterRegex.Replace(labelsString, "").Trim() != "")
                        throw new ArgumentException("Invalid label in metric matcher " + labelsString);

                    foreach (Match label in labelSplitterRegex.Matches(labelsString))
                    {
                        string labelName = label.Groups["name"].Value;
                        string comparator = label.Groups["comparator"].Value;
                        string value = label.Groups["value"].Value;

                        Regex valueSelector = null;
                        bool invertValueSelector;

                        if (comparator == "=" || comparator == "!=")
                            valueSelector = new Regex("^" + Regex.Escape(value) + "$", RegexOptions.Compiled);
                        else if (comparator == "=~" || comparator == "!~")
                            valueSelector = new Regex("^" + value + "$", RegexOptions.Compiled);
                        else
                            throw new ArgumentException("not a valid metric selector label: " + label.Groups["label"].Value);

                        if (comparator == "=" || comparator == "=~")
                            invertValueSelector = false;
                        else if (comparator == "!=" || comparator == "!~")
                            invertValueSelector = true;
                        else
                            throw new ArgumentException("not a valid metric selector label: " + label.Groups["label"].Value);

                        try
                        {
                            tagMatchers.Add(labelName, valueSelector);
                            tagMatcherInvert.Add(labelName, invertValueSelector);
                        }
                        catch (ArgumentException)
                        {
                            throw new ArgumentException("error parsing metric matcher, is there a duplicate label?");
                        }
                    }
                }
            }

            public bool Matches(Metric testMetric)
            {
                // endpoint is easy to test (since it doesn't involve regexes), do it first
                if (this.endpoint != "" && testMetric.Endpoint != this.endpoint)
                    return false;

                // next test metric name
                if (metricNameMatcher.Matches(testMetric.Name).Count != 1)
                    return false;

                // now do tags
                foreach (var labelRegexPair in tagMatchers)
                {
                    string name = labelRegexPair.Key;
                    Regex matcher = labelRegexPair.Value;

                    if (testMetric.Tags.TryGetValue(name, out string labelVal))
                    {
                        int numMatches = matcher.Matches(labelVal).Count;
                        bool shouldInvert = tagMatcherInvert[name];
                        if (numMatches == 1 && shouldInvert)
                            return false;  // the label has the right value but != or !~ were used
                        if (numMatches == 0 && !shouldInvert)
                            return false;  // the label does not have the right value and = or =~ were used
                    }
                    else
                    {
                        return false;  // testMetric does not contain a required label
                    }
                }

                // all conditions pass
                return true;
            }
        }
    }
}
