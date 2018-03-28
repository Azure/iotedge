// Copyright (c) Microsoft. All rights reserved.

extern crate tower_grpc_build;

fn main() {
    println!("hello");
    // Build helloworld
    let result = tower_grpc_build::Config::new()
        .enable_server(true)
        .enable_client(true)
        .build(
            &["proto/calculator/calculator.proto"],
            &["proto/calculator"],
        );
    println!("result {:?}", result);
}
