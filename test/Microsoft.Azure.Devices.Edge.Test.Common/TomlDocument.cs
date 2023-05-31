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

        (TomlTable table, string key) TraverseKey(string dottedKey)
        {
            string[] segments = dottedKey.Split(".");
            TomlTable table = this.document;

            for (int i = 0; i < segments.Length - 1; i++)
            {
                string tableKey = segments[i];

                if (!table.ContainsKey(tableKey))
                {
                    table.Add(tableKey, table.CreateEmptyAttachedTable());
                }

                table = (TomlTable)table[tableKey];
            }

            return (table, segments[segments.Length - 1]);
        }

        public void ReplaceOrAdd(string dottedKey, Dictionary<string, string> value)
        {
            var (table, key) = TraverseKey(dottedKey);

            if (table.ContainsKey(key))
            {
                var elem = table[key];
                if (elem.TomlType != TomlObjectType.ArrayOfTables)
                {
                    throw new ArgumentException(
                        $"Tried to overwrite TOML value of type {elem.TomlType} with value of " +
                        $"type {TomlObjectType.ArrayOfTables}");
                }

                // add existing elements to the TOML array of tables
                var list = ((TomlTableArray)elem).Items;

                // add new element
                list.Add(elem.CreateAttached(value));
            }
            else
            {
            }
        }

        public void ReplaceOrAdd<T>(string dottedKey, T value)
        {
            var (table, key) = TraverseKey(dottedKey);

            if (table.ContainsKey(key))
            {
                table.Update(key, value.ToString()); // May need to fix to support other types.
            }
            else
            {
                table.Add(key, value.ToString()); // May need to fix to support other types.
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
    }
}
