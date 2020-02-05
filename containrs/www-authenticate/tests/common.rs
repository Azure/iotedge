#[macro_export]
macro_rules! string_map(
    {} => { ::std::collections::HashMap::new() };
    { $($key:expr => $value:expr),+} => {
        {
            let mut m = ::std::collections::HashMap::new();
            $(
                m.insert($key.to_string(), $value.to_string());
            )+
            m
        }
     };
);

#[macro_export]
macro_rules! single_challenge_valid {
    ($name:ident, $teststr:expr, $scheme:expr, $parameters:expr) => {
        #[test]
        fn $name() {
            assert_eq!(
                $teststr.parse::<WWWAuthenticate>().unwrap(),
                WWWAuthenticate::new(vec![Challenge::new($scheme, $parameters).unwrap()])
            );
        }
    };
}

#[macro_export]
macro_rules! multi_challenge_valid {
    ($name:ident, $teststr:expr, $({$scheme:expr, $parameters:expr}),+) => {
        #[test]
        fn $name() {
            assert_eq!(
                $teststr.parse::<WWWAuthenticate>().unwrap(),
                WWWAuthenticate::new(vec![
                    $(
                        Challenge::new($scheme, $parameters).unwrap(),
                    )+
                ])
            );
        }
    };
}

/// $err should be a pattern describing the expected error type
///
/// e.g: invalid!(test, "asd asd asd asd", WWWAuthenticateError::Pest(_))
#[macro_export]
macro_rules! invalid {
    ($name:ident, $teststr:expr, $err:pat) => {
        #[test]
        fn $name() {
            let err = $teststr.parse::<WWWAuthenticate>().unwrap_err();
            match err {
                $err => {}
                _ => panic!(),
            }
        }
    };
}
