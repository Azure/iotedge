use async_trait::async_trait;

#[async_trait]
pub trait Sidecar {
    type ShutdownHandle: SidecarShutdownHandle;
    fn run(&self) -> Self::ShutdownHandle;
}

pub trait SidecarShutdownHandle {
    fn shutdown(&self);
    fn wait_for_shutdown(&self);
}
