# Kiem tra app batchInput_wpf co dang chay khong, neu khong thi mo lai.
# Chay dinh ky bang Task Scheduler (xem huong dan dang ky ben duoi file nay).
param(
    [string]$ProcessName = 'batchInput_wpf',
    [Parameter(Mandatory = $true)][string]$AppPath,   # duong dan .exe hoac shortcut ClickOnce (.appref-ms)
    [string]$LogPath = 'C:\BatchInput_HeatGuide_config\watchdog.log'
)

function Write-Log($message) {
    $dir = Split-Path $LogPath
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    ('[{0:yyyy-MM-dd HH:mm:ss}] {1}' -f (Get-Date), $message) | Add-Content -Path $LogPath -Encoding UTF8
}

# App dang chay -> khong lam gi
if (Get-Process -Name $ProcessName -ErrorAction SilentlyContinue) { exit 0 }

if (-not (Test-Path $AppPath)) {
    Write-Log "Khong tim thay file de mo: $AppPath"
    exit 1
}

Write-Log "App '$ProcessName' khong chay -> mo lai: $AppPath"
try {
    if ($AppPath -like '*.exe') {
        Start-Process -FilePath $AppPath -WorkingDirectory (Split-Path $AppPath)
    }
    else {
        Start-Process -FilePath $AppPath
    }
}
catch {
    Write-Log "Mo app that bai: $($_.Exception.Message)"
    exit 1
}

<#
DANG KY TASK (chay 1 lan, bang PowerShell tren server, dang nhap dung tai khoan dang chay app):

$script = 'C:\BatchInput_config\watchdog.ps1'
$app    = 'C:\DUONG\DAN\TOI\batchInput_wpf.exe'     # hoac file .appref-ms trong Start Menu

$action    = New-ScheduledTaskAction -Execute 'powershell.exe' `
             -Argument "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$script`" -AppPath `"$app`""
$trigger   = New-ScheduledTaskTrigger -Once -At (Get-Date) `
             -RepetitionInterval (New-TimeSpan -Minutes 5) -RepetitionDuration (New-TimeSpan -Days 3650)
$principal = New-ScheduledTaskPrincipal -UserId "$env:USERDOMAIN\$env:USERNAME" -LogonType Interactive -RunLevel Limited
$settings  = New-ScheduledTaskSettingsSet -MultipleInstances IgnoreNew -StartWhenAvailable

Register-ScheduledTask -TaskName 'BatchInput_Watchdog' -Action $action -Trigger $trigger `
                       -Principal $principal -Settings $settings

Go bo:  Unregister-ScheduledTask -TaskName 'BatchInput_Watchdog' -Confirm:$false
#>
