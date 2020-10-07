pub(crate) fn is_rfc_1035_valid(name: &str) -> bool {
    if name.is_empty() || name.len() > 255 {
        return false;
    }

    let mut labels = name.split('.');

    let all_labels_valid = labels.all(|label| {
        if label.len() > 63 {
            return false;
        }

        let first_char = match label.chars().next() {
            Some(c) => c,
            None => return false,
        };
        if !first_char.is_ascii_alphabetic() {
            return false;
        }

        if label
            .chars()
            .any(|c| !c.is_ascii_alphanumeric() && c != '-')
        {
            return false;
        }

        let last_char = label
            .chars()
            .last()
            .expect("label has at least one character");
        if !last_char.is_ascii_alphanumeric() {
            return false;
        }

        true
    });
    if !all_labels_valid {
        return false;
    }

    true
}

pub(crate) fn check_length_for_local_issuer(name: &str) -> bool {
    if name.is_empty() || name.len() > 64 {
        return false;
    }

    true
}

#[cfg(test)]
mod tests {
    use super::check_length_for_local_issuer;
    use super::is_rfc_1035_valid;

    #[test]
    fn test_check_length_for_local_issuer() {
        let longest_valid_label = "a".repeat(64);
        assert!(check_length_for_local_issuer(&longest_valid_label));

        let invalid_label = "a".repeat(65);
        assert!(!check_length_for_local_issuer(&invalid_label));
    }

    #[test]
    fn test_is_rfc_1035_valid() {
        let longest_valid_label = "a".repeat(63);
        let longest_valid_name = format!(
            "{label}.{label}.{label}.{label_rest}",
            label = longest_valid_label,
            label_rest = "a".repeat(255 - 63 * 3 - 3)
        );
        assert_eq!(longest_valid_name.len(), 255);

        assert!(is_rfc_1035_valid("foobar"));
        assert!(is_rfc_1035_valid("foobar.baz"));
        assert!(is_rfc_1035_valid(&longest_valid_label));
        assert!(is_rfc_1035_valid(&format!(
            "{label}.{label}.{label}",
            label = longest_valid_label
        )));
        assert!(is_rfc_1035_valid(&longest_valid_name));
        assert!(is_rfc_1035_valid("xn--v9ju72g90p.com"));
        assert!(is_rfc_1035_valid("xn--a-kz6a.xn--b-kn6b.xn--c-ibu"));

        assert!(is_rfc_1035_valid("FOOBAR"));
        assert!(is_rfc_1035_valid("FOOBAR.BAZ"));
        assert!(is_rfc_1035_valid("FoObAr01.bAz"));

        assert!(!is_rfc_1035_valid(&format!("{}a", longest_valid_label)));
        assert!(!is_rfc_1035_valid(&format!("{}a", longest_valid_name)));
        assert!(!is_rfc_1035_valid("01.org"));
        assert!(!is_rfc_1035_valid("\u{4eca}\u{65e5}\u{306f}"));
        assert!(!is_rfc_1035_valid("\u{4eca}\u{65e5}\u{306f}.com"));
        assert!(!is_rfc_1035_valid("a\u{4eca}.b\u{65e5}.c\u{306f}"));
        assert!(!is_rfc_1035_valid("FoObAr01.bAz-"));
    }
}
