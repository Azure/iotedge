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

# Given a set of products or the product version, return the version for the given component name
def aziotedge_component_version(product_version_or_iter; $component_name):
if product_version_or_iter | [ strings ] | length == 1 then
  # Arg is the aziot-edge product version
  [ component_iter(product_iter("aziot-edge"; product_version_or_iter); $component_name).version ] | first
elif product_version_or_iter | [ objects ] | length != 0 then
  # Arg is a product iterator
  [ component_iter(product_version_or_iter; $component_name).version ] | first
else
  halt_error(
    [
      "Error: aziotedge_component_version: expected a product version string or a product iterator but got",
      "\(product_version_or_iter)\n"
    ] | join(" ")
  )
end;

# Given the product name and version, return the product's version
def product_version($product_name; $product_version):
[ product_iter($product_name; $product_version) | .version ] | first;

# Return a new product-versions document whose "aziot-edge" product and component versions have been updated to the
# given versions
def update_aziotedge_versions($current_product_ver; $new_product_ver; $edgelet_ver; $identity_ver; $core_image_ver):
aziotedge_component_version($current_product_ver; "aziot-edge") = $edgelet_ver
| aziotedge_component_version($current_product_ver; "aziot-identity-service") = $identity_ver
| aziotedge_component_version($current_product_ver; "azureiotedge-agent") = $core_image_ver
| aziotedge_component_version($current_product_ver; "azureiotedge-hub") = $core_image_ver
| aziotedge_component_version($current_product_ver; "azureiotedge-simulated-temperature-sensor") = $core_image_ver
| aziotedge_component_version($current_product_ver; "azureiotedge-diagnostics") = $edgelet_ver
| product_version("aziot-edge"; $current_product_ver) = $new_product_ver;

# Return a new product-versions document which adds "aziot-edge" product entries to the stable and LTS channels. The
# new entries will be copies of the existing stable entry at $current_product_ver, but with the product and component
# versions updated to the given versions. The product name is also updated for the LTS channel.
def add_aziotedge_products($current_product_ver; $new_product_ver; $edgelet_ver; $identity_ver; $core_image_ver):
(
  # Make a copy of the existing product entry from the stable channel, but with the versions updated
  channels_iter("stable")
  | product_iter(.products; "aziot-edge"; $current_product_ver)
  | aziotedge_component_version(.; "aziot-edge") = $edgelet_ver
  | aziotedge_component_version(.; "aziot-identity-service") = $identity_ver
  | aziotedge_component_version(.; "azureiotedge-agent") = $core_image_ver
  | aziotedge_component_version(.; "azureiotedge-hub") = $core_image_ver
  | aziotedge_component_version(.; "azureiotedge-simulated-temperature-sensor") = $core_image_ver
  | aziotedge_component_version(.; "azureiotedge-diagnostics") = $edgelet_ver
  | .version = $new_product_ver
) as $product
# Add the new product entries to the stable and LTS channels
| (channels_iter("stable") | .products) += [ $product ]
| (channels_iter("lts") | .products) += [
    $product | .name = ($new_product_ver | split(".") | .[0:-1] | join(".") | "Azure IoT Edge \(.) LTS")
  ]
# The stable channel should only have one release (the latest), so remove the previous product entry
| del(channels_iter("stable") | .products[] | select(.id == "aziot-edge" and .version == $current_product_ver));
