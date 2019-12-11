// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator
{
    interface ITestResultComparer<T>
    {
        bool Matches(T value1, T value2);
    }
}
