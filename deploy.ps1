# deploy.ps1

$projectName = "akabeko"
$projectPath = "c:\Users\houta\OneDrive\document\code\game\akabeko"
$buildPath = "$projectPath\Builds\WebGL"
$unityPath = "C:\Program Files\Unity\Hub\Editor\6000.3.2f1-x86_64\Editor\Unity.exe"
$repoUrl = "https://github.com/atjgmj/akabeko001"

Write-Host "=== 1. Clear Unity Lockfile ===" -ForegroundColor Cyan
$lockFile = "$projectPath\Temp\UnityLockfile"
if (Test-Path $lockFile) {
    Remove-Item $lockFile -Force -ErrorAction SilentlyContinue
    Write-Host "UnityLockfile deleted." -ForegroundColor Yellow
}

Write-Host "=== 2. Starting WebGL Build ===" -ForegroundColor Cyan
Write-Host "Building WebGL via Unity. This will take a few minutes..." -ForegroundColor Yellow

$logFile = "$projectPath\Logs\build.log"
if (Test-Path $logFile) { Remove-Item $logFile -Force }

$process = Start-Process -FilePath $unityPath -ArgumentList "-quit", "-batchmode", "-projectPath", $projectPath, "-executeMethod", "Akabeko.Editor.BuildScript.PerformWebGLBuild", "-logFile", $logFile -NoNewWindow -PassThru -Wait

if ($process.ExitCode -ne 0) {
    Write-Host "Unity exited with error code: $($process.ExitCode)" -ForegroundColor Red
    exit 1
}

if (Test-Path $logFile) {
    $logContent = Get-Content $logFile
    if ($logContent -match "WebGL Build Succeeded") {
        Write-Host "Unity build completed successfully!" -ForegroundColor Green
    } else {
        Write-Host "Build failed. Check Logs/build.log for details." -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "Build log not found." -ForegroundColor Red
    exit 1
}

Write-Host "=== 2.5. Blacken WebGL Background ===" -ForegroundColor Cyan
$styleCss = "$buildPath\TemplateData\style.css"
if (Test-Path $styleCss) {
    Add-Content -Path $styleCss -Value "`nbody, #unity-container, #unity-canvas { background: #000 !important; }"
    Write-Host "Blackened WebGL background in style.css" -ForegroundColor Green
}

Write-Host "=== 3. Deploy to GitHub Pages ===" -ForegroundColor Cyan

if (-not (Test-Path $buildPath)) {
    Write-Host "Build directory not found: $buildPath" -ForegroundColor Red
    exit 1
}

Push-Location $buildPath

if (Test-Path ".git") {
    Get-ChildItem -Path ".git" -Recurse -Force | Remove-Item -Force -Recurse
    Remove-Item ".git" -Force -Recurse -ErrorAction SilentlyContinue
}

git init
git checkout -B gh-pages
git remote add origin $repoUrl
git add -A
git commit -m "Deploy WebGL Build [skip ci]"

Write-Host "Pushing to GitHub..." -ForegroundColor Yellow
git push origin gh-pages --force

Write-Host "=== Deploy Complete ===" -ForegroundColor Green
Write-Host "URL: https://atjgmj.github.io/akabeko001/" -ForegroundColor Green

Pop-Location
