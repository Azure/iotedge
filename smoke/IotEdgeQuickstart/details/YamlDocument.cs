// Copyright (c) Microsoft. All rights reserved.
namespace IotEdgeQuickstart.Details
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using YamlDotNet.Serialization;

    class YamlDocument
    {
        readonly Dictionary<object, object> root;

        public YamlDocument(string input)
        {
            var reader = new StringReader(input);
            var deserializer = new Deserializer();
            this.root = (Dictionary<object, object>)deserializer.Deserialize(reader);
        }

        public void ReplaceOrAdd(string dottedKey, string value)
        {
            Dictionary<object, object> node = this.root;
            string[] segments = dottedKey.Split('.');
            foreach (string key in segments.SkipLast(1))
            {
                if (!node.ContainsKey(key))
                {
                    node.Add(key, new Dictionary<object, object>());
                }
                node = (Dictionary<object, object>)node[key];
            }

            string leaf = segments.Last();
            if (!node.ContainsKey(leaf))
            {
                node.Add(leaf, value);
            }
            node[leaf] = value;
        }

        public override string ToString()
        {
            var serializer = new Serializer();
            return serializer.Serialize(this.root);
        }
    }
}
