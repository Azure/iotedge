use std::sync::Arc;

use tokio::sync::Notify;

#[derive(Debug)]
pub struct ShutdownHandle(pub Arc<Notify>);

impl ShutdownHandle {
    pub async fn shutdown(self) {
        self.0.notify_one();
    }
}
