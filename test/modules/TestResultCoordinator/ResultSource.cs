// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator
{
    class ResultSource
    {
        public string Source { get; set; }

        public string Type { get; set; }

        public override string ToString()
        {
            return $"Source: {this.Source}, Type: {this.Type}";
        }
    }
}
