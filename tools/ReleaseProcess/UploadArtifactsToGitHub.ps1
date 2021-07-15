$BuildId = "44098637"
$workDir = "./"

# Assume Az CLI is installed & logged in.
$pat = $(az keyvault secret show -n IotEdge1-PAT-msazure --vault-name edgebuildkv | ConvertFrom-Json)
$pat = $pat.value;

$encoded64Pat = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes(':'+$pat))
$header = @{
	"Authorization" = "Basic $encoded64Pat"
}

# Get all the artifact names
$response = $(Invoke-WebRequest -Uri "https://dev.azure.com/msazure/One/_apis/build/builds/$BuildId/artifacts?api-version=6.0" -Headers $header)
$content = ($response.Content | ConvertFrom-Json).value

foreach ($artifact in $content)
{
    $artifactName = $artifact.name
    $artifactUrl = $artifact.resource.downloadUrl
    $artifactPath = "$workDir$artifactName"
    $artifactExtension = ".zip"

    # Download and Expand each artifact
    Invoke-WebRequest -Uri $artifactUrl -Headers $header -OutFile "$artifactPath$artifactExtension"
    Expand-Archive -Path $artifactPath -DestinationPath $workDir

    # Each artifact is a directory, fetch the packages within it.
    $packages = $(Get-ChildItem -Path $artifactPath)

    # Within each directory, rename the artifacts
    $component,$os,$suffix = $artifactName.split('-')

    foreach ($package in $packages)
    {
        $name,$version,$arch,$suffix = $package.Name.split("_")
        $arch,$ext,$suffix = $arch.split(".")

        # Reconstruct the new name from the segments. 
        $finalName = @($name, $version, $os, $arch) -join '_'
        $finalName = "$finalName.$ext"
    }

    Remove-Item -Path $artifactPath
}