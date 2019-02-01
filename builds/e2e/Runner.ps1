New-Module -ScriptBlock {

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

    Set-Variable OpenSshUtilsManifest -Option Constant -Value 'https://raw.githubusercontent.com/PowerShell/openssh-portable/68ad673db4bf971b5c087cef19bb32953fd9db75/contrib/win32/openssh/OpenSSHUtils.psd1'
    Set-Variable OpenSshUtilsModule -Option Constant -Value 'https://raw.githubusercontent.com/PowerShell/openssh-portable/68ad673db4bf971b5c087cef19bb32953fd9db75/contrib/win32/openssh/OpenSSHUtils.psm1'

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
        
        Write-Host 'Installing sshd'
        
        Add-WindowsCapability -Online -Name OpenSSH.Client~~~~0.0.1.0
        Add-WindowsCapability -Online -Name OpenSSH.Server~~~~0.0.1.0

        Set-Service -Name ssh-agent -StartupType Automatic
        Set-Service -Name sshd -StartupType Automatic

        Write-Host 'Making PowerShell the default shell for ssh'

        New-Item 'HKLM:\SOFTWARE\OpenSSH' -Force | `
            New-ItemProperty -Name "DefaultShell" -Force -Value "$env:SystemRoot\system32\WindowsPowerShell\v1.0\powershell.exe"
        
        Write-Host 'Starting sshd'

        Start-Service ssh-agent
        Start-Service sshd

        # Update sshd_config to look in the right place for authorized_keys
        Write-Host 'Updating sshd_config'
        
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

        Write-Host 'Restarting sshd'

        Restart-Service ssh-agent
        Restart-Service sshd

        # Output the host key so it can be added to the agent's known_hosts file
        Write-Host -NoNewline '#DATA#'
        Get-Content -Encoding Utf8 "$env:ProgramData\ssh\ssh_host_rsa_key.pub" | ForEach-Object { Write-Host -NoNewline $_.Split()[0,1] }
        Write-Host -NoNewline '#DATA#'
    }

    Export-ModuleMember -Function Initialize-WindowsVM
}
