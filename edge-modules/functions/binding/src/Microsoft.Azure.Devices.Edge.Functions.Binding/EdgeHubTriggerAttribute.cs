// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Functions.Binding
{
    using System;
    using Microsoft.Azure.WebJobs.Description;

    /// <summary>
    /// Attribute used to bind a parameter to a EdgeHub message, causing the function to run when a
    /// message is sent.
    /// </summary>
    /// <remarks>
    /// When the function is triggered the parameter type is a Message.
    /// </remarks>
    [Binding]
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class EdgeHubTriggerAttribute : Attribute
    {
        public EdgeHubTriggerAttribute(string inputName)
        {
            this.InputName = inputName;
        }

        /// <summary>
        /// Gets the EdgeHub message inputName that triggers the function.
        /// </summary>
        public string InputName { get; private set; }
    }
}