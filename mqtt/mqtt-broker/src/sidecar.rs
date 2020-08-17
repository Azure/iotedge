trait Sidecar {
    fn run() -> Self;
    fn shutdown();
    fn wait_for_shutdown();
}
