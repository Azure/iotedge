pub fn get_management_uri(contents: &str) -> Option<String> {
    let start_pattern = "management_uri: ";
    let start = contents.find(start_pattern)? + start_pattern.len();
    let end = contents.find("workload_uri: ")?;
    let pre = contents[start..end].trim();
    Some(pre[1..pre.len()-1].to_string())
}