// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Collections.Generic;
    using Nett;

    class TomlDocument
    {
        private TomlTable document;

        public TomlDocument(string input)
        {
            this.document = Toml.ReadString(input);
        }

        (TomlTable table, string key) TraverseKey(string dottedKey, bool add = false)
        {
            string[] segments = dottedKey.Split(".");
            TomlTable table = this.document;

            for (int i = 0; i < segments.Length - 1; i++)
            {
                string tableKey = segments[i];

                if (!table.ContainsKey(tableKey))
                {
                    if (add)
                    {
                        table.Add(tableKey, table.CreateEmptyAttachedTable());
                        Serilog.Log.Information($" {new string(' ', i)}+ [{tableKey}]");
                    }
                    else
                    {
                        Serilog.Log.Information($"Table not found at {dottedKey}");
                        return (null, string.Empty);
                    }
                }
                else
                {
                    Serilog.Log.Information($" {new string(' ', i)}* [{tableKey}]");
                }

                table = (TomlTable)table[tableKey];
            }

            return (table, segments[segments.Length - 1]);
        }

        public void ReplaceOrAdd<T>(string dottedKey, T value)
        {
            var (table, key) = this.TraverseKey(dottedKey, add: true);

            if (table.ContainsKey(key))
            {
                table.Update(key, value);
                Serilog.Log.Information($" ~ {key} = {value} [{typeof(T)}]");
            }
            else
            {
                table.Add(key, value);
                Serilog.Log.Information($" + {key} = {value} [{typeof(T)}]");
            }
        }

        public void RemoveIfExists(string dottedKey)
        {
            var (table, key) = this.TraverseKey(dottedKey);
            if (string.IsNullOrEmpty(key))
            {
                table.Remove(key);
                Serilog.Log.Information($" X {key}");
            }
        }

        public override string ToString()
        {
            return Toml.WriteString(this.document);
        }
    }
}
