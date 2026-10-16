# Offline contract checks: intercept every gh call, never contact GitHub.
$ErrorActionPreference = 'Stop'
$testDir = Join-Path ([IO.Path]::GetTempPath()) ("PortManager-release-test-" + [guid]::NewGuid())
New-Item $testDir -ItemType Directory | Out-Null
foreach ($name in @('0xMalaz.PortManager-1.2.0-full.nupkg', 'releases.win.json', 'assets.win.json', 'PortManager-Setup.exe')) {
    Set-Content (Join-Path $testDir $name) 'test asset'
}
function gh {
    $call = $args -join ' '
    $global:PortManagerReleaseTestCalls.Add($call)
    $global:LASTEXITCODE = 0
    if ($call -like '*--paginate*') {
        if ($global:PortManagerReleaseTestScenario -eq 'query-failure') { $global:LASTEXITCODE = 1; return }
        $draft = $global:PortManagerReleaseTestScenario -ne 'published'
        ConvertTo-Json -Compress -InputObject @(@(@{ id = 123; tag_name = 'v1.2.0'; draft = $draft }))
    } elseif ($call -eq 'api repos/test/repo/git/ref/tags/v1.2.0 --silent') {
        if ($global:PortManagerReleaseTestScenario -eq 'missing-tag') { $global:LASTEXITCODE = 1 }
    } elseif ($call -like 'api --method POST repos/test/repo/releases *') {
        if ($global:PortManagerReleaseTestScenario -eq 'bad-create-response') { '{}'; return }
        ConvertTo-Json -Compress @{ id = 456; tag_name = 'v1.2.0'; draft = $true }
    } elseif ($call -eq 'api repos/test/repo/releases/123' -or $call -eq 'api repos/test/repo/releases/456') {
        ConvertTo-Json -Compress @{ id = 123; tag_name = 'v1.2.0'; draft = ($global:PortManagerReleaseTestScenario -ne 'published') }
    } elseif ($call -like 'release upload*' -and $global:PortManagerReleaseTestScenario -eq 'upload-failure') {
        $global:LASTEXITCODE = 1
    } elseif ($call -notlike 'release upload*' -and $call -notlike 'release create*' -and
        $call -notlike 'api --method DELETE*' -and $call -notlike 'api --method PATCH*') {
        throw "Unexpected gh call: $call"
    }
}
foreach ($scenario in @('published', 'query-failure', 'upload-failure', 'missing-tag', 'bad-create-response', 'retry-draft')) {
    $global:PortManagerReleaseTestScenario = $scenario
    $global:PortManagerReleaseTestCalls = [Collections.Generic.List[string]]::new()
    $failed = $false
    $failure = ''
    try { & "$PSScriptRoot/Publish-Release.ps1" -Tag v1.2.0 -Repository test/repo -OutputDir $testDir }
    catch { $failed = $true; $failure = $_.Exception.Message }
    if ($scenario -eq 'retry-draft') {
        if ($failed -or $global:PortManagerReleaseTestCalls[-1] -notlike 'api --method PATCH*') { throw 'Successful retry did not publish last.' }
        if (@($global:PortManagerReleaseTestCalls | Where-Object { $_ -like 'release upload*' }).Count -ne 4) { throw 'Not every asset was uploaded.' }
        if (@($global:PortManagerReleaseTestCalls | Where-Object { $_ -like 'api --method DELETE*' }).Count -ne 1) { throw 'Failed draft was not recreated.' }
        if (@($global:PortManagerReleaseTestCalls | Where-Object { $_ -like '*--paginate*' }).Count -ne 1) { throw 'Creation must not depend on a fresh release listing.' }
        if ($global:PortManagerReleaseTestCalls[-1] -notlike '*releases/456*') { throw 'Must publish the ID returned by creation.' }
    } else {
        if (!$failed) { throw "Expected $scenario to fail closed." }
        $expected = switch ($scenario) {
            'published' { 'Refusing to modify a published*' }
            'query-failure' { 'Cannot list releases*' }
            'upload-failure' { 'Upload failed:*' }
            'missing-tag' { 'Cannot verify the existing release tag.' }
            'bad-create-response' { 'Invalid draft creation response.' }
        }
        if ($failure -notlike $expected) { throw "Unexpected failure in ${scenario}: $failure" }
        if ($global:PortManagerReleaseTestCalls | Where-Object { $_ -like '*--method PATCH*' }) { throw 'Failure published a release.' }
        if ($scenario -in @('published', 'query-failure', 'missing-tag') -and
            ($global:PortManagerReleaseTestCalls | Where-Object { $_ -like '*--method*' -or $_ -like 'release *' })) { throw 'Protected release was mutated.' }
    }
    Write-Host "PASS: $scenario"
}
# Delete only the known files created above; no recursive directory deletion.
Get-ChildItem -LiteralPath $testDir -File | Remove-Item
Remove-Item -LiteralPath $testDir
$global:LASTEXITCODE = 0
Remove-Variable PortManagerReleaseTestCalls, PortManagerReleaseTestScenario -Scope Global
