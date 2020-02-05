//! Extended from github.com/docker/distribution/reference/reference_test.go

use docker_reference::*;

mod common;
use common::ExpectedRawReference;

// TODO: look into using `proptest` to iron-out parsing issues

#[test]
fn bare() {
    assert_eq!(
        "test_com".parse::<RawReference>().unwrap(),
        ExpectedRawReference {
            path: "test_com".to_string(),
            domain: None,
            tag: None,
            digest: None
        }
    );
}

#[test]
fn tag() {
    assert_eq!(
        "test_com:tag".parse::<RawReference>().unwrap(),
        ExpectedRawReference {
            path: "test_com".to_string(),
            domain: None,
            tag: Some("tag".to_string()),
            digest: None
        }
    );
}

#[test]
fn url_ish() {
    assert_eq!(
        "test.com:5000".parse::<RawReference>().unwrap(),
        ExpectedRawReference {
            path: "test.com".to_string(),
            domain: None,
            tag: Some("5000".to_string()),
            digest: None
        }
    );
}

#[test]
fn domain_no_dot_port() {
    assert_eq!(
        "test:5000/repo".parse::<RawReference>().unwrap(),
        ExpectedRawReference {
            path: "repo".to_string(),
            domain: Some("test:5000".to_string()),
            tag: None,
            digest: None
        }
    );
}

#[test]
fn domain_tag() {
    assert_eq!(
        "test.com/repo:tag".parse::<RawReference>().unwrap(),
        ExpectedRawReference {
            path: "repo".to_string(),
            domain: Some("test.com".to_string()),
            tag: Some("tag".to_string()),
            digest: None
        }
    );
}

#[test]
fn domain_port() {
    assert_eq!(
        "test.com:5000/repo".parse::<RawReference>().unwrap(),
        ExpectedRawReference {
            path: "repo".to_string(),
            domain: Some("test.com:5000".to_string()),
            tag: None,
            digest: None
        }
    );
}

#[test]
fn domain_port_tag() {
    assert_eq!(
        "test.com:5000/repo:tag".parse::<RawReference>().unwrap(),
        ExpectedRawReference {
            path: "repo".to_string(),
            domain: Some("test.com:5000".to_string()),
            tag: Some("tag".to_string()),
            digest: None
        }
    );
}

#[test]
fn domain_port_digest() {
    assert_eq!(
        "test:5000/repo@sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"
            .parse::<RawReference>()
            .unwrap(),
        ExpectedRawReference {
            path: "repo".to_string(),
            domain: Some("test:5000".to_string()),
            tag: None,
            digest: Some(
                "sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"
                    .parse()
                    .unwrap()
            )
        }
    );
}

#[test]
fn domain_port_tag_digest() {
    assert_eq!(
        "test:5000/repo:tag@sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff".parse::<RawReference>().unwrap(),
        ExpectedRawReference {
            path: "repo".to_string(),
            domain: Some("test:5000".to_string()),
            tag: Some("tag".to_string()),
            digest: Some("sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff".parse().unwrap())
        }
    );
}

#[test]
fn empty() {
    assert!("".parse::<RawReference>().is_err());
}

#[test]
fn just_tag() {
    assert!(":justtag".parse::<RawReference>().is_err());
}

#[test]
fn just_digest() {
    assert!(
        "@sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"
            .parse::<RawReference>()
            .is_err()
    );
}

#[test]
fn short_digest() {
    assert!("@sha256:fffffffffffffffff".parse::<RawReference>().is_err());
}

// Passes, as even though it's not a registered digest, it conforms to the
// digest grammar.
#[test]
fn unsupported_digest() {
    assert!(
        "validname@bruhhash:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"
            .parse::<RawReference>()
            .is_ok()
    );
}

#[test]
fn uppercase_name() {
    assert!("Uppercase:tag".parse::<RawReference>().is_err());
}

// FIXME "Uppercase" is incorrectly handled as a domain-name here, therefore
// passes. See https://github.com/docker/distribution/pull/1778, and https://github.com/docker/docker/pull/20175
// #[test]
// fn uppercase_with_slash_tag() {
//     assert!("Uppercase/lowercase:tag".parse::<RawReference>().is_err());
// }

#[test]
fn domain_uppercase_name() {
    assert!("test:5000/Uppercase/lowercase:tag"
        .parse::<RawReference>()
        .is_err());
}

#[test]
fn name_too_long() {
    assert!(("a/".repeat(128) + "a:tag")
        .parse::<RawReference>()
        .is_err());
}

#[test]
fn name_almost_too_long() {
    let path = "a/".repeat(126) + "a";

    assert_eq!(
        ("a/".to_string() + &path + ":tag-puts-this-over-max")
            .parse::<RawReference>()
            .unwrap(),
        ExpectedRawReference {
            path,
            domain: Some("a".to_string()),
            tag: Some("tag-puts-this-over-max".to_string()),
            digest: None
        }
    );
}

#[test]
fn nonsense() {
    assert!("aa/asdf$$^/aa".parse::<RawReference>().is_err());
}

#[test]
fn complex_domain() {
    assert_eq!(
        "sub-dom1.foo.com/bar/baz/quux"
            .parse::<RawReference>()
            .unwrap(),
        ExpectedRawReference {
            path: "bar/baz/quux".to_string(),
            domain: Some("sub-dom1.foo.com".to_string()),
            tag: None,
            digest: None
        }
    );
}

#[test]
fn complex_domain_tag() {
    assert_eq!(
        "sub-dom1.foo.com/bar/baz/quux:long-tag"
            .parse::<RawReference>()
            .unwrap(),
        ExpectedRawReference {
            path: "bar/baz/quux".to_string(),
            domain: Some("sub-dom1.foo.com".to_string()),
            tag: Some("long-tag".to_string()),
            digest: None
        }
    );
}

#[test]
fn complex_domain_tag_2() {
    assert_eq!(
        "b.gcr.io/test.example.com/my-app:test.example.com"
            .parse::<RawReference>()
            .unwrap(),
        ExpectedRawReference {
            path: "test.example.com/my-app".to_string(),
            domain: Some("b.gcr.io".to_string()),
            tag: Some("test.example.com".to_string()),
            digest: None
        }
    );
}

#[test]
fn complex_domain_doubledash() {
    assert_eq!(
        "xn--n3h.com/myimage:xn--n3h.com"
            .parse::<RawReference>()
            .unwrap(),
        ExpectedRawReference {
            path: "myimage".to_string(),
            domain: Some("xn--n3h.com".to_string()),
            tag: Some("xn--n3h.com".to_string()),
            digest: None
        }
    );
}

#[test]
fn complex_domain_doubledash_digest() {
    assert_eq!(
        "xn--7o8h.com/myimage:xn--7o8h.com@sha512:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"
            .parse::<RawReference>()
            .unwrap(),
        ExpectedRawReference {
            path: "myimage".to_string(),
            domain: Some("xn--7o8h.com".to_string()),
            tag: Some("xn--7o8h.com".to_string()),
            digest: Some("sha512:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff".parse().unwrap())
        }
    );
}

#[test]
fn complex_domain_front_hyphen() {
    assert!("-test.com/myimage".parse::<RawReference>().is_err());
}

#[test]
fn complex_domain_end_hyphen() {
    assert!("test-.com/myimage".parse::<RawReference>().is_err());
}

#[test]
fn misleading_1() {
    assert_eq!(
        "foo_bar.com:8080".parse::<RawReference>().unwrap(),
        ExpectedRawReference {
            path: "foo_bar.com".to_string(),
            domain: None,
            tag: Some("8080".to_string()),
            digest: None
        }
    );
}

#[test]
fn misleading_2() {
    assert_eq!(
        "foo/foo_bar.com:8080".parse::<RawReference>().unwrap(),
        ExpectedRawReference {
            path: "foo_bar.com".to_string(),
            domain: Some("foo".to_string()),
            tag: Some("8080".to_string()),
            digest: None
        }
    );
}
