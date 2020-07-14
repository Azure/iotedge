use percent_encoding::{CONTROLS, AsciiSet};

pub const AAD_BYTES: usize = 32;
pub const AES_KEY_BYTES: usize = 32;
// pub const CONFIG_PATH: &str = "/etc/iotedged/store.toml"
pub const ENCODE_CHARS: &AsciiSet = &CONTROLS
    .add(b' ').add(b'"').add(b'<').add(b'>').add(b'`')
    .add(b'#').add(b'?').add(b'{').add(b'}');
pub const HSM_SERVER: &str = "http://localhost:8888";
pub const IV_BYTES: usize = 32;
// pub const SERVER_DIR: &str = "./";
pub const SOCKET_NAME: &str = "store.sock";

