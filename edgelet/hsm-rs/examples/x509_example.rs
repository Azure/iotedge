// Copyright (c) Microsoft. All rights reserved.
extern crate hsm;

use hsm::{GetCerts, HsmX509};

fn main() {
    let hsm_x509 = HsmX509::new();
    println!("common name = {}", hsm_x509.get_common_name().unwrap());
}
