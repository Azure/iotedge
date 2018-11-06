use edgelet_docker::{DockerModuleRuntime, Settings as DockerSettings};

use super::{init_common, log_banner};
use error::Error;
use logging;

#[cfg(target_os = "windows")]
pub fn init() -> Result<(DockerModuleRuntime, DockerSettings), Error> {
    let (settings, matches) = init_common()?;

    if matches.is_present("use-event-logger") {
        logging::init_win_log();
    } else {
        logging::init();
    }

    log_banner();

    Ok((DockerModuleRuntime::new(), settings))
}

#[cfg(not(target_os = "windows"))]
pub fn init() -> Result<(DockerModuleRuntime, DockerSettings), Error> {
    logging::init();
    log_banner();
    init_common().map(|(settings, _)| (DockerModuleRuntime::new(), settings))
}

#[cfg(target_os = "windows")]
pub fn init_win_svc() -> Result<(DockerModuleRuntime, DockerSettings), Error> {
    logging::init_win_log();
    log_banner();
    init_common().map(|(settings, _)| (DockerModuleRuntime::new(), settings))
}
