use crate::check::CheckResult;

pub(crate) trait Checker {
    fn id(&self) -> &'static str;
    fn description(&self) -> &'static str;
    fn result(&self) -> &CheckResult;
}
