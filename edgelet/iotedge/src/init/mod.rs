pub(crate) mod import;

pub(crate) fn execute() -> Result<(), std::borrow::Cow<'static, str>> {
    return Err(format!("\
		This feature has not yet been implemented.\n\
		\n\
		For now, please use `iotedge init import` to import an existing configuration from Azure IoT Edge <1.2 instead.\
	").into());
}
