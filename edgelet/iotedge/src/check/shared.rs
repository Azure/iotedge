use crate::check::Check;

#[derive(Debug, Copy, Clone, serde::Serialize)]
pub struct CheckerMeta {
    /// Unique human-readable identifier for the check.
    pub id: &'static str,
    /// A brief description of what this check does.
    pub description: &'static str,
}

#[async_trait::async_trait]
pub trait Checker: erased_serde::Serialize {
    fn meta(&self) -> CheckerMeta;

    async fn execute(&mut self, shared: &mut Check) -> CheckResult;
}

erased_serde::serialize_trait_object!(Checker);

/// The various ways a check can resolve.
///
/// Check functions return `Result<CheckResult, failure::Error>` where `Err` represents the check failed.
#[derive(Debug)]
pub enum CheckResult {
    /// Check succeeded.
    Ok,

    /// Check failed with a warning.
    Warning(failure::Error),

    /// Check is not applicable and was ignored. Should be treated as success.
    Ignored,

    /// Check was skipped because of errors from some previous checks. Should be treated as an error.
    Skipped,

    /// Check as skipped due to a reason. Should be treated as success.
    SkippedDueTo(String),

    /// Check failed, and further checks should be performed.
    Failed(failure::Error),

    /// Check failed, and further checks should not be performed.
    Fatal(failure::Error),
}
