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

# Given a set of products or the product version, return the version for the given component name
def component_version(product_version_or_iter; $component_name):
if product_version_or_iter | [ strings ] | length == 1 then
  # Arg is the aziot-edge product version
  component_iter(product_iter("aziot-edge"; product_version_or_iter); $component_name).version
elif product_version_or_iter | [ objects ] | length != 0 then
  # Arg is a product iterator
  component_iter(product_version_or_iter; $component_name).version
else
  halt_error(
    [
      "Error: component_version: expected a product version string or a product iterator but got",
      "\(product_version_or_iter)\n"
    ] | join(" ")
  )
end;

# Given the product name and version, return the product's version
def product_version($product_name; $product_version):
product_iter($product_name; $product_version) | .version;

# Return a new JSON object whose "aziot-edge" product and component versions have been updated to the given versions
def update_aziotedge_versions($current_product_ver; $new_product_ver; $edgelet_ver; $identity_ver; $core_image_ver):
component_version($current_product_ver; "aziot-edge") = $edgelet_ver
| component_version($current_product_ver; "aziot-identity-service") = $identity_ver
| component_version($current_product_ver; "azureiotedge-agent") = $core_image_ver
| component_version($current_product_ver; "azureiotedge-hub") = $core_image_ver
| component_version($current_product_ver; "azureiotedge-simulated-temperature-sensor") = $core_image_ver
| component_version($current_product_ver; "azureiotedge-diagnostics") = $edgelet_ver
| product_version("aziot-edge"; $current_product_ver) = $new_product_ver;

# Return a new product-versions document which adds "aziot-edge" product entries to the stable and LTS channels. The new
# entries will be copies of the existing stable entry at $current_product_ver, but with the product and component
# versions updated to the given versions. The product name is also updated for the LTS channel.
def add_aziotedge_products($current_product_ver; $new_product_ver; $edgelet_ver; $identity_ver; $core_image_ver):
(
  channels_iter("stable")
  | product_iter(.products; "aziot-edge"; $current_product_ver)
  | component_version(.; "aziot-edge") = $edgelet_ver
  | component_version(.; "aziot-identity-service") = $identity_ver
  | component_version(.; "azureiotedge-agent") = $core_image_ver
  | component_version(.; "azureiotedge-hub") = $core_image_ver
  | component_version(.; "azureiotedge-simulated-temperature-sensor") = $core_image_ver
  | component_version(.; "azureiotedge-diagnostics") = $edgelet_ver
  | .version = $new_product_ver
) as $product
| (channels_iter("stable") | .products) += [ $product ]
| (channels_iter("lts") | .products) += [
    $product | .name = ($new_product_ver | split(".") | .[0:-1] | join(".") | "Azure IoT Edge \(.) LTS")
  ];
