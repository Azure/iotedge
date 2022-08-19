// Copyright (c) Microsoft. All rights reserved.

#[derive(Debug, thiserror::Error)]
pub enum Error {
    #[error("unable to get virtualization status")]
    GetVirtualizationStatus,

    #[error("An error occurred when obtaining the HSM version")]
    HsmVersion,

    #[error("invalid log tail: {0:?}")]
    InvalidLogTail(String),

    #[error("module runtime error")]
    ModuleRuntime,

    #[error("unable to parse \"since\"")]
    ParseSince,

    #[error("signing error")]
    Sign,

    #[error("workload manager error")]
    WorkloadManager,
}
