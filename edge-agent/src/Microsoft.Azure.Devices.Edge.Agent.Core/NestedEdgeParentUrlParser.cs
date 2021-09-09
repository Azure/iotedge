// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using System.Text.RegularExpressions;
    using Microsoft.Azure.Devices.Edge.Util;

    public class NestedEdgeParentUriParser : INestedEdgeParentUriParser
    {
        const string ImageUpstreamRegexPattern = @"(?<pre>.*)\$upstream(?<post>:[1-9].*)";
        static readonly Regex ImageUpstreamRegex = new Regex(ImageUpstreamRegexPattern);

        public Option<string> ParseURI(string uri)
        {
            return this.ParseURI(uri, new EnvironmentWrapper());
        }

        internal Option<string> ParseURI(string uri, IEnvironmentWrapper env)
        {
            Option<string> parsedURI = Option.None<string>();

            Match matchHost = ImageUpstreamRegex.Match(uri);
            if (matchHost.Success
                && (matchHost.Groups["post"]?.Length > 0))
            {
                string hostAddress = env.GetVariable(Core.Constants.GatewayHostnameVariableName).
                    Expect(() => new InvalidOperationException($"Could not find environment variable: {Core.Constants.GatewayHostnameVariableName}"));

                parsedURI = Option.Some(matchHost.Groups["pre"].Value + hostAddress + matchHost.Groups["post"].Value);
            }

            return parsedURI;
        }
    }

    internal interface IEnvironmentWrapper
    {
        Option<string> GetVariable(string variableName);
    }

    internal class EnvironmentWrapper : IEnvironmentWrapper
    {
        public Option<string> GetVariable(string variableName)
        {
            return Option.Some(Environment.GetEnvironmentVariable(variableName));
        }
    }
}
