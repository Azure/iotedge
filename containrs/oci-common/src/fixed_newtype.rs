/// Defines a zero-sized struct that Serializes to `$val`, and only allows
/// Deserializing from `$val` (with a specified error message if the values
/// don't match).
///
/// e.g:
/// ```
/// use oci_common::fixed_newtype;
///
/// fixed_newtype! {
///     /// A field that is must always de/serialize from/to 2
///     pub struct Always2(u32) == 2u32;
///     else "config.some.field must always be 2";
/// }
/// ```
#[macro_export]
macro_rules! fixed_newtype {
    (
        $(#[$outer:meta])*
        $vis:vis struct $name:ident($type:ty) == $val:literal;
        else $msg:literal;
    ) => {
        $(#[$outer])*
        #[derive(Debug, Default, PartialEq, Eq, Clone)]
        $vis struct $name;

        #[allow(dead_code)]
        impl $name {
            /// Alias for `.default()`
            pub fn new() -> Self {
                Self::default()
            }

            /// Returns the expected value
            pub fn value(self) -> $type {
                $val.into()
            }
        }

        impl serde::Serialize for $name {
            fn serialize<S: serde::Serializer>(&self, serializer: S) -> Result<S::Ok, S::Error> {
                $val.serialize(serializer)
            }
        }

        impl<'de> serde::Deserialize<'de> for $name {
            fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
            where
                D: serde::Deserializer<'de>,
            {
                let v = <$type>::deserialize(deserializer)?;
                if v != $val {
                    Err(serde::de::Error::custom($msg))
                } else {
                    Ok(Self::default())
                }
            }
        }
    };
}
