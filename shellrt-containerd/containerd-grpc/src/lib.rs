pub mod google {
    pub mod rpc {
        use super::super::containerd::v1::services::containers::*;
        use super::super::containerd::v1::services::content::*;
        use super::super::containerd::v1::services::diff::*;
        use super::super::containerd::v1::services::events::*;
        use super::super::containerd::v1::services::images::*;
        tonic::include_proto!("google.rpc");
    }
}

pub use containerd::*;

pub mod containerd {
    pub mod types {
        tonic::include_proto!("containerd.types");
    }

    pub mod v1 {
        pub mod types {
            tonic::include_proto!("containerd.v1.types");
        }

        pub mod services {
            use containers::*;
            use content::*;
            use diff::*;
            use events::*;
            use images::*;
            use introspection::*;
            use leases::*;
            // use namespaces::*;
            use snapshots::*;
            use tasks::*;
            use version::*;

            pub mod containers {
                // use super::*;
                tonic::include_proto!("containerd.services.containers.v1");
            }
            pub mod content {
                use super::*;
                tonic::include_proto!("containerd.services.content.v1");
            }
            pub mod diff {
                use super::*;
                tonic::include_proto!("containerd.services.diff.v1");
            }
            pub mod events {
                use super::*;
                tonic::include_proto!("containerd.services.events.v1");
            }
            // FIXME: prost codegen seems to fail with ttrpc. Could be an upsteam bug?
            // regardless, it's not too bad, since there's effectively no Rust support for
            // ttrpc anyways.
            //
            // pub mod ttrpc {
            //     use super::*;
            //     tonic::include_proto!("containerd.services.events.ttrpc.v1");
            // }
            pub mod images {
                use super::*;
                tonic::include_proto!("containerd.services.images.v1");
            }
            pub mod introspection {
                use super::*;
                tonic::include_proto!("containerd.services.introspection.v1");
            }
            pub mod leases {
                use super::*;
                tonic::include_proto!("containerd.services.leases.v1");
            }
            pub mod namespaces {
                use super::*;
                tonic::include_proto!("containerd.services.namespaces.v1");
            }
            pub mod snapshots {
                use super::*;
                tonic::include_proto!("containerd.services.snapshots.v1");
            }
            pub mod tasks {
                use super::*;
                tonic::include_proto!("containerd.services.tasks.v1");
            }
            pub mod version {
                use super::*;
                tonic::include_proto!("containerd.services.version.v1");
            }
        }
    }
}
