mod system_logs;
mod system_restart;

pub use self::system_logs::SystemLogs;
pub use self::system_restart::SystemRestart;

// List of processes. Order is important, as this is the order they will be started.
pub static PROCESSES: &[&str] = &[
    "aziot-keyd",
    "aziot-certd",
    "aziot-identityd",
    "aziot-edged",
];
