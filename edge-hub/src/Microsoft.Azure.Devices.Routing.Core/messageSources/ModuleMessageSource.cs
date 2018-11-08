// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.MessageSources
{
    using System.Globalization;

    using Microsoft.Azure.Devices.Routing.Core.Util;

    public class ModuleMessageSource : BaseMessageSource
    {
        const string SourcePatternWithOutput = "/messages/modules/{0}/outputs/{1}";
        const string SourcePattern = "/messages/modules/{0}";

        public ModuleMessageSource(string source)
            : base(source)
        {
        }

        public static ModuleMessageSource Create(string moduleId, string outputName)
        {
            Preconditions.CheckArgument(!string.IsNullOrWhiteSpace(moduleId), "ModuleId cannot be null or empty");
            Preconditions.CheckArgument(!string.IsNullOrWhiteSpace(outputName), "OutputEndpoint cannot be null or empty");

            return new ModuleMessageSource(string.Format(CultureInfo.InvariantCulture, SourcePatternWithOutput, moduleId, outputName));
        }

        public static ModuleMessageSource Create(string moduleId)
        {
            Preconditions.CheckArgument(!string.IsNullOrWhiteSpace(moduleId), "ModuleId cannot be null or empty");

            return new ModuleMessageSource(string.Format(CultureInfo.InvariantCulture, SourcePattern, moduleId));
        }
    }
}
