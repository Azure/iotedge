// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    public class ModuleIdentityHelper
    {
        // System modules have different module names and identity names. We need to convert module names to module identity names
        // and vice versa, to make sure the right values are being used.
        // TODO - This will fail if the user adds modules with the same module name as a system module - for example a module called
        // edgeHub. We might have to catch such cases and flag them as error (or handle them in some other way).

        public static string GetModuleIdentityName(string moduleName)
        {
            if (moduleName.Equals(Constants.EdgeHubModuleName))
            {
                return Constants.EdgeHubModuleIdentityName;
            }
            else if (moduleName.Equals(Constants.EdgeAgentModuleName))
            {
                return Constants.EdgeAgentModuleIdentityName;
            }
            return moduleName;
        }

        public static string GetModuleName(string moduleIdentityName)
        {
            if (moduleIdentityName.Equals(Constants.EdgeHubModuleIdentityName))
            {
                return Constants.EdgeHubModuleName;
            }
            else if (moduleIdentityName.Equals(Constants.EdgeAgentModuleIdentityName))
            {
                return Constants.EdgeAgentModuleName;
            }
            return moduleIdentityName;
        }
    }
}
