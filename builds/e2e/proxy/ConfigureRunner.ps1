New-Module -ScriptBlock {

    <#
     # Completes initialization of a Windows VM (version 1809 or later) that is
     # installed behind an HTTPS proxy server for end-to-end testing of Azure
     # IoT Edge. It configures system-wide proxy settings.
     #>

    #requires -Version 5
    #requires -RunAsAdministrator

    function Initialize-WindowsVM {
        [CmdletBinding()]
        param (
            [ValidateNotNullOrEmpty()]
            [String] $ProxyHostname
        )
        
        Set-StrictMode -Version "Latest"
        $ErrorActionPreference = "Stop"

        $proxyUri = "http://${ProxyHostname}:3128"
        
        Write-Host 'Setting wininet proxy'

        Install-PackageProvider -Name NuGet -Force -Proxy $proxyUri
        Register-PSRepository -Default -Proxy $proxyUri
        Set-PSRepository PSGallery -InstallationPolicy Trusted -Proxy $proxyUri
        Install-Module NetworkingDsc -MinimumVersion 6.3 -AllowClobber -Force -Proxy $proxyUri
        Invoke-DscResource ProxySettings -Method Set -ModuleName NetworkingDsc -Property @{
            IsSingleInstance = "Yes"
            EnableManualProxy = $true
            ProxyServerBypassLocal = $true
            ProxyServer = $proxyUri
        }
        # output settings to log
        Invoke-DscResource ProxySettings -Method Get -ModuleName NetworkingDsc -Property @{ IsSingleInstance = "Yes" }

        Write-Host 'Setting winhttp proxy'
        
        netsh winhttp set proxy "${ProxyHostname}:3128" "<local>"

        # iotedge-moby needs this variable for `docker pull`
        Write-Host 'Setting HTTPS_PROXY in environment'
        [Environment]::SetEnvironmentVariable("HTTPS_PROXY", $proxyUri, [EnvironmentVariableTarget]::Machine)
    }

    Export-ModuleMember -Function Initialize-WindowsVM
}
