// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using Microsoft.Azure.Devices.Edge.Util;

    public class Registry
    {
        public string Address { get; }

        public string Username { get; }

        public string Password { get; }

        public Registry(
            string address,
            string username,
            string password)
        {
            this.Address = Preconditions.CheckNotNull(address, nameof(address));
            this.Username = Preconditions.CheckNotNull(username, nameof(username));
            this.Password = Preconditions.CheckNotNull(password, nameof(password));
        }
    }
}
