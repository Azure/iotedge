// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Kubernetes
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public static class KubeUtils
    {
        public static async Task<Option<string>> FindPod(string moduleName, CancellationToken token)
        {
            string podname = moduleName.ToLower();
            string getPods = $"get pods --namespace {Constants.Deployment} --template=\"{{range.items}}{{println .metadata.name}}{{end}}\"";
            string[] pods_list = await Process.RunAsync("kubectl", getPods, token);
            return Option.Maybe(pods_list.SingleOrDefault(pod => pod.StartsWith(podname)));
        }
    }
}
