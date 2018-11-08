// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Endpoints.StateMachine
{
    using System;
    using System.Globalization;

    /// <summary>
    /// Stores current state and command to be used as a key
    /// in the state transition table.
    /// </summary>
    public struct StateCommandPair : IEquatable<StateCommandPair>
    {
        readonly State state;
        readonly CommandType command;

        public StateCommandPair(State state, CommandType command)
        {
            this.state = state;
            this.command = command;
        }

        public static bool operator ==(StateCommandPair pair1, StateCommandPair pair2)
        {
            return pair1.Equals(pair2);
        }

        public static bool operator !=(StateCommandPair pair1, StateCommandPair pair2)
        {
            return !pair1.Equals(pair2);
        }

        public bool Equals(StateCommandPair other)
        {
            return this.state == other.state && this.command == other.command;
        }

        public override bool Equals(object obj)
        {
            return obj is StateCommandPair && this.Equals((StateCommandPair)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)this.state * 397) ^ (int)this.command;
            }
        }

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "StateCommandPair({0}, {1})", this.state, this.command);
    }
}
