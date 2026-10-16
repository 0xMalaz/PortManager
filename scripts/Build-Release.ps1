[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+$')][string] $Version,
    [string] $OutputDir = 'artifacts/releases',
    [string] $PreviousPackage,
    [switch] $LocalFeedTest
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    [xml] $project = Get-Content src/PortManager.App/PortManager.App.csproj
    $projectVersion = @($project.Project.PropertyGroup.Version | Where-Object { $_ })[0]
    if (!$LocalFeedTest -and $Version -cne $projectVersion) { throw "Version $Version does not match project $projectVersion." }
    $sdkVersion = ($project.Project.ItemGroup.PackageReference | Where-Object Include -eq 'Velopack').Version
    $cliVersion = (Get-Content .config/dotnet-tools.json -Raw | ConvertFrom-Json).tools.vpk.version
    if ($sdkVersion -ne $cliVersion) { throw 'Velopack SDK and CLI versions must match.' }
    # Never mix stale packages with a new build. No recursive deletion is needed.
    if ((Test-Path $OutputDir) -and (Get-ChildItem $OutputDir -Force)) { throw "Output directory must be empty: $OutputDir" }
    New-Item $OutputDir -ItemType Directory -Force | Out-Null
    if ($PreviousPackage) {
        if ((Split-Path $PreviousPackage -Leaf) -notmatch '^0xMalaz\.PortManager-(\d+\.\d+\.\d+)-full\.nupkg$') {
            throw 'PreviousPackage must be a full PortManager release package.'
        }
        if ([version]$Matches[1] -ge [version]$Version) { throw 'Previous package must be older than the new version.' }
        Copy-Item -LiteralPath $PreviousPackage -Destination $OutputDir
    }
    $publishDir = Join-Path $root ("artifacts/publish/" + [guid]::NewGuid())
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'Tool restore failed.' }
    $localFeed = if ($LocalFeedTest) { 'true' } else { 'false' }
    $symbols = dotnet msbuild src/PortManager.App/PortManager.App.csproj -nologo -p:Configuration=Release `
        "-p:EnableLocalUpdateFeed=$localFeed" -getProperty:DefineConstants
    if ($LASTEXITCODE -ne 0) { throw 'Build symbol evaluation failed.' }
    if (((($symbols -join '') -split ';') -contains 'LOCAL_UPDATE_FEED') -ne [bool]$LocalFeedTest) {
        throw 'Local-feed build symbols do not match the requested package mode.'
    }
    dotnet publish src/PortManager.App/PortManager.App.csproj -c Release -r win-x64 --self-contained true `
        "-p:Version=$Version" "-p:EnableLocalUpdateFeed=$localFeed" -o $publishDir
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    # Permanent identity; settings live in HKCU\Software\0xMalaz\PortManager, outside this installation.
    dotnet vpk pack --packId 0xMalaz.PortManager --packVersion $Version --packTitle 'Port Manager' `
        --packAuthors 0xMalaz --packDir $publishDir --mainExe PortManager.exe --runtime win-x64 `
        --icon src/PortManager.App/Assets/PortManager.ico --shortcuts StartMenuRoot --outputDir $OutputDir
    if ($LASTEXITCODE -ne 0) { throw 'Packaging failed.' }
    Copy-Item (Join-Path $OutputDir '0xMalaz.PortManager-win-Setup.exe') (Join-Path $OutputDir 'PortManager-Setup.exe')
}
finally { Pop-Location }
