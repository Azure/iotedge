// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System.IO;
    using System.Linq;
    using YamlDotNet.Serialization;

    using Node = System.Collections.Generic.Dictionary<object, object>;

    // This class assumes the input document contains only "mapping" nodes,
    // not "scalars" or "sequences" (see the YAML spec:
    // https://yaml.org/spec/1.1/#id861435). Specifically, it assumes that
    // each node in the deserialized document can be represented by
    // Dictionary<object, object>.
    class YamlDocument : IConfigDocument
    {
        readonly Node root;

        public YamlDocument(string input)
        {
            var reader = new StringReader(input);
            var deserializer = new Deserializer();
            this.root = (Node)deserializer.Deserialize(reader);
        }

        public void ReplaceOrAdd<T>(string dottedKey, T value)
        {
            Node node = this.root;
            string[] segments = dottedKey.Split('.');
            foreach (string key in segments.SkipLast(1))
            {
                if (!node.ContainsKey(key))
                {
                    node.Add(key, new Node());
                }

                node = (Node)node[key];
            }

            string leaf = segments.Last();
            if (!node.ContainsKey(leaf))
            {
                node.Add(leaf, value);
            }

            node[leaf] = value;
        }

        public void RemoveIfExists(string dottedKey)
        {
            Node node = this.root;
            string[] segments = dottedKey.Split('.');
            foreach (string key in segments.SkipLast(1))
            {
                if (!node.ContainsKey(key))
                {
                    return;
                }

                node = (Node)node[key];
            }

            string leaf = segments.Last();
            node.Remove(leaf);
        }

        public override string ToString()
        {
            var serializer = new Serializer();
            return serializer.Serialize(this.root);
        }
    }
}
