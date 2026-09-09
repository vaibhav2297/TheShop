# Shared evidence helpers. Requires PowerShell 7.
$script:SddStages=@('spec','plan','implement','test','verify','review','document')

function Get-SddFileHash([string]$Path) {
    $algorithm=[Security.Cryptography.SHA256]::Create()
    $stream=[IO.File]::OpenRead($Path)
    try { return ([BitConverter]::ToString($algorithm.ComputeHash($stream))).Replace('-','').ToLowerInvariant() }
    finally { $stream.Dispose(); $algorithm.Dispose() }
}

function Get-SddInputSnapshot([string]$Root,[string[]]$Paths,[string[]]$ExcludedDirectories=@()) {
    $result=[ordered]@{}
    foreach ($relative in @($Paths | Sort-Object -Unique)) {
        $path=Resolve-WorkspacePath $Root $relative
        if (-not (Test-Path -LiteralPath $path)) { $result[$relative.Replace('\','/')]=$null; continue }
        $item=Get-Item -LiteralPath $path -Force
        if ($item.PSIsContainer) {
            # PowerShell does not follow directory links without -FollowSymlink. Inspect
            # every returned entry before hashing; avoid resolving each parent repeatedly.
            $entries=@(Get-ChildItem -LiteralPath $path -Recurse -Force)
            if (@($entries | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count) { throw "Reparse point in evidence source: $relative" }
            $files=@($entries | Where-Object {-not $_.PSIsContainer})
        } else { $files=@($item) }
        foreach ($file in $files) {
            $name=$file.FullName.Substring($Root.TrimEnd('/','\').Length+1).Replace('\','/')
            # Exclude incidental outputs during directory traversal. Explicit files,
            # including retained test logs under bin, remain evidence inputs.
            if ($item.PSIsContainer) {
                if ($name -match '(^|/)(bin|obj|node_modules|\.git)(/|$)') { continue }
                if ($ExcludedDirectories -and @($ExcludedDirectories | Where-Object { $name.StartsWith($_.TrimEnd('/')+'/',[StringComparison]::OrdinalIgnoreCase) }).Count) { continue }
            }
            $result[$name]=Get-SddFileHash $file.FullName
        }
    }
    return $result
}

function Get-SddStagePaths([string]$Feature,[string]$Stage) {
    if ($script:SddStages -notcontains $Stage) { throw "Unknown evidence stage: $Stage" }
    $paths=@(".specs/$Feature/spec.md",'.sdd/contracts','.sdd/catalog.json','.sdd/scripts/check-sdd-gates.ps1','.sdd/scripts/check-design-rules.ps1','.sdd/scripts/Sdd-Evidence.ps1','.sdd/scripts/manage-sdd-evidence.ps1','.sdd/scripts/Common.ps1')
    # Track all canonical definitions: conservative invalidation across instruction changes.
    $paths+=@('.sdd/skills','.sdd/roles','.sdd/adapters','.sdd/extensions/registry.json')
    $paths+=@('.sdd/scripts/Test-Proof.ps1','.sdd/scripts/check-worker-scope.ps1','.sdd/scripts/format-changes.ps1')
    if ($Stage -ne 'spec') { $paths+=".specs/$Feature/plan.md" }
    if ($Stage -notin @('spec','plan')) { $paths+=@('src','supabase','Directory.Build.props','Directory.Packages.props','global.json','TheShop.sln','TheShop.slnx') }
    if ($Stage -in @('test','verify','review','document')) { $paths+='tests' }
    if ($Stage -in @('test','verify','review','document')) { $paths+=@(".specs/$Feature/test-manifest.json",".specs/$Feature/test-report.md") }
    if ($Stage -in @('verify','review','document')) { $paths+=@(".specs/$Feature/e2e-manifest.json",".specs/$Feature/e2e-report.md") }
    if ($Stage -in @('review','document')) { $paths+=".specs/$Feature/review-report.md" }
    return $paths
}

function Get-SddStageExclusions([string]$Stage) {
    # E2E writes belong to Verify; unit/component changes still invalidate Test.
    if ($Stage -eq 'test') { return @('tests/TheShop.E2E.Tests') }
    return @()
}

function Test-SddSnapshot($Expected,$Actual) {
    if ($Expected.Count -ne $Actual.Count) { return $false }
    foreach ($key in $Expected.Keys) {
        if (-not $Actual.Contains($key) -or $Expected[$key] -cne $Actual[$key]) { return $false }
    }
    return $true
}

function Get-SddStaleStages([string]$Root,[string]$Feature,$State) {
    $stale=[Collections.Generic.List[string]]::new()
    foreach ($failure in $State.failures) {
        if (-not $failure.Contains('resolved') -or -not $failure.resolved) {
            if (-not $stale.Contains($failure.report.stage)) { $stale.Add($failure.report.stage) }
        }
    }
    foreach ($stage in $script:SddStages) {
        if (-not $State.stages.Contains($stage)) { continue }
        $record=$State.stages[$stage]
        $excluded=if($record.Contains('excludedDirectories')){@($record.excludedDirectories)}else{@()}
        $current=Get-SddInputSnapshot $Root @($record.paths) $excluded
        if (($record.stale -or -not (Test-SddSnapshot $record.sources $current)) -and -not $stale.Contains($stage)) { $stale.Add($stage) }
    }
    return @($stale)
}

function Get-SddHandoffUpstream([string]$Role,[string]$Purpose='') {
    if ($Purpose) {
        if ($Purpose -cne 'implementation-tests' -or $Role -notin @('shop-test-writer','shop-test-runner')) { throw 'Unsupported handoff purpose for role.' }
        return 'plan'
    }
    switch ($Role) {
        'shop-test-writer' { return 'implement' }
        'shop-test-runner' { return 'implement' }
        'shop-code-quality-review' { return 'verify' }
        'shop-code-security-reviewer' { return 'verify' }
        'shop-code-documenter' { return 'review' }
        default { return 'plan' }
    }
}

function Get-SddDescendants([string]$Stage) {
    $index=[Array]::IndexOf($script:SddStages,$Stage)
    if ($index -lt 0) { throw "Unknown evidence stage: $Stage" }
    return @($script:SddStages | Select-Object -Skip ($index+1))
}

function Read-SddEvidence([string]$Root,[string]$Feature) {
    if ($Feature -notmatch '\A[a-z0-9]+(-[a-z0-9]+)*\z') { throw 'Feature must be one lowercase kebab-case segment.' }
    $path=Resolve-WorkspacePath $Root ".specs/$Feature/evidence/state.json"
    if (-not (Test-Path -LiteralPath $path)) { return [ordered]@{ schemaVersion=1; feature=$Feature; stages=[ordered]@{}; failures=@(); amendments=@() } }
    $state=[IO.File]::ReadAllText($path) | ConvertFrom-Json -AsHashtable
    if ($state.schemaVersion -ne 1 -or $state.feature -cne $Feature) { throw 'Unsupported or mismatched evidence state.' }
    return $state
}
