# Install Chocolatey

if(!(Test-Path "C:\ProgramData\chocolatey\bin\choco.exe" -PathType Leaf)) {
    Set-ExecutionPolicy Bypass -Scope Process -Force
    [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072
    iex ((New-Object System.Net.WebClient).DownloadString('https://chocolatey.org/install.ps1'))
}

$env:Path += ";C:\ProgramData\chocolatey\bin"

# Install ReSharper

choco install resharper -y

# Keep RDP session alive after disconnect

New-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\Terminal Server" -Name KeepAliveEnable -PropertyType DWORD -Value 1 -Force

# Map network drive

cmd.exe /C "cmdkey /add:`"<storageAccount>.file.core.windows.net`" /user:`"Azure\<storageAccount>`" /pass:`"<storageKey>`""
net use z: "\\<storageAccount>.file.core.windows.net\<shareName>" /persistent:yes