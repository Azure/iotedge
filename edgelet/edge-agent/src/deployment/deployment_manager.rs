use std::path::{Path, PathBuf};

use std::error::Error;

use tokio::fs::File;
use tokio::io::{AsyncReadExt, AsyncWriteExt};
use tokio::sync::mpsc;

use super::deployment::Deployment;

struct DeploymentManager {
    notifier: mpsc::Sender<()>,
    file_location: PathBuf,
}

impl DeploymentManager {
    pub fn new(notifier: mpsc::Sender<()>, storage_location: impl AsRef<Path>) -> Self {
        Self {
            notifier,
            file_location: storage_location.as_ref().join("deployment.json"),
        }
    }

    pub async fn update_deployment(&self, deployment: &str) -> Result<(), Box<dyn Error>> {
        let deployment: Deployment = serde_json::from_str(deployment)?;
        println!("Received deployment version {}", deployment.version);
        Self::validate_deployment(&deployment)?;

        let deployment = serde_json::to_string(&deployment)?;
        File::create(&self.file_location)
            .await?
            .write_all(deployment.as_bytes())
            .await?;

        self.notifier.send(()).await?;
        Ok(())
    }

    pub async fn get_deployment(&self) -> Result<Deployment, Box<dyn Error>> {
        let mut file = File::open(&self.file_location).await?;
        let mut contents = vec![];
        file.read_to_end(&mut contents).await?;
        let deployment = serde_json::from_slice(&contents)?;

        Self::validate_deployment(&deployment)?;
        Ok(deployment)
    }

    fn validate_deployment(_deployment: &Deployment) -> Result<(), Box<dyn Error>> {
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use rand::Rng;
    use tempfile::tempdir;
    use tokio::select;

    #[tokio::test]
    async fn test_writing() {
        let test_file = std::path::Path::new(concat!(
            env!("CARGO_MANIFEST_DIR"),
            "/src/deployment/test/twin1.json"
        ));
        let tmp_dir = tempdir().unwrap();

        let (tx, mut rx) = mpsc::channel(32);
        let manager = DeploymentManager::new(tx, tmp_dir.path());

        // Deploy normally and validate file
        let test_deployment = read_file(test_file).await;
        manager
            .update_deployment(&test_deployment)
            .await
            .expect("Update Deployment is able to parse and write deployment");
        manager
            .get_deployment()
            .await
            .expect("Written Deployment is Valid");

        // Correctly overwrites bad deployment
        let mut rng = rand::thread_rng();
        let noise: Vec<u8> = (0..100000).map(|_| rng.gen()).collect();
        let deployment_file = tmp_dir.path().join("deployment.json");
        File::create(&deployment_file)
            .await
            .unwrap()
            .write_all(&noise)
            .await
            .unwrap();

        manager
            .update_deployment(&test_deployment)
            .await
            .expect("Update Deployment is able to parse and write deployment");
        manager
            .get_deployment()
            .await
            .expect("Written Deployment is Valid");
    }

    #[tokio::test]
    async fn test_parsing() {
        let test_files_directory =
            std::path::Path::new(concat!(env!("CARGO_MANIFEST_DIR"), "/src/deployment/test"));
        let tmp_dir = tempdir().unwrap();

        let (tx, mut rx) = mpsc::channel(32);
        let manager = DeploymentManager::new(tx, tmp_dir.path());

        for test_file in std::fs::read_dir(test_files_directory).unwrap() {
            let test_file = test_file.unwrap();
            if test_file.file_type().unwrap().is_dir() {
                continue;
            }
            let test_deployment = read_file(test_file.path()).await;

            manager
                .update_deployment(&test_deployment)
                .await
                .expect("Update Deployment is able to parse and write deployment");
            assert_eq!(
                check_receiver(&mut rx).await,
                Some(()),
                "Updated deployment sends notification"
            );
            assert_eq!(
                check_receiver(&mut rx).await,
                None,
                "Only 1 notification should be sent"
            );
        }

        // let
    }

    // There might be a better way to do this, I don't know it
    async fn check_receiver<T>(receiver: &mut mpsc::Receiver<T>) -> Option<T> {
        select! {
            () = tokio::time::sleep(std::time::Duration::from_millis(5)) => None,
            val = receiver.recv() => val,
        }
    }

    async fn read_file(path: impl AsRef<Path>) -> String {
        let err_msg = format!("Missing file {:#?}", path.as_ref());
        let mut file = File::open(path).await.expect(&err_msg);
        let mut contents = vec![];
        file.read_to_end(&mut contents).await.unwrap();

        String::from_utf8(contents).unwrap()
    }
}
