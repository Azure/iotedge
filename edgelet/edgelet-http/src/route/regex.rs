// Copyright (c) Microsoft. All rights reserved.

use std::borrow::Cow;
use std::collections::HashMap;
use std::default::Default;

use hyper::{Method, StatusCode};
use percent_encoding::percent_decode;
use regex::Regex;
use version::Version;

use super::{Builder, Handler, HandlerParamsPair, Recognizer};

pub trait IntoCaptures {
    fn into_captures(self) -> Vec<(Option<String>, String)>;
}

#[derive(Debug, PartialEq)]
pub struct Parameters {
    captures: Vec<(Option<String>, String)>,
}

impl Parameters {
    pub fn new() -> Self {
        Parameters { captures: vec![] }
    }

    pub fn with_captures<I>(captures: I) -> Self
    where
        I: IntoCaptures,
    {
        Parameters {
            captures: captures.into_captures(),
        }
    }

    pub fn name(&self, k: &str) -> Option<&str> {
        for capture in &self.captures {
            if let (Some(ref key), ref val) = *capture {
                if key == k {
                    return Some(val);
                }
            }
        }
        None
    }
}

impl IntoCaptures for Vec<(Option<String>, String)> {
    fn into_captures(self) -> Self {
        self
    }
}

impl<'a> IntoCaptures for (&'a str, &'a str) {
    fn into_captures(self) -> Vec<(Option<String>, String)> {
        vec![(Some(self.0.to_string()), self.1.to_string())]
    }
}

impl Default for Parameters {
    fn default() -> Self {
        Parameters::new()
    }
}

struct RegexRoute {
    pattern: Regex,
    handler: Box<Handler<Parameters> + Sync>,
    version: Version,
}

#[derive(Default)]
pub struct RegexRoutesBuilder {
    routes: HashMap<Method, Vec<RegexRoute>>,
}

impl Builder for RegexRoutesBuilder {
    type Recognizer = RegexRecognizer;

    fn route<S, H>(mut self, method: Method, version: Version, pattern: S, handler: H) -> Self
    where
        S: AsRef<str>,
        H: Handler<<Self::Recognizer as Recognizer>::Parameters> + Sync,
    {
        let pattern = normalize_pattern(pattern.as_ref());
        let pattern = Regex::new(&pattern).expect("failed to compile regex");
        let handler = Box::new(handler);
        self.routes
            .entry(method)
            .or_insert_with(Vec::new)
            .push(RegexRoute {
                pattern,
                handler,
                version,
            });
        self
    }

    fn finish(self) -> Self::Recognizer {
        RegexRecognizer {
            routes: self.routes,
        }
    }
}

pub struct RegexRecognizer {
    routes: HashMap<Method, Vec<RegexRoute>>,
}

impl Recognizer for RegexRecognizer {
    type Parameters = Parameters;

    fn recognize(
        &self,
        method: &Method,
        api_version: Version,
        path: &str,
    ) -> Result<HandlerParamsPair<Self::Parameters>, StatusCode> {
        let routes = self.routes.get(method).ok_or(StatusCode::NOT_FOUND)?;
        for route in routes {
            if api_version >= route.version {
                if let Some(params) = match_route(&route.pattern, path) {
                    return Ok((&*route.handler, params));
                }
            }
        }
        Err(StatusCode::NOT_FOUND)
    }
}

fn match_route(re: &Regex, path: &str) -> Option<Parameters> {
    re.captures(path).map(|cap| {
        let mut captures = Vec::with_capacity(cap.len());

        for (i, name) in re.capture_names().enumerate() {
            let val = name
                .map(|n| cap.name(n).expect("missing name"))
                .and_then(|v| percent_decode(v.as_str().as_bytes()).decode_utf8().ok())
                .map_or_else(
                    || cap.get(i).expect("missing capture").as_str().to_owned(),
                    |v| v.to_string(),
                );
            captures.push((name.map(|s| s.to_owned()), val));
        }
        Parameters { captures }
    })
}

fn normalize_pattern(pattern: &str) -> Cow<str> {
    let pattern = pattern
        .trim()
        .trim_start_matches('^')
        .trim_end_matches('$')
        .trim_end_matches('/');
    match pattern {
        "" => "^/$".into(),
        s => format!("^{}/?$", s).into(),
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn params_name() {
        let pattern = Regex::new("^/test/(?P<name>[^/]+)$").expect("failed to compile regex");
        let params = match_route(&pattern, "/test/mike").expect("failed to get params");
        assert_eq!("mike", params.name("name").unwrap());
    }

    #[test]
    fn params_no_match() {
        let pattern = Regex::new("^/test/(?P<name>[^/]+)$").expect("failed to compile regex");
        let params = match_route(&pattern, "/different/mike");
        assert_eq!(None, params);
    }

    #[test]
    fn params_missing_param() {
        let pattern = Regex::new("^/test/(?P<name>[^/]+)$").expect("failed to compile regex");
        let params = match_route(&pattern, "/test/mike").expect("failed to get params");
        assert_eq!(None, params.name("wrong-param"));
    }

    #[test]
    fn params_urldecode() {
        let pattern = Regex::new("^/test/(?P<name>[^/]+)$").expect("failed to compile regex");
        let params = match_route(&pattern, "/test/mi%2fke").expect("failed to get params");
        assert_eq!("mi/ke", params.name("name").unwrap());
    }
}
