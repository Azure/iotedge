pub mod import;

pub fn execute() -> Result<(), std::borrow::Cow<'static, str>> {
    Err("\
        This feature has not yet been implemented.\n\
        \n\
        For now, please use `iotedge init import` to import an existing configuration from Azure IoT Edge <1.2 instead.\
    ".into())
}
