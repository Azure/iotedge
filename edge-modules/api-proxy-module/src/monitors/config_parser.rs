use std::env;

use anyhow::{Context, Result};
use regex::Regex;

use crate::token_service::token_server;

const PROXY_CONFIG_DEFAULT_VALUES: &[(&str, &str)] = &[("NGINX_DEFAULT_PORT", "443")];

pub struct ConfigParser {
    context: std::collections::HashMap<String, String>,
}

impl ConfigParser {
    pub fn new(config: &str) -> Result<Self, anyhow::Error> {
        let mut context = std::collections::HashMap::new();
        let mut default_values = std::collections::HashMap::new();

        //Set default config values
        for (key, value) in PROXY_CONFIG_DEFAULT_VALUES.iter() {
            default_values.insert((*key).to_string(), (*value).to_string());
        }

        let re = Regex::new(r"\$\{(.*?)\}").context("Failed to match variables")?;

        for key in re.captures_iter(config) {
            let new_key = key[1].to_string();

            match std::env::var(key[1].to_string()) {
                //If user passed the variable value through env var, select it
                Ok(value) => {
                    context.insert(new_key, (*value).to_string());
                }
                //Else check if it has a default value, otherwise set to 0
                Err(_) => {
                    if let Some(val) = default_values.get(&new_key) {
                        context.insert(new_key, (*val).to_string());
                    } else {
                        context.insert(new_key, "0".to_string());
                    }
                }
            };
        }

        //Harded coded keys
        if let Ok(moduleid) = env::var("IOTEDGE_PARENTAPIPROXYNAME") {
            context.insert(
                "IOTEDGE_PARENTAPIPROXYNAME".to_string(),
                sanitize_dns_label(&moduleid),
            );
        }

        // Tokens are cached by nginx. Current duration is 30mn. Make sure the the validity is not lower than the caching time.
        context.insert(
            "TOKEN_VALIDITY_MINUTES".to_string(),
            (token_server::TOKEN_VALIDITY_SECONDS / 60 / 2).to_string(),
        );
        context.insert(
            "TOKEN_SERVER_PORT".to_string(),
            token_server::TOKEN_SERVER_PORT.to_string(),
        );

        Ok(ConfigParser { context })
    }

    pub fn get_key_value(&self, key: &str) -> Option<(String, String)> {
        if let Some(key_pair) = self.context.get_key_value(key) {
            return Some((key_pair.0.to_string(), key_pair.1.to_string()));
        }

        None
    }

    pub fn add_new_key(&mut self, key: &str, val: &str) {
        self.context.insert(key.to_string(), val.to_string());
    }

    pub fn remove_key(&mut self, key: &str) {
        self.context.remove(key);
    }

    pub fn clear(&mut self) {
        self.context.clear();
    }

    //Check readme for details of how parsing is done.
    //First all the environment variables are replaced by their value.
    //Only environment variables in the list NGINX_CONFIG_ENV_VAR_LIST are replaced.
    //A second pass of replacing happens. This is to allow one level of indirection.
    //Then everything that is between #if_tag 0 and #endif_tag 0 or between  #if_tag !1 and #endif_tag !1 is removed.
    pub fn get_parsed_config(&self, config: &str) -> Result<String, anyhow::Error> {
        //Do 2 passes of subst to allow one level of indirection
        let config: String =
            envsubst::substitute(config, &self.context).context("Failed to subst the text")?;

        //Replace is 0
        let re = Regex::new(r"#if_tag 0((.|\n)*?)#endif_tag 0")
            .context("Failed to remove text between #if_tag 0 tags ")?;
        let config = re.replace_all(&config, "").to_string();

        //Or not 1. This allows usage of if ... else ....
        let re = Regex::new(r"#if_tag ![^0]((.|\n)*?)#endif_tag [^0].*?\n")
            .context("Failed to remove text between #if_tag 0 tags ")?;
        let config = re.replace_all(&config, "").to_string();

        Ok(config)
    }
}

const ALLOWED_CHAR_DNS: char = '-';
const DNS_MAX_SIZE: usize = 63;

// The name returned from here must conform to following rules (as per RFC 1035):
//  - length must be <= 63 characters
//  - must be all lower case alphanumeric characters or '-'
//  - must start with an alphabet
//  - must end with an alphanumeric character
pub fn sanitize_dns_label(name: &str) -> String {
    name.trim_start_matches(|c: char| !c.is_ascii_alphabetic())
        .trim_end_matches(|c: char| !c.is_ascii_alphanumeric())
        .to_lowercase()
        .chars()
        .filter(|c| c.is_ascii_alphanumeric() || c == &ALLOWED_CHAR_DNS)
        .take(DNS_MAX_SIZE)
        .collect::<String>()
}

#[cfg(test)]
mod tests {
    const RAW_CONFIG_BASE64:&str = "ZXZlbnRzIHsgfQ0KDQoNCmh0dHAgew0KICAgIHByb3h5X2J1ZmZlcnMgMzIgMTYwazsgIA0KICAgIHByb3h5X2J1ZmZlcl9zaXplIDE2MGs7DQogICAgcHJveHlfcmVhZF90aW1lb3V0IDM2MDA7DQogICAgZXJyb3JfbG9nIC9kZXYvc3Rkb3V0IGluZm87DQogICAgYWNjZXNzX2xvZyAvZGV2L3N0ZG91dDsNCg0KICAgIHNlcnZlciB7DQogICAgICAgIGxpc3RlbiAke05HSU5YX0RFRkFVTFRfUE9SVH0gc3NsIGRlZmF1bHRfc2VydmVyOw0KDQogICAgICAgIGNodW5rZWRfdHJhbnNmZXJfZW5jb2Rpbmcgb247DQoNCiAgICAgICAgc3NsX2NlcnRpZmljYXRlICAgICAgICBzZXJ2ZXIuY3J0Ow0KICAgICAgICBzc2xfY2VydGlmaWNhdGVfa2V5ICAgIHByaXZhdGVfa2V5LnBlbTsgDQogICAgICAgIHNzbF9jbGllbnRfY2VydGlmaWNhdGUgdHJ1c3RlZENBLmNydDsNCiAgICAgICAgc3NsX3ZlcmlmeV9jbGllbnQgb247DQoNCg0KICAgICAgICAjaWZfdGFnICR7TkdJTlhfSEFTX0JMT0JfTU9EVUxFfQ0KICAgICAgICBpZiAoJGh0dHBfeF9tc19ibG9iX3R5cGUgPSBCbG9ja0Jsb2IpDQogICAgICAgIHsNCiAgICAgICAgICAgIHJld3JpdGUgXiguKikkIC9zdG9yYWdlJDEgbGFzdDsNCiAgICAgICAgfSANCiAgICAgICAgI2VuZGlmX3RhZyAke05HSU5YX0hBU19CTE9CX01PRFVMRX0NCg0KICAgICAgICAjaWZfdGFnICR7RE9DS0VSX1JFUVVFU1RfUk9VVEVfQUREUkVTU30NCiAgICAgICAgbG9jYXRpb24gL3YyIHsNCiAgICAgICAgICAgIHByb3h5X2h0dHBfdmVyc2lvbiAxLjE7DQogICAgICAgICAgICByZXNvbHZlciAxMjcuMC4wLjExOw0KICAgICAgICAgICAgc2V0ICRiYWNrZW5kICJodHRwOi8vJHtET0NLRVJfUkVRVUVTVF9ST1VURV9BRERSRVNTfSI7DQogICAgICAgICAgICBwcm94eV9wYXNzICAgICAgICAgICRiYWNrZW5kOw0KICAgICAgICB9DQogICAgICAgI2VuZGlmX3RhZyAke0RPQ0tFUl9SRVFVRVNUX1JPVVRFX0FERFJFU1N9DQoNCiAgICAgICAgI2lmX3RhZyAke05HSU5YX0hBU19CTE9CX01PRFVMRX0NCiAgICAgICAgbG9jYXRpb24gfl4vc3RvcmFnZS8oLiopew0KICAgICAgICAgICAgcHJveHlfaHR0cF92ZXJzaW9uIDEuMTsNCiAgICAgICAgICAgIHJlc29sdmVyIDEyNy4wLjAuMTE7DQogICAgICAgICAgICBzZXQgJGJhY2tlbmQgImh0dHA6Ly8ke05HSU5YX0JMT0JfTU9EVUxFX05BTUVfQUREUkVTU30iOw0KICAgICAgICAgICAgcHJveHlfcGFzcyAgICAgICAgICAkYmFja2VuZC8kMSRpc19hcmdzJGFyZ3M7DQogICAgICAgIH0NCiAgICAgICAgI2VuZGlmX3RhZyAke05HSU5YX0hBU19CTE9CX01PRFVMRX0NCg0KICAgICAgICAjaWZfdGFnICR7TkdJTlhfTk9UX1JPT1R9ICAgICAgDQogICAgICAgIGxvY2F0aW9uIC97DQogICAgICAgICAgICBwcm94eV9odHRwX3ZlcnNpb24gMS4xOw0KICAgICAgICAgICAgcmVzb2x2ZXIgMTI3LjAuMC4xMTsNCiAgICAgICAgICAgIHNldCAkYmFja2VuZCAiaHR0cHM6Ly8ke0dBVEVXQVlfSE9TVE5BTUV9OjQ0MyI7DQogICAgICAgICAgICBwcm94eV9wYXNzICAgICAgICAgICRiYWNrZW5kLyQxJGlzX2FyZ3MkYXJnczsNCiAgICAgICAgfQ0KICAgICAgICAjZW5kaWZfdGFnICR7TkdJTlhfTk9UX1JPT1R9DQogICAgfQ0KfQ==";
    const RAW_CONFIG_TEXT:&str = "events { }\r\n\r\n\r\nhttp {\r\n    proxy_buffers 32 160k;  \r\n    proxy_buffer_size 160k;\r\n    proxy_read_timeout 3600;\r\n    error_log /dev/stdout info;\r\n    access_log /dev/stdout;\r\n\r\n    server {\r\n        listen ${NGINX_DEFAULT_PORT} ssl default_server;\r\n\r\n        chunked_transfer_encoding on;\r\n\r\n        ssl_certificate        server.crt;\r\n        ssl_certificate_key    private_key.pem; \r\n        ssl_client_certificate trustedCA.crt;\r\n        ssl_verify_client on;\r\n\r\n\r\n        #if_tag ${NGINX_HAS_BLOB_MODULE}\r\n        if ($http_x_ms_blob_type = BlockBlob)\r\n        {\r\n            rewrite ^(.*)$ /storage$1 last;\r\n        } \r\n        #endif_tag ${NGINX_HAS_BLOB_MODULE}\r\n\r\n        #if_tag ${DOCKER_REQUEST_ROUTE_ADDRESS}\r\n        location /v2 {\r\n            proxy_http_version 1.1;\r\n            resolver 127.0.0.11;\r\n            set $backend \"http://${DOCKER_REQUEST_ROUTE_ADDRESS}\";\r\n            proxy_pass          $backend;\r\n        }\r\n       #endif_tag ${DOCKER_REQUEST_ROUTE_ADDRESS}\r\n\r\n        #if_tag ${NGINX_HAS_BLOB_MODULE}\r\n        location ~^/storage/(.*){\r\n            proxy_http_version 1.1;\r\n            resolver 127.0.0.11;\r\n            set $backend \"http://${NGINX_BLOB_MODULE_NAME_ADDRESS}\";\r\n            proxy_pass          $backend/$1$is_args$args;\r\n        }\r\n        #endif_tag ${NGINX_HAS_BLOB_MODULE}\r\n\r\n        #if_tag ${NGINX_NOT_ROOT}      \r\n        location /{\r\n            proxy_http_version 1.1;\r\n            resolver 127.0.0.11;\r\n            set $backend \"https://${GATEWAY_HOSTNAME}:443\";\r\n            proxy_pass          $backend/$1$is_args$args;\r\n        }\r\n        #endif_tag ${NGINX_NOT_ROOT}\r\n    }\r\n}";
    const PARSED_CONFIG:&str = "events { }\r\n\r\n\r\nhttp {\r\n    proxy_buffers 32 160k;  \r\n    proxy_buffer_size 160k;\r\n    proxy_read_timeout 3600;\r\n    error_log /dev/stdout info;\r\n    access_log /dev/stdout;\r\n\r\n    server {\r\n        listen 443 ssl default_server;\r\n\r\n        chunked_transfer_encoding on;\r\n\r\n        ssl_certificate        server.crt;\r\n        ssl_certificate_key    private_key.pem; \r\n        ssl_client_certificate trustedCA.crt;\r\n        ssl_verify_client on;\r\n\r\n\r\n        \r\n\r\n        #if_tag registry:5000\r\n        location /v2 {\r\n            proxy_http_version 1.1;\r\n            resolver 127.0.0.11;\r\n            set $backend \"http://registry:5000\";\r\n            proxy_pass          $backend;\r\n        }\r\n       #endif_tag registry:5000\r\n\r\n        \r\n\r\n        \r\n    }\r\n}";
    use super::*;

    #[test]
    fn env_var_tests() {
        //All environment variable tests are grouped in one test.
        //The reason is concurrency. Rust test are multi threaded by default
        //And environment variable are globals, so race condition happens.

        //**************************Check config***************************************
        std::env::set_var("NGINX_DEFAULT_PORT", "443");
        std::env::set_var("DOCKER_REQUEST_ROUTE_ADDRESS", "registry:5000");
        let config_parser = ConfigParser::new(RAW_CONFIG_TEXT).unwrap();

        let byte_str = base64::decode(RAW_CONFIG_BASE64).unwrap();
        let config = std::str::from_utf8(&byte_str).unwrap();
        assert_eq!(config, RAW_CONFIG_TEXT);

        let config = config_parser.get_parsed_config(RAW_CONFIG_TEXT).unwrap();

        assert_eq!(&config, PARSED_CONFIG);

        std::env::remove_var("NGINX_DEFAULT_PORT");
        std::env::remove_var("DOCKER_REQUEST_ROUTE_ADDRESS");

        //**************************Check defaults variables set***************************************
        let config_parser =
            ConfigParser::new("${NGINX_DEFAULT_PORT}   ${IOTEDGE_PARENTAPIPROXYNAME}").unwrap();

        for (key, value) in PROXY_CONFIG_DEFAULT_VALUES.iter() {
            let var = config_parser.get_key_value(key).unwrap().1;
            assert_eq!(*value, &var);
        }

        //******************Check the the default function doesn't override user variable***************
        //put dummy value in a an env var that has a default;
        std::env::set_var("NGINX_DEFAULT_PORT", "Dummy value");
        let config_parser = ConfigParser::new(RAW_CONFIG_TEXT).unwrap();

        let var = config_parser.get_key_value("NGINX_DEFAULT_PORT").unwrap().1;
        //Check the value is still equal to dummy value
        assert_eq!("Dummy value", &var);

        std::env::remove_var("NGINX_DEFAULT_PORT");

        //************************* Check config between ![^1] get deleted *********************************
        std::env::set_var("DOCKER_REQUEST_ROUTE_ADDRESS", "IOTEDGE_PARENTHOSTNAME");
        let dummy_config = "#if_tag !${DOCKER_REQUEST_ROUTE_ADDRESS}\r\nshould be removed\r\n#endif_tag ${DOCKER_REQUEST_ROUTE_ADDRESS}\r\n\r\n#if_tag ${DOCKER_REQUEST_ROUTE_ADDRESS}\r\nshould not be removed\r\n#endif_tag ${DOCKER_REQUEST_ROUTE_ADDRESS}";

        let config_parser = ConfigParser::new(RAW_CONFIG_TEXT).unwrap();
        let config = config_parser.get_parsed_config(dummy_config).unwrap();

        assert_eq!("\r\n#if_tag IOTEDGE_PARENTHOSTNAME\r\nshould not be removed\r\n#endif_tag IOTEDGE_PARENTHOSTNAME", config);
        std::env::remove_var("DOCKER_REQUEST_ROUTE_ADDRESS");

        //*************************** Check IOTEDGE_PARENTAPIPROXYNAME get sanitized *******************
        std::env::set_var("IOTEDGE_PARENTAPIPROXYNAME", "iotedge_api_proxy");

        let config_parser = ConfigParser::new(RAW_CONFIG_TEXT).unwrap();

        let dummy_config = "${IOTEDGE_PARENTAPIPROXYNAME}";

        let config = config_parser.get_parsed_config(dummy_config).unwrap();

        assert_eq!("iotedgeapiproxy", config);
        std::env::remove_var("IOTEDGE_PARENTAPIPROXYNAME");
    }
}
