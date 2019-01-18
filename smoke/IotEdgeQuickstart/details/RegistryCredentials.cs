// Copyright (c) Microsoft. All rights reserved.
namespace IotEdgeQuickstart.Details
{
    using System;

    public class RegistryCredentials
    {
        public RegistryCredentials(string address, string user, string password)
        {
            if (string.IsNullOrEmpty(address))
            {
                throw new ArgumentException("address cannot be null or empty");
            }

            if (string.IsNullOrEmpty(user))
            {
                throw new ArgumentException("user cannot be null or empty");
            }

            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentException("password cannot be null or empty");
            }

            this.Address = address;
            this.User = user;
            this.Password = password;
        }

        public string Address { get; }

        public string User { get; }

        public string Password { get; }
    }
}
