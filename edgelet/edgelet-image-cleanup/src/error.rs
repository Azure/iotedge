#[derive(Debug, thiserror::Error)]
pub enum ImageCleanupError {
    #[error("image id was unexpectedly found to be empty")]
    GetImageId(),

    #[error("error while trying to get running modules: {0:?}")]
    ListRunningModules(#[source] anyhow::Error),

    #[error("failed to prune images from file: {0:?}")]
    PruneImages(#[source] edgelet_docker::Error),

    #[error("failed to list images: {0:?}")]
    ListImages(#[source] anyhow::Error),

    #[error("invalid configuration: {0:?}")]
    InvalidConfiguration(String),
}
