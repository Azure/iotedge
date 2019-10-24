pub mod google {
    pub mod rpc {
        tonic::include_proto!("google.rpc");
    }
}

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
        pub mod containers {
            pub mod v1 {
                tonic::include_proto!("containerd.services.containers.v1");
            }
        }
        pub mod content {
            pub mod v1 {
                tonic::include_proto!("containerd.services.content.v1");
            }
        }
        pub mod diff {
            pub mod v1 {
                tonic::include_proto!("containerd.services.diff.v1");
            }
        }
        pub mod events {
            pub mod v1 {
                tonic::include_proto!("containerd.services.events.v1");
            }
            pub mod ttrpc {
                pub mod v1 {
                    tonic::include_proto!("containerd.services.events.ttrpc.v1");
                }
            }
        }
        pub mod images {
            pub mod v1 {
                tonic::include_proto!("containerd.services.images.v1");
            }
        }
        pub mod introspection {
            pub mod v1 {
                tonic::include_proto!("containerd.services.introspection.v1");
            }
        }
        pub mod leases {
            pub mod v1 {
                tonic::include_proto!("containerd.services.leases.v1");
            }
        }
        pub mod namespaces {
            pub mod v1 {
                tonic::include_proto!("containerd.services.namespaces.v1");
            }
        }
        pub mod snapshots {
            pub mod v1 {
                tonic::include_proto!("containerd.services.snapshots.v1");
            }
        }
        pub mod tasks {
            pub mod v1 {
                tonic::include_proto!("containerd.services.tasks.v1");
            }
        }
        pub mod version {
            pub mod v1 {
                tonic::include_proto!("containerd.services.version.v1");
            }
        }
    }
}
