// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports
{
    public abstract class TestReportMetadataBase
    {

        public TestReportMetadataBase(string testDescription)
        {
            this.TestDescription = testDescription;
        }
        public string TestDescription { get; }

        public override string ToString()
        {
            return $"TestDescription: {this.TestDescription}";
        }
    }
}
