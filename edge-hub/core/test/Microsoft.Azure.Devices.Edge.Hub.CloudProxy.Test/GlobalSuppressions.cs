// Copyright (c) Microsoft. All rights reserved.
using System.Diagnostics.CodeAnalysis;

// This file is used by Code Analysis to maintain SuppressMessage attributes that are applied to this project.
// Project-level suppressions either have no target or are given a specific target and scoped to a namespace, type, member, etc.
[assembly: SuppressMessage(
    "Usage",
    "xUnit1026:Theory methods should use all of their parameters",
    Justification = "Some data methods are reused, therefore some columns are not used in every test.")]
