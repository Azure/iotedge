const PREFIX: &str = "proto/github.com/containerd/containerd/api";

use std::path::PathBuf;

fn main() -> Result<(), Box<dyn std::error::Error>> {
    let prefix = PathBuf::from(PREFIX);
    let protos = &[
        "types/platform.proto",
        "types/mount.proto",
        "types/metrics.proto",
        "types/descriptor.proto",
        "types/task/task.proto",
        "services/containers/v1/containers.proto",
        "services/content/v1/content.proto",
        "services/diff/v1/diff.proto",
        "services/events/v1/events.proto",
        "services/images/v1/images.proto",
        "services/introspection/v1/introspection.proto",
        "services/leases/v1/leases.proto",
        "services/snapshots/v1/snapshots.proto",
        "services/tasks/v1/tasks.proto",
        "services/version/v1/version.proto",
        // naming outliers
        "services/namespaces/v1/namespace.proto",
        "services/ttrpc/events/v1/events.proto",
    ]
    .iter()
    .map(|p| prefix.join(p))
    .collect::<Vec<_>>();

    tonic_build::configure()
        .build_server(false)
        .compile(protos, &["proto/".into()])?;

    Ok(())
}
