// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using Nett;
    using System.Collections.Generic;
    using System.Linq;

    class TomlDocument : IConfigDocument
    {
        private TomlTable document;

        public TomlDocument(string input)
        {
            this.document = Toml.ReadString(input);
        }

        public void ReplaceOrAdd(string dottedKey, string value)
        {
            string[] segments = dottedKey.Split(".");
            TomlTable table = this.document;

            for(int i = 0; i < segments.Length - 1; i++)
            {
                string tableKey = segments[i];

                try
                {
                    table = (TomlTable)table[tableKey];
                }
                catch(KeyNotFoundException)
                {
                    // Nett does not provide a function to easily add table subkeys.
                    // A hack workaround is to serialize the table, append the new table as a string, then deserialize.
                    // This only needs to be done once to create the new subtable
                    // After that, Nett functions can be used to modify the subtable.
                    string tableName = string.Join(".", segments.Take(segments.Length - 1));
                    AddTable(tableName, segments[segments.Length - 1], value);
                    return;
                }
            }

            string key = segments[segments.Length - 1];
            if(table.ContainsKey(key))
            {
                table.Update(key, value);
            }
            else
            {
                table.Add(key, value);
            }
        }

        public void RemoveIfExists(string dottedKey)
        {
            string[] segments = dottedKey.Split(".");
            TomlTable table = this.document;

            for(int i = 0; i < segments.Length - 1; i++)
            {
                try
                {
                    table = (TomlTable)table[segments[i]];
                }
                catch(KeyNotFoundException)
                {
                    // Key does not exist in table; do nothing.
                    return;
                }
            }

            table.Remove(segments[segments.Length-1]);
        }

        public override string ToString()
        {
            return document.ToString();
        }

        void AddTable(string tableName, string key, string value)
        {
            this.document = Toml.ReadString(
                this.document.ToString() + "\n" +
                $"[{tableName}]\n" +
                $"{key} = \"{value}\"\n"
            );
        }
    }
}
