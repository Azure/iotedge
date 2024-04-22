# Return the set of channel objects with the given name
def channels_iter($channel_name):
.channels[] | select(.name == $channel_name);

# Given a set of channels, return the set of product objects that match the given product name and version
def product_iter(products; $product_name; $product_version):
products[] | select(.id == $product_name and .version == $product_version);

# Return the set of product objects from all channels that match the given product name and version
def product_iter($product_name; $product_version):
product_iter(.channels[].products; $product_name; $product_version);

# Given a set of product objects, return the set of component objects that match the given component name
def component_iter(product_iter; $component_name):
product_iter | .components[] | select(.name == $component_name);

# Given a set of product objects, return an array of component objects that match the given component name
def components(product_iter; $component_name):
[ component_iter(product_iter; $component_name) ];

# For all "aziot-edge" products that match the given version, return the versions of all components matching the given
# name
def aziotedge_component_version($aziotedge_product_version; $component_name):
[ components(product_iter("aziot-edge"; $aziotedge_product_version); $component_name)[].version ]
| . as $versions
|
if length == 0 then
  "Error: no \(component_name) component found for IoT Edge version \(aziotedge_product_version) in \(input_filename)\n"
  | halt_error
elif length == 1 then
  first
elif (first as $ver | all(. == $ver)) then
  first
else
  [
    "Error in \(input_filename): for all releases of IoT Edge version \(aziotedge_product_version), expected",
    "\(component_name) component to have the same version but found:\n\($versions)\n"
  ]
  | join(" ")
  | halt_error
end;

# Given a set of products and the product version, return a reference to the "aziot-edge" component's mutable version
# property
def edgelet_version_mut(product_iter; $aziotedge_product_version):
component_iter(product_iter; "aziot-edge").version;

# Given the product version, return a reference to the "aziot-edge" component's mutable version property
def edgelet_version_mut($aziotedge_product_version):
edgelet_version_mut(product_iter("aziot-edge"; $aziotedge_product_version); "aziot-edge").version;

# Given a set of products and the product version, return a reference to the "aziot-identity-service" component's
# mutable version property
def identity_version_mut(product_iter; $aziotedge_product_version):
component_iter(product_iter; "aziot-identity-service").version;

# Given the product version, return a reference to the "aziot-identity-service" component's mutable version property
def identity_version_mut($aziotedge_product_version):
identity_version_mut(product_iter("aziot-edge"; $aziotedge_product_version); "aziot-identity-service").version;

# Given a set of products and the product version, return a reference to the "azureiotedge-agent" component's mutable
# version property
def agent_version_mut(product_iter; $aziotedge_product_version):
component_iter(product_iter; "azureiotedge-agent").version;

# Given the product version, return a reference to the "azureiotedge-agent" component's mutable version property
def agent_version_mut($aziotedge_product_version):
agent_version_mut(product_iter("aziot-edge"; $aziotedge_product_version); "azureiotedge-agent").version;

# Given a set of products and the product version, return a reference to the "azureiotedge-hub" component's mutable
# version property
def hub_version_mut(product_iter; $aziotedge_product_version):
component_iter(product_iter; "azureiotedge-hub").version;

# Given the product version, return a reference to the "azureiotedge-hub" component's mutable version property
def hub_version_mut($aziotedge_product_version):
hub_version_mut(product_iter("aziot-edge"; $aziotedge_product_version); "azureiotedge-hub").version;

# Given a set of products and the product version, return a reference to the "azureiotedge-simulated-temperature-sensor"
# component's mutable version property
def simulated_temperature_sensor_version_mut(product_iter; $aziotedge_product_version):
component_iter(product_iter; "azureiotedge-simulated-temperature-sensor").version;

# Given the product version, return a reference to the "azureiotedge-simulated-temperature-sensor" component's mutable
# version property
def simulated_temperature_sensor_version_mut($aziotedge_product_version):
simulated_temperature_sensor_version_mut(
  product_iter("aziot-edge"; $aziotedge_product_version); "azureiotedge-simulated-temperature-sensor"
).version;

# Given a set of products and the product version, return a reference to the "azureiotedge-diagnostics" component's
# mutable version property
def diagnostics_version_mut(product_iter; $aziotedge_product_version):
component_iter(product_iter; "azureiotedge-diagnostics").version;

# Given the product version, return a reference to the "azureiotedge-diagnostics" component's mutable version property
def diagnostics_version_mut($aziotedge_product_version):
diagnostics_version_mut(product_iter("aziot-edge"; $aziotedge_product_version); "azureiotedge-diagnostics").version;

# Given the product name and version, return a reference to the product's mutable version property
def product_version_mut($product_name; $product_version):
product_iter($product_name; $product_version) | .version;

# Return a new JSON object whose "aziot-edge" product and component versions have been updated to the given versions
def update_aziotedge_versions($current_product_ver; $new_product_ver; $edgelet_ver; $identity_ver; $core_image_ver):
edgelet_version_mut($current_product_ver) = $edgelet_ver
| identity_version_mut($current_product_ver) = $identity_ver
| agent_version_mut($current_product_ver) = $core_image_ver
| hub_version_mut($current_product_ver) = $core_image_ver
| simulated_temperature_sensor_version_mut($current_product_ver) = $core_image_ver
| diagnostics_version_mut($current_product_ver) = $edgelet_ver
| product_version_mut("aziot-edge"; $current_product_ver) = $new_product_ver;

# Return a new product-versions document which adds "aziot-edge" product entries to the stable and LTS channels. The new
# entries will be copies of the existing stable entry at $current_product_ver, but with the product and component
# versions updated to the given versions. The product name is also updated for the LTS channel.
def add_aziotedge_products($current_product_ver; $new_product_ver; $edgelet_ver; $identity_ver; $core_image_ver):
(
  channels_iter("stable")
  | product_iter(.products; "aziot-edge"; $current_product_ver)
  | edgelet_version_mut(.; $current_product_ver) = $edgelet_ver
  | identity_version_mut(.; $current_product_ver) = $identity_ver
  | agent_version_mut(.; $current_product_ver) = $core_image_ver
  | hub_version_mut(.; $current_product_ver) = $core_image_ver
  | simulated_temperature_sensor_version_mut(.; $current_product_ver) = $core_image_ver
  | diagnostics_version_mut(.; $current_product_ver) = $edgelet_ver
  | .version = $new_product_ver
) as $product
| (channels_iter("stable") | .products) += [ $product ]
| (channels_iter("lts") | .products) += [
    $product | .name = ($new_product_ver | split(".") | .[0:-1] | join(".") | "Azure IoT Edge \(.) LTS")
  ];
