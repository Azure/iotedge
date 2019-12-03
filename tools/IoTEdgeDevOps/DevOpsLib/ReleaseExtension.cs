// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib
{
    public static class ReleaseExtension
    {
        public static string IdString(this ReleaseDefinitionId buildDefinitionId) => ((int)buildDefinitionId).ToString();
    }
}
