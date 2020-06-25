pub const API_SURFACE: &str = r#"{
    "GET /": "YOU ARE HERE",
    "GET /secret": "FETCH A SECRET",
    "PUT /secret": "MODIFY A SECRET",
    "DELETE /secret": "DELETE A SECRET",
    "PUT /pull": "PULL A KEY VAULT"
}
"#;
pub const HSM_SERVER: &str = "http://0.0.0.0:8888";
pub const LOST: &str = "YOU ARE LOST";
