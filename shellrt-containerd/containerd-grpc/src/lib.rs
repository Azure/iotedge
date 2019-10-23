pub mod google {
    pub mod rpc {
        use super::super::containerd::services::containers::v1::*;
        use super::super::containerd::services::content::v1::*;
        use super::super::containerd::services::diff::v1::*;
        use super::super::containerd::services::events::v1::*;
        use super::super::containerd::services::images::v1::*;
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
    }

    pub mod services {
        pub use containers::v1::*;
        pub use content::v1::*;
        pub use diff::v1::*;
        pub use events::v1::*;
        pub use images::v1::*;
        pub use introspection::v1::*;
        pub use leases::v1::*;
        pub use snapshots::v1::*;
        pub use tasks::v1::*;
        pub use version::v1::*;

        pub mod containers {
            // use super::*;
            pub mod v1 {
                // use super::*;
                tonic::include_proto!("containerd.services.containers.v1");
            }
        }
        pub mod content {
            use super::*;
            pub mod v1 {
                use super::*;
                tonic::include_proto!("containerd.services.content.v1");
            }
        }
        pub mod diff {
            use super::*;
            pub mod v1 {
                use super::*;
                tonic::include_proto!("containerd.services.diff.v1");
            }
        }
        pub mod events {
            use super::*;
            pub mod v1 {
                use super::*;
                tonic::include_proto!("containerd.services.events.v1");
            }
        }
        // FIXME: prost codegen seems to fail with ttrpc. Could be an upsteam bug?
        // regardless, it's not too bad, since there's effectively no Rust support for
        // ttrpc anyways.
        pub mod images {
            use super::*;
            pub mod v1 {
                use super::*;
                tonic::include_proto!("containerd.services.images.v1");
            }
        }
        pub mod introspection {
            use super::*;
            pub mod v1 {
                use super::*;
                tonic::include_proto!("containerd.services.introspection.v1");
            }
        }
        pub mod leases {
            use super::*;
            pub mod v1 {
                use super::*;
                tonic::include_proto!("containerd.services.leases.v1");
            }
        }
        pub mod namespaces {
            use super::*;
            pub mod v1 {
                use super::*;
                tonic::include_proto!("containerd.services.namespaces.v1");
            }
        }
        pub mod snapshots {
            use super::*;
            pub mod v1 {
                use super::*;
                tonic::include_proto!("containerd.services.snapshots.v1");
            }
        }
        pub mod tasks {
            use super::*;
            pub mod v1 {
                use super::*;
                tonic::include_proto!("containerd.services.tasks.v1");
            }
        }
        pub mod version {
            use super::*;
            pub mod v1 {
                use super::*;
                tonic::include_proto!("containerd.services.version.v1");
            }
        }
    }
}
