function Set-ProcessEnvIfMissing {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    $current = [Environment]::GetEnvironmentVariable($Name, "Process")
    if ([string]::IsNullOrWhiteSpace($current)) {
        [Environment]::SetEnvironmentVariable($Name, $Value, "Process")
    }
}

function Initialize-WindowsEnvironmentDefaults {
    $userProfile = $env:USERPROFILE
    if ([string]::IsNullOrWhiteSpace($userProfile)) {
        throw "USERPROFILE is not set."
    }

    $systemDrive = if ([string]::IsNullOrWhiteSpace($env:SystemDrive)) { "C:" } else { $env:SystemDrive }
    $systemRoot = if ([string]::IsNullOrWhiteSpace($env:SystemRoot)) { "$systemDrive\WINDOWS" } else { $env:SystemRoot }
    $userName = Split-Path -Leaf $userProfile

    $defaults = @(
        @{ Name = "ProgramFiles"; Value = "$systemDrive\Program Files" },
        @{ Name = "ProgramFiles(x86)"; Value = "$systemDrive\Program Files (x86)" },
        @{ Name = "CommonProgramFiles"; Value = "$systemDrive\Program Files\Common Files" },
        @{ Name = "CommonProgramFiles(x86)"; Value = "$systemDrive\Program Files (x86)\Common Files" },
        @{ Name = "ALLUSERSPROFILE"; Value = "$systemDrive\ProgramData" },
        @{ Name = "ProgramData"; Value = "$systemDrive\ProgramData" },
        @{ Name = "APPDATA"; Value = "$userProfile\AppData\Roaming" },
        @{ Name = "LOCALAPPDATA"; Value = "$userProfile\AppData\Local" },
        @{ Name = "TEMP"; Value = "$userProfile\AppData\Local\Temp" },
        @{ Name = "TMP"; Value = "$userProfile\AppData\Local\Temp" },
        @{ Name = "HOMEDRIVE"; Value = $systemDrive },
        @{ Name = "HOMEPATH"; Value = "\Users\$userName" },
        @{ Name = "SystemDrive"; Value = $systemDrive },
        @{ Name = "SystemRoot"; Value = $systemRoot },
        @{ Name = "WINDIR"; Value = $systemRoot },
        @{ Name = "PUBLIC"; Value = "$systemDrive\Users\Public" }
    )

    foreach ($item in $defaults) {
        Set-ProcessEnvIfMissing -Name $item.Name -Value $item.Value
    }
}
