# 强制结束 NoFences 进程
Write-Host "Stopping NoFences processes..."
Stop-Process -Name "NoFences" -Force -ErrorAction SilentlyContinue

# 等待一小会儿确保进程结束
Start-Sleep -Seconds 1

# 定义要清理的路径
$localDataPath = "$env:LOCALAPPDATA\NoFences"
$roamingDataPath = "$env:APPDATA\NoFences"

# 清理 LocalAppData
if (Test-Path $localDataPath) {
    Write-Host "Removing Local Data: $localDataPath"
    Remove-Item -Path $localDataPath -Recurse -Force
} else {
    Write-Host "Local Data path not found: $localDataPath"
}

# 清理 AppData (Roaming)
if (Test-Path $roamingDataPath) {
    Write-Host "Removing Roaming Data: $roamingDataPath"
    Remove-Item -Path $roamingDataPath -Recurse -Force
} else {
    Write-Host "Roaming Data path not found: $roamingDataPath"
}

Write-Host "Cleanup complete."
