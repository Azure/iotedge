use http::HeaderMap;

use docker_scope::Scope;

/// A cache which associates Scopes with their corresponding auth headers.
// FIXME: this needs to be made much more efficient
#[derive(Debug)]
pub struct ScopeCache {
    map: Vec<(Scope, HeaderMap)>,
}

impl ScopeCache {
    /// Create a new, empty ScopeCache
    pub fn new() -> ScopeCache {
        ScopeCache { map: Vec::new() }
    }

    /// Check if a given scope is already present in the cache.
    pub fn get(&self, scope: &Scope) -> Option<&HeaderMap> {
        for (s, headers) in self.map.iter() {
            if s.is_superset(scope) {
                return Some(headers);
            }
        }
        None
    }

    /// Insert a new scope-headers pair into the cache.
    pub fn insert(&mut self, scope: Scope, headers: HeaderMap) {
        self.map.push((scope, headers))
    }
}
