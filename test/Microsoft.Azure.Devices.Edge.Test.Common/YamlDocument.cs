// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.IO;
    using System.Linq;
    using YamlDotNet.Serialization;
    using Node = System.Collections.Generic.Dictionary<object, object>;

    class YamlDocument
    {
        readonly Node root;

        public YamlDocument(string input)
        {
            var reader = new StringReader(input);
            var deserializer = new Deserializer();
            this.root = (Node)deserializer.Deserialize(reader);
        }

        public void ReplaceOrAdd(string dottedKey, string value)
        {
            this.Do(
                dottedKey,
                (innerNode, missingKey) =>
                {
                    innerNode.Add(missingKey, new Node());
                    return true;
                },
                (parentNode, leafKey) =>
                {
                    if (!parentNode.ContainsKey(leafKey))
                    {
                        parentNode.Add(leafKey, value);
                    }

                    parentNode[leafKey] = value;
                });
        }

        public void RemoveIfExists(string dottedKey)
        {
            this.Do(
                dottedKey,
                (n, k) => false,
                (parentNode, leafKey) => parentNode.Remove(leafKey));
        }

        void Do(string dottedKey, Func<Node, string, bool> onMissing, Action<Node, string> op)
        {
            Node node = this.root;
            string[] segments = dottedKey.Split('.');
            foreach (string key in segments.SkipLast(1))
            {
                if (!node.ContainsKey(key) && !onMissing(node, key))
                {
                    return;
                }

                node = (Node)node[key];
            }

            string last = segments.Last();
            op(node, last);
        }

        public override string ToString()
        {
            var serializer = new Serializer();
            return serializer.Serialize(this.root);
        }
    }
}
