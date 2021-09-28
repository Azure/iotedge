// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Nett;

    class TomlDocument
    {
        private TomlTable document;

        public TomlDocument(string input)
        {
            this.document = Toml.ReadString(input);
        }

        public void ReplaceOrAdd<T>(string dottedKey, T value)
        {
            string[] segments = dottedKey.Split(".");
            TomlTable table = this.document;

            for (int i = 0; i < segments.Length - 1; i++)
            {
                string tableKey = segments[i];

                try
                {
                    table = (TomlTable)table[tableKey];
                }
                catch (KeyNotFoundException)
                {
                    // Nett does not provide a function to easily add table subkeys.
                    // A hack workaround is to serialize the table, append the new table as a string, then deserialize.
                    // This only needs to be done once to create the new subtable.
                    // After that, Nett functions can be used to modify the subtable.
                    string tableName = string.Join(".", segments.Take(segments.Length - 1));
                    this.AddTable(tableName, segments[segments.Length - 1], value);
                    return;
                }
            }

            string key = segments[segments.Length - 1];
            if (table.ContainsKey(key))
            {
                if (value is string)
                {
                    table.Update(key, value.ToString()); // May need to fix to support other
                }
                else
                {
                    int val = Convert.ToInt32(value);
                    table.Update(key, val);
                }
            }
            else
            {
                if (value is string)
                {
                    table.Add(key, value.ToString()); // May need to fix to support other types.
                }
                else
                {
                    int val = Convert.ToInt32(value);
                    table.Add(key, val);
                }
            }
        }

        public void RemoveIfExists(string dottedKey)
        {
            string[] segments = dottedKey.Split(".");
            TomlTable table = this.document;

            for (int i = 0; i < segments.Length - 1; i++)
            {
                try
                {
                    table = (TomlTable)table[segments[i]];
                }
                catch (KeyNotFoundException)
                {
                    // Key does not exist in table; do nothing.
                    return;
                }
            }

            table.Remove(segments[segments.Length - 1]);
        }

        public override string ToString()
        {
            return this.document.ToString();
        }

        void AddTable<T>(string tableName, string key, T value)
        {
            string v = value is string ? $"\"{value}\"" : value.ToString().ToLower();

            this.document = Toml.ReadString(
                this.document.ToString() + "\n" +
                $"[{tableName}]\n" +
                $"{key} = {v}\n");
        }
    }
}
