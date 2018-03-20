// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Text;
    using System.Text.RegularExpressions;
    using Microsoft.Azure.Devices.Edge.Util;

    public class UriPathTemplate
    {
        const char PathSeparator = '/';
        const char VariableNameValueSeparator = '=';
        const char WildcardCharacter = '*';
        const char VariablePlaceholderStartCharacter = '{';
        const char VariablePlaceholderEndCharacter = '}';
        const char PeriodCharacter = '.';
        const int EstimatedVariableValueLength = 20;

        public static readonly char[] PathSegmentTerminationCharacters = { PathSeparator };

        TemplatePart[] parts;
        int projectedLength;
        Regex pattern;
        IList<string> variablesName;

        public UriPathTemplate(string template)
        {
            Contract.Requires(template != null);

            this.Compile(template);
        }

        public string Bind(IDictionary<string, string> variables)
        {
            var result = new StringBuilder(this.projectedLength); // todo: calc estimated initial capacity during compilation

            TemplatePart[] templateParts = this.parts;
            int partsLength = templateParts.Length;

            for (int index = 0; index < partsLength; index++)
            {
                string partValue = templateParts[index].Bind(variables);
                if (string.IsNullOrEmpty(partValue))
                {
                    continue;
                }
                if (result.Length > 0 && result[result.Length - 1] == PathSeparator && partValue[0] == PathSeparator)
                {
                    result.Append(partValue, 1, partValue.Length - 1);
                }
                else
                {
                    result.Append(partValue);
                }
            }
            return result.ToString();
        }

        public (bool Success, IList<KeyValuePair<string, string>> Matches) Match(Uri uri)
        {
            IList<KeyValuePair<string, string>> result = new List<KeyValuePair<string, string>>();
            Match match = this.pattern.Match(uri.ToString());
            bool success = match.Success;
            while (match.Success)
            {
                for (int i = 1; i < match.Groups.Count; i++)
                {
                    string value = match.Groups[i].Value;
                    result.Add(new KeyValuePair<string, string>(this.variablesName[i - 1], value));
                }

                match = match.NextMatch();
            }

            return (success, result);
        }

        void Compile(string template)
        {
            var templateParts = new List<TemplatePart>();
            var patternStringBuilder = new StringBuilder();
            this.variablesName = new List<string>();

            int initialCapacity = 0;

            int length = template.Length;
            int index = 0;

            while (index < length)
            {
                int varStartIndex = template.IndexOf(VariablePlaceholderStartCharacter, index);
                if (varStartIndex != -1)
                {
                    int varEndIndex = template.IndexOf(VariablePlaceholderEndCharacter, varStartIndex + 1);
                    if (varEndIndex == -1)
                    {
                        throw new InvalidOperationException("Variable definition is never closed.");
                    }

                    string varDefinition = template.Substring(varStartIndex + 1, varEndIndex - varStartIndex - 1);

                    if (varDefinition.IndexOf(VariablePlaceholderStartCharacter) != -1)
                    {
                        throw new InvalidOperationException("Variable definition syntax is invalid in template definition.");
                    }

                    int eqIndex = varDefinition.IndexOf(VariableNameValueSeparator);
                    int nameOffset;
                    if (varDefinition[0] == WildcardCharacter)
                    {
                        if (varEndIndex < length - 1)
                        {
                            throw new InvalidOperationException("Wildcard variable can only be used at the end of the template.");
                        }
                        nameOffset = 1;
                    }
                    else
                    {
                        nameOffset = 0;
                    }
                    string varName;
                    if (eqIndex == -1)
                    {
                        varName = nameOffset == 0 ? varDefinition : varDefinition.Substring(nameOffset);
                    }
                    else
                    {
                        varName = varDefinition.Substring(nameOffset, eqIndex);
                    }
                    string varDefaultValue = eqIndex == -1 ? null : varDefinition.Substring(eqIndex + 1);

                    if (varStartIndex > index)
                    {
                        int partLength = varStartIndex - index;
                        templateParts.Add(new TemplatePart(template.Substring(index, partLength)));
                        patternStringBuilder.Append(Regex.Escape(template.Substring(index, partLength)));
                        initialCapacity += partLength;
                    }
                    templateParts.Add(new TemplatePart(varName, varDefaultValue));
                    this.variablesName.Add(varName);
                    patternStringBuilder.Append("([^/]*)");
                    initialCapacity += EstimatedVariableValueLength;
                    index = varEndIndex + 1;
                }
                else
                {
                    int partLength = length - index;
                    string part = template.Substring(index, partLength);
                    templateParts.Add(new TemplatePart(part));
                    // don't escape wildcard if it is at the end of the template
                    if (part.EndsWith(WildcardCharacter.ToString()) && index + partLength == length)
                    {
                        patternStringBuilder.Append(Regex.Escape(part.Substring(0, partLength - 1)));
                        patternStringBuilder.Append(PeriodCharacter.ToString() + WildcardCharacter.ToString());
                    }
                    else
                    {
                        patternStringBuilder.Append(Regex.Escape(part));
                    }

                    initialCapacity += partLength;
                    index = length;
                }
            }

            this.parts = templateParts.ToArray();
            this.projectedLength = initialCapacity;
            patternStringBuilder.Append("$");
            this.pattern = new Regex(patternStringBuilder.ToString(), RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }

        struct TemplatePart
        {
            string VariableName { get; }

            string Value { get; }

            public TemplatePart(string value)
            {
                Preconditions.CheckNotNull(value, nameof(value));

                this.VariableName = null;
                this.Value = value;
            }

            public TemplatePart(string variableName, string defaultValue)
            {
                Preconditions.CheckNotNull(variableName, nameof(variableName));

                this.VariableName = variableName;
                this.Value = defaultValue;
            }

            public string Bind(IDictionary<string, string> variables)
            {
                if (this.VariableName == null)
                {
                    return this.Value;
                }
                else
                {
                    if (!variables.TryGetValue(this.VariableName, out string variableValue))
                    {
                        if (this.Value == null) // comparison to null is correct. empty string is allowed as a default value.
                        {
                            throw new InvalidOperationException("Variable was not provided and has no default value to fallback to.");
                        }
                        return this.Value;
                    }
                    return variableValue;
                }
            }
        }
    }
}
