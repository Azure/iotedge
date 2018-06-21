fn main() {
    #[cfg(windows)]
    {
        let current_dir = ::std::env::var("CARGO_MANIFEST_DIR").unwrap();
        println!(
            "cargo:rustc-link-search=all={}\\src\\resources",
            current_dir,
        );
        println!("cargo:rustc-link-lib=event_messages.res");
    }
}
