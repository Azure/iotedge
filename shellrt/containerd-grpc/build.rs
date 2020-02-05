const PREFIX: &str = "proto/github.com/containerd/containerd/api";

use std::path::PathBuf;

// NOTE: for whatever reason, the order of compilation matters!
// be careful when re-arranging things, and things might get overwritten!

fn main() -> Result<(), Box<dyn std::error::Error>> {
    // specify containerd proto packages (which all share a common path prefix)
    let containerd_protos = vec![
        // containerd.events
        vec![
            "events/container.proto",
            "events/content.proto",
            "events/namespace.proto",
            "events/snapshot.proto",
            "events/task.proto",
        ],
        // containerd.services.{}.v1
        vec!["services/containers/v1/containers.proto"],
        vec!["services/content/v1/content.proto"],
        vec!["services/diff/v1/diff.proto"],
        vec!["services/events/v1/events.proto"],
        vec!["events/image.proto", "services/images/v1/images.proto"],
        vec!["services/introspection/v1/introspection.proto"],
        vec!["services/leases/v1/leases.proto"],
        vec!["services/namespaces/v1/namespace.proto"],
        vec!["services/snapshots/v1/snapshots.proto"],
        vec!["services/tasks/v1/tasks.proto"],
        vec!["services/ttrpc/events/v1/events.proto"],
        vec!["services/version/v1/version.proto"],
        // containerd.types
        vec![
            "types/descriptor.proto",
            "types/metrics.proto",
            "types/mount.proto",
            "types/platform.proto",
            "types/task/task.proto",
        ],
    ]
    .into_iter()
    .map(|pkg| {
        pkg.into_iter()
            .map(|p| PathBuf::from(PREFIX).join(p))
            .collect::<Vec<_>>()
    })
    .collect::<Vec<_>>();

    // specify gprc protos
    let grpc_protos = vec![
        "proto/google/rpc/code.proto",
        "proto/google/rpc/error_details.proto",
        "proto/google/rpc/status.proto",
    ]
    .into_iter()
    .map(|p| PathBuf::from(p))
    .collect::<Vec<_>>();

    // collect all proto packages and compile
    let mut protos = Vec::new();
    protos.extend(containerd_protos.into_iter());
    protos.push(grpc_protos);

    for proto_pkg in protos {
        tonic_build::configure()
            .build_server(false)
            .compile(&proto_pkg, &["proto/".into()])?;
    }

    Ok(())
}
