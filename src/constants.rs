use percent_encoding::{CONTROLS, AsciiSet};

pub const AAD_BYTES: usize = 32;
pub const AES_KEY_BYTES: usize = 32;
pub const API_SURFACE: &str = r#"{
    "GET /": "YOU ARE HERE",
    "GET /secret": "FETCH A SECRET",
    "PUT /secret": "MODIFY A SECRET",
    "DELETE /secret": "DELETE A SECRET",
    "PUT /pull": "PULL A KEY VAULT"
}
"#;
pub const ENCODE_CHARS: &AsciiSet = &CONTROLS
    .add(b' ').add(b'"').add(b'<').add(b'>').add(b'`')
    .add(b'#').add(b'?').add(b'{').add(b'}');
pub const HSM_SERVER: &str = "http://localhost:8888";
pub const IV_BYTES: usize = 32;
pub const SERVER_DIR: &str = "./";
pub const SOCKET_NAME: &str = "store.sock";

