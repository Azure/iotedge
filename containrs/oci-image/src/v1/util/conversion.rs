use std::collections::HashMap;

use crate::v1 as imgspec;
use oci_runtime::v1 as rtspec;

/// A OCI Image Spec compliant converter between OCI Image Spec `Image`s to OCI
/// Runtime Spec `Spec`s, as documented at
/// https://github.com/opencontainers/image-spec/blob/master/conversion.md
///
/// Callers must provide a `default_cwd` String, which will be used if
/// `image.working_dir` is `None`.
///
/// Callers must provide a `process_user_f` function that converts the
/// Option<String> in `image.config.user` into a corresponding OCI Runtime
/// `spec.process.user` [`oci_runtime::v1::process::User`] structure.
/// The specifics of this conversion varies by platform and implementation.
pub fn image_to_runtime_spec_v1<F, E>(
    image: &imgspec::Image,
    default_cwd: impl Into<String>,
    process_user_f: F,
) -> Result<rtspec::Spec, E>
where
    F: FnOnce(&Option<String>) -> Result<oci_runtime::v1::process::User, E>,
{
    // to avoid performing None checks all over the place, fall-back to a "dummy"
    // ImageConfig if `image.config` is set to None.
    let dummy_image_config = imgspec::ImageConfig::default();
    let img_config: &_ = image.config.as_ref().unwrap_or(&dummy_image_config);

    let mut rt_spec = rtspec::Spec::new_base();

    // # Parsed Fields
    // Config.User -> process.user.*
    let process_user = process_user_f(&img_config.user)?;

    // # Verbatim Fields
    // Config.WorkingDir -> process.cwd
    rt_spec.process = Some(rtspec::process::Process::new_base(
        match img_config.working_dir {
            Some(ref cwd) => cwd.clone(),
            None => default_cwd.into(),
        },
        process_user,
    ));

    let rt_process = rt_spec.process.as_mut().unwrap();

    // Config.Env -> process.env
    if let Some(env) = &img_config.env {
        if !env.is_empty() {
            rt_process.env = Some(Vec::new());
            let rt_process_env = rt_process.env.as_mut().unwrap();
            for envvar in env {
                rt_process_env.push(envvar.clone())
            }
        }
    }

    // Config.Entrypoint -> process.args
    if let Some(entrypoint) = &img_config.entrypoint {
        rt_process.args.extend(entrypoint.iter().cloned());
    }

    // Config.Cmd -> process.args
    if let Some(cmd) = &img_config.cmd {
        rt_process.args.extend(cmd.iter().cloned());
    }

    // # Annotation Fields

    // precedence:
    //  1) values in `image.config.labels`
    //  2) implicit annotations from `image` (e.g: os, architecture, etc...)
    let mut add_annotation_with_precedence = |annotation: &str, base: Option<&String>| {
        if let Some(x) = (img_config.labels)
            .as_ref()
            .and_then(|l| l.get(annotation))
            .or(base)
        {
            rt_spec
                .annotations
                .get_or_insert(HashMap::new())
                .insert(annotation.to_string(), x.clone());
        }
    };

    let annotations = vec![
        ("org.opencontainers.image.os", Some(&image.os)),
        (
            "org.opencontainers.image.architecture",
            Some(&image.architecture),
        ),
        ("org.opencontainers.image.author", image.author.as_ref()),
        ("org.opencontainers.image.created", image.created.as_ref()),
        (
            "org.opencontainers.image.stopSignal",
            img_config.stop_signal.as_ref(),
        ),
    ];

    for (annotation, base) in annotations {
        add_annotation_with_precedence(annotation, base)
    }

    // # Optional Fields
    // Config.ExposedPorts -> annotations
    if let Some(exposed_ports) = &img_config.exposed_ports {
        // > "org.opencontainers.image.exposedPorts" is the list of values that
        // > correspond to the keys defined for Config.ExposedPorts
        // > (string, comma-separated values).
        let value = exposed_ports
            .keys()
            .cloned()
            .collect::<Vec<String>>()
            .join(",");
        add_annotation_with_precedence("org.opencontainers.image.exposedPorts", Some(&value));
    }

    // Config.Volumes -> spec.mounts
    if let Some(volumes) = &img_config.volumes {
        if !volumes.is_empty() {
            rt_spec.mounts = Some(Vec::new());
            let rt_mounts = rt_spec.mounts.as_mut().unwrap();
            for path in volumes.keys() {
                rt_mounts.push(rtspec::Mount::new_base(path.clone()))
            }
        }
    }

    Ok(rt_spec)
}
