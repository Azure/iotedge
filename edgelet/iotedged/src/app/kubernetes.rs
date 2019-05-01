use edgelet_docker::Settings as DockerSettings;
use edgelet_kube::KubeModuleRuntime;

use super::{init_common, log_banner};
use error::Error;
use logging;

pub fn init() -> Result<(KubeModuleRuntime, DockerSettings), Error> {
    logging::init();
    log_banner();
    let (settings, _) = init_common()?;

    Ok((KubeModuleRuntime::new(), settings))
}
