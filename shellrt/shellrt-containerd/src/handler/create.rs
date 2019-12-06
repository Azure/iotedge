use std::collections::HashMap;

use lazy_static::lazy_static;
use serde::{Deserialize, Serialize};

use containerd_grpc::containerd::services::content::v1::client::ContentClient;
use containrs::{Digest, Reference};
use cri_grpc::{
    client::RuntimeServiceClient, ContainerConfig, ContainerMetadata, CreateContainerRequest,
    ImageSpec, KeyValue, ListPodSandboxRequest, PodSandboxConfig, PodSandboxMetadata,
    RunPodSandboxRequest,
};
use shellrt_api::v0::{request, response};

use crate::error::*;

lazy_static! {
    // corresponds to "k8s.gcr.io/pause:3.1"
    static ref PAUSE_IMAGE_DIGEST: Digest =
        "sha256:59eec8837a4d942cc19a52b8c09ea75121acc38114a2c68b98983ce9356b8610"
            .parse::<Digest>()
            .unwrap();
}

// TODO: move containerd-cri specific structs into their own crate (to share
// between clients and plugins)
#[derive(Debug, Serialize, Deserialize)]
struct CreateConfig {
    image: String,
}

pub struct CreateHandler {
    grpc_uri: String,
}

impl CreateHandler {
    pub fn new(grpc_uri: String) -> CreateHandler {
        CreateHandler { grpc_uri }
    }

    pub async fn handle(
        self,
        req: request::Create,
    ) -> Result<(response::Create, Option<crate::ResponseThunk>)> {
        let request::Create {
            name, config, env, ..
        } = req;

        let (content_client, mut cri_client) = (
            ContentClient::connect(self.grpc_uri.clone())
                .await
                .context(ErrorKind::GrpcConnect)?,
            RuntimeServiceClient::connect(self.grpc_uri.clone())
                .await
                .context(ErrorKind::GrpcConnect)?,
        );

        // prereq: the "k8s.gcr.io/pause:3.1" image must exist. If it doesn't,
        // containerd-cri will silently download it, sidestepping containrs entirely.
        //
        // TODO?: if pause image is missing, ingest the pause image from a local .tar?
        // TODO?: if pause image is missing, add config option to specify download url?
        if !super::img_pull::already_in_containerd(content_client, &PAUSE_IMAGE_DIGEST).await? {
            return Err(Error::new(ErrorKind::MissingPauseImage.into()));
        }

        // create a new pod sandbox for the container
        let pod_sandbox_config = PodSandboxConfig {
            // containerd-cri only uses these value to generate a unique sandbox name (by
            // concatenating the fields into a string with '_' as a delimiter).
            metadata: Some(PodSandboxMetadata {
                // arbitrarily chosen values
                name: name.clone(),
                namespace: "".to_string(),
                attempt: 0,
                uid: "".to_string(),
            }),
            // TODO: revisit `log_directory` handling when implementing log functionality
            log_directory: "/tmp".to_string(),
            labels: HashMap::new(),
            annotations: HashMap::new(),
            linux: None,
            // TODO: revisit RunPodSandboxRequest when implementing networking
            hostname: "".to_string(),
            dns_config: None,
            port_mappings: Vec::new(),
        };

        let res = cri_client
            .run_pod_sandbox(RunPodSandboxRequest {
                runtime_handler: String::new(),
                config: Some(pod_sandbox_config.clone()),
            })
            .await;

        let pod_sandbox_id = match res {
            Ok(res) => res.into_inner().pod_sandbox_id,
            Err(e) => {
                // the pod_sandbox might already exist.
                // TODO?: return error if pod_sandbox already exists?
                let pods = cri_client
                    .list_pod_sandbox(ListPodSandboxRequest { filter: None })
                    .await
                    .context(ErrorKind::GrpcUnexpectedErr)?
                    .into_inner()
                    .items;
                let pod = pods
                    .into_iter()
                    .find(|pod| pod.metadata.as_ref().unwrap().name == name);
                match pod {
                    Some(pod) => pod.id,
                    // if it doesn't, then something really odd is happening
                    None => return Err(e.context(ErrorKind::GrpcUnexpectedErr).into()),
                }
            }
        };

        // parse containerd-cri specific config data
        let config = serde_json::from_value::<CreateConfig>(config)
            .context(ErrorKind::MalformedCreateConfig)?;

        let image = config
            .image
            .parse::<Reference>()
            .context(ErrorKind::MalformedReference)?;

        let image = if image.registry() == "registry-1.docker.io" {
            // for some reason, containerd-cri tags images downloaded from
            // "registry-1.docker.io" as being pulled from "docker.io".
            let mut raw_image = image.clone().into_raw_reference();
            // cannot panic, since docker.io is a valid domain string
            raw_image.set_domain(Some("docker.io")).unwrap();
            raw_image.canonicalize().to_string()
        } else {
            image.to_string()
        };

        // prereq: the specified image must exist.
        // TODO: check if image exists before create_container (for a better err msg)

        let res = cri_client
            .create_container(CreateContainerRequest {
                pod_sandbox_id,
                config: Some(ContainerConfig {
                    metadata: Some(ContainerMetadata {
                        name: pod_sandbox_config.metadata.as_ref().unwrap().name.clone(),
                        attempt: 0,
                    }),
                    image: Some(ImageSpec { image }),
                    // TODO: revisit `log_path` handling when implementing log functionality
                    log_path: format!("{}.log", name.clone()),
                    // XXX: remove placeholder values in the create handler!
                    command: vec!["/bin/bash".to_string(), "-c".to_string(), "--".to_string()],
                    args: vec!["while true; do sleep 1; echo boop; done;".to_string()],
                    working_dir: "".to_string(),
                    envs: env
                        .into_iter()
                        .map(|(key, value)| KeyValue { key, value })
                        .collect(),
                    mounts: Vec::new(),
                    devices: Vec::new(),
                    labels: HashMap::new(),
                    annotations: HashMap::new(),
                    linux: None,
                    windows: None,
                    // interactive container vars
                    stdin: false,
                    stdin_once: false,
                    tty: false,
                }),
                sandbox_config: Some(pod_sandbox_config.clone()),
            })
            .await;

        let _container_id = match res {
            Ok(res) => res.into_inner().container_id,
            Err(e) => return Err(e.context(ErrorKind::GrpcUnexpectedErr).into()),
        };

        Ok((response::Create {}, None))
    }
}
