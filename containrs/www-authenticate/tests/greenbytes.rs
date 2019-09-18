//! pulled from http://test.greenbytes.de/tech/tc/httpauth/

use www_authenticate::*;

mod common;

single_challenge_valid!(simplebasic, 
    r#"Basic realm="foo""#, 
    ChallengeScheme::Basic,
    string_map!{ "realm" => "foo" }
);

single_challenge_valid!(simplebasiclf, 
    r#"Basic
 realm="foo""#, 
    ChallengeScheme::Basic,
    string_map!{ "realm" => "foo" }
);

single_challenge_valid!(simplebasicucase, 
    r#"BASIC REALM="foo""#, 
    ChallengeScheme::Basic,
    string_map!{ "realm" => "foo" }
);

// TODO: Uncomment once challenge-type valdation is in
// /// realm is only allowed to use quoted-string syntax
// invalid!(simplebasictok, r#"Basic realm=foo"#, Error::ChallengeError(?));

// FIXME: apparently, this should pass the parser, but not the realm check?
// invalid!(simplebasictokbs, r#"Basic realm=\f\o\o"#, Error::?);

// TODO: Uncomment once challenge-type valdation is in
// /// realm is only allowed to use quoted-string syntax
// invalid!(simplebasicsq, r#"Basic realm='foo'"#, Error::?);

single_challenge_valid!(simplebasicpct, 
    r#"Basic realm="foo%20bar""#, 
    ChallengeScheme::Basic,
    string_map!{ "realm" => "foo%20bar" }
);

single_challenge_valid!(simplebasiccomma, 
    r#"Basic , realm="foo""#, 
    ChallengeScheme::Basic,
    string_map!{ "realm" => "foo" }
);

invalid!(simplebasiccomma2, r#"Basic, realm="foo""#, WWWAuthenticateError::Pest(_));

// TODO: Uncomment once challenge-type valdation is in
// invalid!(simplebasicnorealm, r#"Basic"#, Error::ChallengeError(?));

// TODO: Uncomment once challenge-type valdation is in
// invalid!(simplebasic2realms, r#"Basic realm="foo", realm="bar""#, Error::ChallengeError(?))

single_challenge_valid!(simplebasicwsrealm, 
    r#"Basic realm = "foo""#, 
    ChallengeScheme::Basic,
    string_map!{ "realm" => "foo" }
);

// FIXME: this is broken
// single_challenge_valid!(simplebasicrealmsqc, 
//     r#"Basic realm="\f\o\o""#, 
//     ChallengeScheme::Basic,
//     string_map!{ "realm" => "foo" }
// );

// TODO: Uncomment once double-quote unescape is in 
// single_challenge_valid!(simplebasicrealmsqc, 
//     r#"Basic realm="\"foo\""#, 
//     ChallengeScheme::Basic,
//     string_map!{ "realm" => "foo" }
// );

single_challenge_valid!(simplebasicnewparam1, 
    r#"Basic realm="foo", bar="xyz",, a=b,,,c=d"#, 
    ChallengeScheme::Basic,
    string_map!{
        "realm" => "foo",
        "bar" => "xyz",
        "a" => "b",
        "c" => "d"
    }
);

single_challenge_valid!(simplebasicnewparam2, 
    r#"Basic bar="xyz", realm="foo""#, 
    ChallengeScheme::Basic,
    string_map!{
        "realm" => "foo",
        "bar" => "xyz"
    }
);

single_challenge_valid!(simplebasicrealmiso88591, 
    r#"Basic realm="foo-ä""#, 
    ChallengeScheme::Basic,
    string_map!{ "realm" => "foo-ä" }
);

single_challenge_valid!(simplebasicrealmut8, 
    r#"Basic realm="foo-Ã¤""#, 
    ChallengeScheme::Basic,
    string_map!{ "realm" => "foo-Ã¤" }
);

single_challenge_valid!(simplebasicrealmrfc2047, 
    r#"Basic realm="=?ISO-8859-1?Q?foo-=E4?=""#, 
    ChallengeScheme::Basic,
    string_map!{ "realm" => "=?ISO-8859-1?Q?foo-=E4?=" }
);

multi_challenge_valid!(multibasicunknown,
    r#"Basic realm="basic", Newauth realm="newauth""#,
    {
        ChallengeScheme::Basic,
        string_map!{ "realm" => "basic" }
    },
    {
        ChallengeScheme::Other("newauth".to_string()),
        string_map!{ "realm" => "newauth" }
    }
);

multi_challenge_valid!(multibasicunknownnoparam,
    r#"Basic realm="basic", Newauth"#,
    {
        ChallengeScheme::Basic,
        string_map!{ "realm" => "basic" }
    },
    {
        ChallengeScheme::Other("newauth".to_string()),
        string_map!{}
    }
);

multi_challenge_valid!(multibasicunknown2,
    r#"Newauth realm="newauth", Basic realm="basic""#,
    {
        ChallengeScheme::Other("newauth".to_string()),
        string_map!{ "realm" => "newauth" }
    },
    {
        ChallengeScheme::Basic,
        string_map!{ "realm" => "basic" }
    }
);

multi_challenge_valid!(multibasicunknown2np,
    r#"Newauth, Basic realm="basic""#,
    {
        ChallengeScheme::Other("newauth".to_string()),
        string_map!{}
    },
    {
        ChallengeScheme::Basic,
        string_map!{ "realm" => "basic" }
    }
);

single_challenge_valid!(multibasicempty, 
    r#",Basic realm="basic""#, 
    ChallengeScheme::Basic,
    string_map!{ "realm" => "basic" }
);

// TODO: Uncomment once double-quote unescape is in 
// multi_challenge_valid!(multibasicqs, 
//     r#"Newauth realm="apps", type=1, title="Login to \"apps\"", Basic realm="simple""#, 
//     {
//         ChallengeScheme::Other("newauth".to_string()),
//         string_map!{
//             "realm" => "apps",
//             "type" => "1",
//             "title" => "Login to \"apps\"" 
//         }
//     },
//     {
//         ChallengeScheme::Basic,
//         string_map!{ "realm" => "simple" }
//     }
// );

multi_challenge_valid!(multidisgscheme, 
    r#"Newauth realm="Newauth Realm", basic=foo, Basic realm="Basic Realm""#, 
    {
        ChallengeScheme::Other("newauth".to_string()),
        string_map!{
            "realm" => "Newauth Realm",
            "basic" => "foo"
        }
    },
    {
        ChallengeScheme::Basic,
        string_map!{ "realm" => "Basic Realm" }
    }
);

single_challenge_valid!(unknown, 
    r#"Newauth param="value""#, 
    ChallengeScheme::Other("newauth".to_string()),
    string_map!{ "param" => "value" }
);

multi_challenge_valid!(parametersnotrequired, 
    r#"A, B"#, 
    {
        ChallengeScheme::Other("a".to_string()),
        string_map!{}
    },
    {
        ChallengeScheme::Other("b".to_string()),
        string_map!{}
    }
);

single_challenge_valid!(disguisedrealm, 
    r#"Basic foo="realm=nottherealm", realm="basic""#, 
    ChallengeScheme::Basic,
    string_map!{
        "foo" => "realm=nottherealm",
        "realm" => "basic"
    }
);

single_challenge_valid!(disguisedrealm2, 
    r#"Basic nottherealm="nottherealm", realm="basic""#, 
    ChallengeScheme::Basic,
    string_map!{
        "nottherealm" => "nottherealm",
        "realm" => "basic"
    }
);

invalid!(missing_quote, r#"Basic realm="basic"#, WWWAuthenticateError::Pest(_));
