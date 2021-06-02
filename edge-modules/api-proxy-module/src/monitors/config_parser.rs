use std::collections::HashMap;

use anyhow::{anyhow, Context, Error, Result};
use log::error;
use regex::{Captures, Regex};

use crate::token_service::token_server;

const PROXY_CONFIG_DEFAULT_VALUES: &[(&str, &str)] = &[("NGINX_DEFAULT_PORT", "443")];

pub struct ConfigParser {
    default_values: HashMap<String, String>,
    reserved_values: HashMap<String, String>,
    regex_get_variables: Regex,
    regex_get_boolean_expr: Regex,
    regex_get_if_tag: Regex,
}

impl ConfigParser {
    pub fn new() -> Result<Self, anyhow::Error> {
        //Default values will be used if user didn't provide any value through environment variable
        let mut default_values = HashMap::new();
        //reserved value cannot be changed by the user.
        let mut reserved_values = HashMap::new();

        //Set default config values
        for (key, value) in PROXY_CONFIG_DEFAULT_VALUES.iter() {
            default_values.insert((*key).to_string(), (*value).to_string());
        }

        //Harded coded keys
        // Tokens are cached by nginx. Current duration is 30mn. Make sure the the validity is not lower than the caching time.
        reserved_values.insert(
            "TOKEN_VALIDITY_MINUTES".to_string(),
            (token_server::TOKEN_VALIDITY_SECONDS / 60 / 2).to_string(),
        );
        reserved_values.insert(
            "TOKEN_SERVER_PORT".to_string(),
            token_server::TOKEN_SERVER_PORT.to_string(),
        );

        // Define regex here so regex is not compiled everytime a config is received.
        let regex_get_variables =
            Regex::new(r"\$\{(.*?)\}").context("Failed to match variables")?;

        // Find all regular expressions in if_tag and resolve them
        let regex_get_boolean_expr = Regex::new(r"boolean_expression\[(.*)\]")
            .context("Failed to match boolean statement in if_tag")?;

        // Find all $ifdef_tag 0
        let regex_get_if_tag = Regex::new(r"#if_tag 0((.|\n)*?)#endif_tag 0")
            .context("Failed to remove text between #if_tag 0 tags ")?;

        Ok(ConfigParser {
            default_values,
            reserved_values,
            regex_get_variables,
            regex_get_boolean_expr,
            regex_get_if_tag,
        })
    }

    pub fn load_config(&self, config: &str) -> Result<HashMap<String, String>, anyhow::Error> {
        let mut context = HashMap::new();

        for key in self.regex_get_variables.captures_iter(config) {
            let new_key = key[1].to_string();

            // This priority could be coded as a nested if else statement but it is hard to read.
            // Instead value are load from lowest priority to highest. Higher priority will overwrite lower.
            // 1. Load 0
            // 2. load default value if it has
            // 3. load value from customer if it has.
            // 4. load reserved value.
            // Load 0
            let new_value = self
                .reserved_values
                .get(&new_key)
                .map(std::string::ToString::to_string)
                .or_else(|| std::env::var(&new_key).ok())
                .or_else(|| {
                    self.default_values
                        .get(&new_key)
                        .map(std::string::ToString::to_string)
                })
                .unwrap_or_else(|| String::from("0"));

            context.insert(new_key, new_value);
        }

        Ok(context)
    }

    //Check readme for details of how parsing is done.
    //Get all env variables from config file and set their value to the default value or 0
    //all the environment variables are replaced by their value.
    //A second pass of replacing happens. This is to allow one level of indirection.
    //Then everything that is between #if_tag 0 and #endif_tag 0 or between  #if_tag !1 and #endif_tag !1 is removed.
    pub fn get_parsed_config(&self, config: &str) -> Result<String, anyhow::Error> {
        let context = self.load_config(config)?;

        //Do 2 passes of subst to allow one level of indirection
        let config: String =
            envsubst::substitute(config, &context).context("Failed to subst the text")?;

        // Find all regular expressions in if_tag and resolve them
        let config = self
            .regex_get_boolean_expr
            .replace_all(&config, |caps: &Captures| {
                parse_boolean_expression(&caps[1])
            });

        //Replace is 0
        let config = self.regex_get_if_tag.replace_all(&config, "").to_string();

        Ok(config)
    }
}
enum Expr {
    Value(bool),
    Operator(char),
}

fn parse_boolean_expression(expression: &str) -> String {
    let expression = replace_env_var_with_boolean(expression);
    match solve_boolean_expression(&expression) {
        Ok(x) => x,
        Err(e) => {
            error!("{}", e);
            expression
        }
    }
}

// Comma in env var name is forbidden because of that
// We use this instead of simpler regex because we cannot match overlapping regular expression with regex.
fn replace_env_var_with_boolean(expression: &str) -> String {
    let mut fifo = Vec::new();
    let mut flush_fifo = Vec::new();

    for c in expression.chars() {
        match c {
            '(' | '!' | '&' | '|' => fifo.push(c),
            ')' | ',' => {
                if flush_fifo.len() == 1 && Some('0') == flush_fifo.pop() {
                    fifo.push('0')
                } else if flush_fifo.len() > 1 {
                    fifo.push('1')
                };
                fifo.push(c);
                flush_fifo.clear();
            }
            _ => flush_fifo.push(c),
        }
    }

    fifo.iter().collect()
}

// The resolution of the regular expression is done by using a stack.
// For example: &(!(0),1,1)
// Stack fills up: Stack = ['&', '(', '!', '(','0']
// When ")" is encounter, the deepest boolean expression is solved:
// Stack result Stack = ['&', '(', '1']
// Stack fills up: Stack = ['&', '(', '1','0','0']
// When ")" is encounter, the last boolean expression is solved:
// First all the value are load in temporary stack: tmp_fifo = ['1','0','0']
// Then the operator '&' is extracted and the expression is solved
fn solve_boolean_expression(expression: &str) -> Result<String, Error> {
    let mut fifo = Vec::new();

    for c in expression.chars() {
        match c {
            '(' | ',' | ' ' => (),
            '!' | '|' | '&' => fifo.push(Expr::Operator(c)),
            '1' => fifo.push(Expr::Value(true)),
            '0' => fifo.push(Expr::Value(false)),
            ')' => {
                let mut tmp_fifo = vec![];
                while let Some(Expr::Value(v)) = fifo.last() {
                    tmp_fifo.push(*v);
                    fifo.pop();
                }

                if let Some(Expr::Operator(val)) = fifo.pop() {
                    let result = match val {
                        '!' => {
                            if tmp_fifo.len() > 1 {
                                return Err(anyhow!("Invalid ! expression {}", expression));
                            }
                            !tmp_fifo[0]
                        }
                        '|' => tmp_fifo.iter().any(|v| *v),
                        '&' => tmp_fifo.iter().all(|v| *v),
                        _ => {
                            return Err(anyhow!("Invalid boolean expression {}", expression));
                        }
                    };
                    fifo.push(Expr::Value(result));
                } else {
                    return Err(anyhow!(
                        "Could not find operator for boolean expression {}",
                        expression
                    ));
                }
            }
            _ => {
                return Err(anyhow!(
                    "Unrecognized character {} in boolean expression {}",
                    c,
                    expression
                ));
            }
        }
    }

    if fifo.len() > 1 {
        return Err(anyhow!(
            "Error when parsing boolean expression {}",
            expression
        ));
    }

    if let Some(Expr::Value(v)) = fifo.last() {
        if *v {
            Ok("1".to_string())
        } else {
            Ok("0".to_string())
        }
    } else {
        Err(anyhow!(
            "Unable to parse boolean expression, {}",
            expression
        ))
    }
}

#[cfg(test)]
mod tests {
    const RAW_CONFIG_BASE64:&str = "ZXZlbnRzIHsgfQ0KDQoNCmh0dHAgew0KICAgIHByb3h5X2J1ZmZlcnMgMzIgMTYwazsgIA0KICAgIHByb3h5X2J1ZmZlcl9zaXplIDE2MGs7DQogICAgcHJveHlfcmVhZF90aW1lb3V0IDM2MDA7DQogICAgZXJyb3JfbG9nIC9kZXYvc3Rkb3V0IGluZm87DQogICAgYWNjZXNzX2xvZyAvZGV2L3N0ZG91dDsNCg0KICAgIHNlcnZlciB7DQogICAgICAgIGxpc3RlbiAke05HSU5YX0RFRkFVTFRfUE9SVH0gc3NsIGRlZmF1bHRfc2VydmVyOw0KDQogICAgICAgIGNodW5rZWRfdHJhbnNmZXJfZW5jb2Rpbmcgb247DQoNCiAgICAgICAgc3NsX2NlcnRpZmljYXRlICAgICAgICBzZXJ2ZXIuY3J0Ow0KICAgICAgICBzc2xfY2VydGlmaWNhdGVfa2V5ICAgIHByaXZhdGVfa2V5LnBlbTsgDQogICAgICAgIHNzbF9jbGllbnRfY2VydGlmaWNhdGUgdHJ1c3RlZENBLmNydDsNCiAgICAgICAgc3NsX3ZlcmlmeV9jbGllbnQgb247DQoNCg0KICAgICAgICAjaWZfdGFnICR7TkdJTlhfSEFTX0JMT0JfTU9EVUxFfQ0KICAgICAgICBpZiAoJGh0dHBfeF9tc19ibG9iX3R5cGUgPSBCbG9ja0Jsb2IpDQogICAgICAgIHsNCiAgICAgICAgICAgIHJld3JpdGUgXiguKikkIC9zdG9yYWdlJDEgbGFzdDsNCiAgICAgICAgfSANCiAgICAgICAgI2VuZGlmX3RhZyAke05HSU5YX0hBU19CTE9CX01PRFVMRX0NCg0KICAgICAgICAjaWZfdGFnICR7RE9DS0VSX1JFUVVFU1RfUk9VVEVfQUREUkVTU30NCiAgICAgICAgbG9jYXRpb24gL3YyIHsNCiAgICAgICAgICAgIHByb3h5X2h0dHBfdmVyc2lvbiAxLjE7DQogICAgICAgICAgICByZXNvbHZlciAxMjcuMC4wLjExOw0KICAgICAgICAgICAgc2V0ICRiYWNrZW5kICJodHRwOi8vJHtET0NLRVJfUkVRVUVTVF9ST1VURV9BRERSRVNTfSI7DQogICAgICAgICAgICBwcm94eV9wYXNzICAgICAgICAgICRiYWNrZW5kOw0KICAgICAgICB9DQogICAgICAgI2VuZGlmX3RhZyAke0RPQ0tFUl9SRVFVRVNUX1JPVVRFX0FERFJFU1N9DQoNCiAgICAgICAgI2lmX3RhZyAke05HSU5YX0hBU19CTE9CX01PRFVMRX0NCiAgICAgICAgbG9jYXRpb24gfl4vc3RvcmFnZS8oLiopew0KICAgICAgICAgICAgcHJveHlfaHR0cF92ZXJzaW9uIDEuMTsNCiAgICAgICAgICAgIHJlc29sdmVyIDEyNy4wLjAuMTE7DQogICAgICAgICAgICBzZXQgJGJhY2tlbmQgImh0dHA6Ly8ke05HSU5YX0JMT0JfTU9EVUxFX05BTUVfQUREUkVTU30iOw0KICAgICAgICAgICAgcHJveHlfcGFzcyAgICAgICAgICAkYmFja2VuZC8kMSRpc19hcmdzJGFyZ3M7DQogICAgICAgIH0NCiAgICAgICAgI2VuZGlmX3RhZyAke05HSU5YX0hBU19CTE9CX01PRFVMRX0NCg0KICAgICAgICAjaWZfdGFnICR7TkdJTlhfTk9UX1JPT1R9ICAgICAgDQogICAgICAgIGxvY2F0aW9uIC97DQogICAgICAgICAgICBwcm94eV9odHRwX3ZlcnNpb24gMS4xOw0KICAgICAgICAgICAgcmVzb2x2ZXIgMTI3LjAuMC4xMTsNCiAgICAgICAgICAgIHNldCAkYmFja2VuZCAiaHR0cHM6Ly8ke0dBVEVXQVlfSE9TVE5BTUV9OjQ0MyI7DQogICAgICAgICAgICBwcm94eV9wYXNzICAgICAgICAgICRiYWNrZW5kLyQxJGlzX2FyZ3MkYXJnczsNCiAgICAgICAgfQ0KICAgICAgICAjZW5kaWZfdGFnICR7TkdJTlhfTk9UX1JPT1R9DQogICAgfQ0KfQ==";
    const RAW_CONFIG_TEXT:&str = "events { }\r\n\r\n\r\nhttp {\r\n    proxy_buffers 32 160k;  \r\n    proxy_buffer_size 160k;\r\n    proxy_read_timeout 3600;\r\n    error_log /dev/stdout info;\r\n    access_log /dev/stdout;\r\n\r\n    server {\r\n        listen ${NGINX_DEFAULT_PORT} ssl default_server;\r\n\r\n        chunked_transfer_encoding on;\r\n\r\n        ssl_certificate        server.crt;\r\n        ssl_certificate_key    private_key.pem; \r\n        ssl_client_certificate trustedCA.crt;\r\n        ssl_verify_client on;\r\n\r\n\r\n        #if_tag ${NGINX_HAS_BLOB_MODULE}\r\n        if ($http_x_ms_blob_type = BlockBlob)\r\n        {\r\n            rewrite ^(.*)$ /storage$1 last;\r\n        } \r\n        #endif_tag ${NGINX_HAS_BLOB_MODULE}\r\n\r\n        #if_tag ${DOCKER_REQUEST_ROUTE_ADDRESS}\r\n        location /v2 {\r\n            proxy_http_version 1.1;\r\n            resolver 127.0.0.11;\r\n            set $backend \"http://${DOCKER_REQUEST_ROUTE_ADDRESS}\";\r\n            proxy_pass          $backend;\r\n        }\r\n       #endif_tag ${DOCKER_REQUEST_ROUTE_ADDRESS}\r\n\r\n        #if_tag ${NGINX_HAS_BLOB_MODULE}\r\n        location ~^/storage/(.*){\r\n            proxy_http_version 1.1;\r\n            resolver 127.0.0.11;\r\n            set $backend \"http://${NGINX_BLOB_MODULE_NAME_ADDRESS}\";\r\n            proxy_pass          $backend/$1$is_args$args;\r\n        }\r\n        #endif_tag ${NGINX_HAS_BLOB_MODULE}\r\n\r\n        #if_tag ${NGINX_NOT_ROOT}      \r\n        location /{\r\n            proxy_http_version 1.1;\r\n            resolver 127.0.0.11;\r\n            set $backend \"https://${GATEWAY_HOSTNAME}:443\";\r\n            proxy_pass          $backend/$1$is_args$args;\r\n        }\r\n        #endif_tag ${NGINX_NOT_ROOT}\r\n    }\r\n}";
    const PARSED_CONFIG:&str = "events { }\r\n\r\n\r\nhttp {\r\n    proxy_buffers 32 160k;  \r\n    proxy_buffer_size 160k;\r\n    proxy_read_timeout 3600;\r\n    error_log /dev/stdout info;\r\n    access_log /dev/stdout;\r\n\r\n    server {\r\n        listen 443 ssl default_server;\r\n\r\n        chunked_transfer_encoding on;\r\n\r\n        ssl_certificate        server.crt;\r\n        ssl_certificate_key    private_key.pem; \r\n        ssl_client_certificate trustedCA.crt;\r\n        ssl_verify_client on;\r\n\r\n\r\n        \r\n\r\n        #if_tag registry:5000\r\n        location /v2 {\r\n            proxy_http_version 1.1;\r\n            resolver 127.0.0.11;\r\n            set $backend \"http://registry:5000\";\r\n            proxy_pass          $backend;\r\n        }\r\n       #endif_tag registry:5000\r\n\r\n        \r\n\r\n        \r\n    }\r\n}";
    use super::*;

    #[test]
    fn replace_env_var_with_boolean_test() {
        // Test non 0 variables are correctly replaced by 1.
        assert_eq!("|(1)", replace_env_var_with_boolean("|(10.1.23.1)"));
        assert_eq!(
            "|(1,0,1,1)",
            replace_env_var_with_boolean("|(myvariable,0,11,00)")
        );
        assert_eq!(
            "|(1,1,1)",
            replace_env_var_with_boolean("|(myvariable,myvariable,myvariable)")
        );
    }

    #[test]
    fn solve_boolean_expression_test() {
        // Test or
        assert_eq!("0", solve_boolean_expression("|(0,0,0 )").unwrap());
        assert_eq!("1", solve_boolean_expression("|(1,0,0)").unwrap());
        // Test and
        assert_eq!("0", solve_boolean_expression("&(1,0,0)").unwrap());
        assert_eq!("1", solve_boolean_expression("&(1,1,1)").unwrap());
        // Test !
        assert_eq!("1", solve_boolean_expression("&(!(0),!(0),!(0))").unwrap());

        // Test error detection
        assert_eq!(true, solve_boolean_expression("(!(0),!(0),!(0))").is_err());
        assert_eq!(true, solve_boolean_expression("&(!(0),!(0),!(0)").is_err());
        assert_eq!(
            true,
            solve_boolean_expression("&(!(asd),!(0),!(0))").is_err()
        );
        assert_eq!(true, solve_boolean_expression("!(0,0)").is_err());
    }

    #[test]
    fn parse_boolean_expression_test() {
        // Test or
        assert_eq!("1", parse_boolean_expression("|(myvariable,0,0)"));
        // Test and
        assert_eq!("0", parse_boolean_expression("&(myvariable,0,0)"));
        assert_eq!(
            "1",
            parse_boolean_expression("&(myvariable,myvariable,myvariable)")
        );
        // Test !
        assert_eq!(
            "0",
            parse_boolean_expression("|(!(myvariable),!(myvariable),!(myvariable))")
        );
    }

    #[test]
    fn env_var_tests() {
        //All environment variable tests are grouped in one test.
        //The reason is concurrency. Rust test are multi threaded by default
        //And environment variable are globals, so race condition happens.

        //**************************Check config***************************************
        std::env::set_var("NGINX_DEFAULT_PORT", "443");
        std::env::set_var("DOCKER_REQUEST_ROUTE_ADDRESS", "registry:5000");
        let config_parser = ConfigParser::new().unwrap();

        let byte_str = base64::decode(RAW_CONFIG_BASE64).unwrap();
        let config = std::str::from_utf8(&byte_str).unwrap();
        assert_eq!(config, RAW_CONFIG_TEXT);

        let config = config_parser.get_parsed_config(RAW_CONFIG_TEXT).unwrap();

        assert_eq!(&config, PARSED_CONFIG);

        std::env::remove_var("NGINX_DEFAULT_PORT");
        std::env::remove_var("DOCKER_REQUEST_ROUTE_ADDRESS");

        //************************* Check config between ![^1] get deleted *********************************
        std::env::set_var("DOCKER_REQUEST_ROUTE_ADDRESS", "IOTEDGE_PARENTHOSTNAME");
        let dummy_config = "#if_tag boolean_expression[|(!(${DOCKER_REQUEST_ROUTE_ADDRESS}),0,0)]\r\nshould be removed\r\n#endif_tag boolean_expression[|(!(${DOCKER_REQUEST_ROUTE_ADDRESS}),0,0)]\r\n#if_tag boolean_expression[&(${DOCKER_REQUEST_ROUTE_ADDRESS},1,1)]\r\nshould not be removed\r\n#endif_tag boolean_expression[&(${DOCKER_REQUEST_ROUTE_ADDRESS},1,1)]";

        let config_parser = ConfigParser::new().unwrap();
        let config = config_parser.get_parsed_config(dummy_config).unwrap();

        assert_eq!(
            "\r\n#if_tag 1\r\nshould not be removed\r\n#endif_tag 1",
            config
        );
        std::env::remove_var("DOCKER_REQUEST_ROUTE_ADDRESS");

        //********************************* Check priorities *************************************************
        // *** Lowest priority => 0
        let config_parser = ConfigParser::new().unwrap();
        let config = config_parser
            .get_parsed_config("${DUMMY_VARIABLE}")
            .unwrap();

        //Check the value is still equal to dummy value
        assert_eq!("0", config);

        // *** Default value
        let config = config_parser
            .get_parsed_config("${NGINX_DEFAULT_PORT}")
            .unwrap();

        //Check the value is still equal to dummy value
        assert_eq!("443", config);

        // *** Environment variable
        std::env::set_var("NGINX_DEFAULT_PORT", "Dummy value");
        let config_parser = ConfigParser::new().unwrap();
        let config = config_parser
            .get_parsed_config("${NGINX_DEFAULT_PORT}")
            .unwrap();

        //Check the value is still equal to dummy value
        assert_eq!("Dummy value", config);

        std::env::remove_var("NGINX_DEFAULT_PORT");

        // *** Hard coded value
        // Try to overrided hardcode value
        std::env::set_var("TOKEN_SERVER_PORT", "123");
        let config_parser = ConfigParser::new().unwrap();
        let config = config_parser
            .get_parsed_config("${TOKEN_SERVER_PORT}")
            .unwrap();

        //Check the value is still equal to dummy value
        assert_eq!(token_server::TOKEN_SERVER_PORT.to_string(), config);

        std::env::remove_var("TOKEN_SERVER_PORT");
    }
}
