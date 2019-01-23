New-Module -ScriptBlock {

    [Console]::OutputEncoding = New-Object -typename System.Text.ASCIIEncoding

    <#
     # Completes initialization of a Windows VM (version 1809 or later) that is
     # installed behind an HTTPS proxy server for end-to-end testing of Azure
     # IoT Edge. It installs/enables sshd so that the test agent can SSH into
     # this VM and Linux VMs using the same commands. It also configures system-
     # wide proxy settings (so that sshd and related components can be
     # downloaded) and sets the default SSH shell to a newer version of
     # PowerShell (so that the agent can make use of the NoProxy parameter
     # on Invoke-WebRequest rather than reverting system-wide proxy settings).
     #>

    #requires -Version 5
    #requires -RunAsAdministrator

    Set-Variable PwshInstallUri -Option Constant -Value 'https://github.com/PowerShell/PowerShell/releases/download/v6.1.1/PowerShell-6.1.1-win-x64.zip'
    Set-Variable ProxySettingsModule -Option Constant -Value 'https://raw.githubusercontent.com/PowerShell/NetworkingDsc/8a4ae47b835b931d816dbbd5244d59e8fb0ce36b/Modules/NetworkingDsc/DSCResources/MSFT_ProxySettings/MSFT_ProxySettings.psm1'
    Set-Variable OpenSshUtilsManifest -Option Constant -Value 'https://raw.githubusercontent.com/PowerShell/openssh-portable/latestw_all/contrib/win32/openssh/OpenSSHUtils.psd1'
    Set-Variable OpenSshUtilsModule -Option Constant -Value 'https://raw.githubusercontent.com/PowerShell/openssh-portable/latestw_all/contrib/win32/openssh/OpenSSHUtils.psm1'

    function Get-WebResource {
        param (
            [String] $proxyUri,
            [String] $sourceUri,
            [String] $destinationFile
        )

        if (-not (Test-Path $destinationFile -PathType Leaf)) {
            Invoke-WebRequest -UseBasicParsing $sourceUri -Proxy $proxyUri -OutFile $destinationFile
        }
    }

    function Start-TestPath {
        param (
            [String] $path,
            [Int32] $timeoutSecs
        )

        $action = {
            while (-not (Test-Path $args[0])) {
                Start-Sleep 5
            }
            
            Test-Path $args[0]
        }

        $job = Start-Job $action -ArgumentList $path
        $result = $job | Wait-Job -Timeout $timeoutSecs | Receive-Job
        $job | Stop-Job -PassThru | Remove-Job
        return $result
    }

    function Initialize-WindowsVM {
        [CmdletBinding()]
        param (
            [ValidateNotNullOrEmpty()]
            [String] $ProxyHostname,

            [ValidateNotNullOrEmpty()]
            [String] $SshPublicKeyBase64
        )
        
        Set-StrictMode -Version "Latest"
        $ErrorActionPreference = "Stop"

        $sshPublicKey = [System.Text.Encoding]::Utf8.GetString([System.Convert]::FromBase64String($SshPublicKeyBase64))
        $proxyUri = "http://${ProxyHostname}:3128"
        
        Write-Host "Setting wininet proxy"

        $proxyModuleFile = ".\$(Split-Path $ProxySettingsModule -Leaf)"
        Get-WebResource $proxyUri $ProxySettingsModule $proxyModuleFile
        # The following Import-Module statement will cause a few errors about missing module
        # dependencies. The missing modules aren't really required for what we're trying to do,
        # so we won't bother downloading them.
        Import-Module $proxyModuleFile -Force | Out-Null
        Set-TargetResource -IsSingleInstance Yes -EnableManualProxy $true -ProxyServer $proxyUri
        Get-TargetResource -IsSingleInstance Yes | Format-Table # Print the result to the logs

        Write-Host "Setting winhttp proxy"
        
        netsh winhttp set proxy "${ProxyHostname}:3128"

        # Install newer version of PowerShell (6.0 or later) so that the -NoProxy option works in Invoke-WebRequest
        Write-Host "Installing latest pwsh"
        
        $zipFile = ".\$(Split-Path $PwshInstallUri -Leaf)"
        Get-WebResource $proxyUri $PwshInstallUri $zipFile
        Expand-Archive -Force "$zipFile"
        $pwshPath = Join-Path (Join-Path (Get-Location) (Get-Item $zipFile).BaseName) "pwsh.exe" -Resolve
        
        # Add public key so agent can SSH into this runner
        $authorizedKeys = Join-Path ${env:UserProfile} (Join-Path ".ssh" "authorized_keys")
        Write-Host "Adding public key to $authorizedKeys"
        
        New-Item (Split-Path $authorizedKeys -Parent) -ItemType Directory -Force | Out-Null
        Add-Content "$sshPublicKey" -Path $authorizedKeys
        
        # Fix up authorized_keys file permissions
        $openSshUtils = ".\$(Split-Path "$OpenSshUtilsManifest" -Leaf)"
        Get-WebResource $proxyUri $OpenSshUtilsManifest $openSshUtils
        Get-WebResource $proxyUri $OpenSshUtilsModule ".\$(Split-Path "$OpenSshUtilsModule" -Leaf)"
        Import-Module $openSshUtils -Force
        Repair-AuthorizedKeyPermission $authorizedKeys
        
        Write-Host "Installing sshd"
        
        Add-WindowsCapability -Online -Name OpenSSH.Client~~~~0.0.1.0
        Add-WindowsCapability -Online -Name OpenSSH.Server~~~~0.0.1.0

        Set-Service -Name ssh-agent -StartupType Automatic
        Set-Service -Name sshd -StartupType Automatic

        Write-Host "Making pwsh the default shell for ssh"

        New-Item 'HKLM:\SOFTWARE\OpenSSH' -Force | New-ItemProperty -Force -Name DefaultShell -Value "$pwshPath"
        
        Write-Host "Starting sshd"

        Start-Service ssh-agent
        Start-Service sshd

        # Update sshd_config to look in the right place for authorized_keys
        Write-Host "Updating sshd_config"
        
        $sshdConfig = "$env:ProgramData\ssh\sshd_config"
        $exists = Start-TestPath $sshdConfig -timeoutSecs 30
        if (-not $exists) {
            Write-Error "Could not find $sshdConfig, exiting..."
            return 1
        }

        $findLine = '^(\s*AuthorizedKeysFile\s+)\.ssh/authorized_keys$'
        $replaceLine = "`$1$authorizedKeys"
        (Get-Content "$sshdConfig") -replace "$findLine", "$replaceLine" | Out-File -Encoding Utf8 "$sshdConfig"

        $findLine = '^(\s*AuthorizedKeysFile\s+__PROGRAMDATA__/ssh/administrators_authorized_keys)$'
        $replaceLine = '#$1'
        (Get-Content "$sshdConfig") -replace "$findLine", "$replaceLine" | Out-File -Encoding Utf8 "$sshdConfig"

        Write-Host "Restarting sshd"

        Restart-Service ssh-agent
        Restart-Service sshd
    }

    Export-ModuleMember -Function Initialize-WindowsVM
}