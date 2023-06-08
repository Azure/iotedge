// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Test.Common
{
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
                    }
                    else
                    {
                        return (table, string.Empty);
                    }
                }

                table = (TomlTable)table[tableKey];
            }

            return (table, segments[segments.Length - 1]);
        }

        public void ReplaceOrAdd(string dottedKey, bool value)
        {
            var (table, key) = this.TraverseKey(dottedKey, add: true);

            if (table.ContainsKey(key))
            {
                table.Update(key, value);
            }
            else
            {
                table.Add(key, value);
            }
        }

        public void ReplaceOrAdd(string dottedKey, string value)
        {
            var (table, key) = this.TraverseKey(dottedKey, add: true);

            if (table.ContainsKey(key))
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
            var (table, key) = this.TraverseKey(dottedKey);
            if (!string.IsNullOrEmpty(key))
            {
                table.Remove(key);
            }
        }

        public override string ToString()
        {
            return Toml.WriteString(this.document);
        }
    }
}
