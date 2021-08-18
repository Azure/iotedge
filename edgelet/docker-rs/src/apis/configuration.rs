use super::ApiError;
use hyper::Uri;
use std::error::Error;

pub struct Configuration {
    pub base_path: String,
    pub user_agent: Option<String>,
    pub uri_composer: Box<dyn Fn(&str, &str) -> Result<Uri, Box<dyn Error>> + Send + Sync>,
}

impl Default for Configuration {
    fn default() -> Self {
        Configuration {
            base_path: "http://localhost/v1.34".to_owned(),
            user_agent: Some("edgelet/0.1.0".to_owned()),
            uri_composer: Box::new(|base_path, path| {
                format!("{}{}", base_path, path)
                    .parse()
                    .map_err(|e| ApiError::with_message(format!("Url parse error: {}", e)).into())
            }),
        }
    }
}
