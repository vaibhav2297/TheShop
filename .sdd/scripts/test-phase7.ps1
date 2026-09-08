# Phase 7 preservation proof. Checks refactoring against its own immutable snapshot.
[CmdletBinding()]
param([string]$RepositoryRoot=(Join-Path $PSScriptRoot '../..'))
. (Join-Path $PSScriptRoot 'Common.ps1')
. (Join-Path $PSScriptRoot 'Instruction-References.ps1')
$root=[IO.Path]::GetFullPath($RepositoryRoot)
$inventory=Get-Content (Join-Path $root '.sdd/baseline/phase7/inventory.json') -Raw | ConvertFrom-Json -AsHashtable
$archive=Join-Path $root ('.sdd/baseline/phase7/'+$inventory.archive)
$checks=[Collections.Generic.List[object]]::new()
function Assert([string]$Name,[bool]$Passed,[string]$Evidence) {
    $checks.Add([ordered]@{name=$Name;passed=$Passed;evidence=$Evidence})
    $label=if($Passed){'PASS'}else{'FAIL'}
    Write-Output "[$label] $Name"
}
Assert 'Phase 7 archive hash matches inventory' ((Get-FileHash $archive).Hash.ToLowerInvariant() -ceq $inventory.archiveSha256) $inventory.archive
if (-not $checks[0].passed) { throw 'Untrusted baseline.' }
$before=@{}
$zip=[IO.Compression.ZipFile]::OpenRead($archive)
try {
    foreach ($entry in $zip.Entries) {
        if ($entry.FullName -match '\.(md|ps1|json|yml)$') {
            $reader=[IO.StreamReader]::new($entry.Open(),[Text.Encoding]::UTF8)
            try { $before[$entry.FullName]=$reader.ReadToEnd().Replace("`r`n","`n") } finally { $reader.Dispose() }
        }
    }
} finally { $zip.Dispose() }
$catalog=Get-Content (Join-Path $root '.sdd/catalog.json') -Raw | ConvertFrom-Json -AsHashtable
$entries=@($catalog.skills)+@($catalog.roles)
Assert 'Core catalog and invocation/ownership metadata unchanged' ((Read-Utf8 (Join-Path $root '.sdd/catalog.json')) -ceq $before['.sdd/catalog.json']) "$($catalog.skills.Count) workflows; $($catalog.roles.Count) roles."
$protectedDrift=@($inventory.protectedFiles | Where-Object {
    $path=Resolve-WorkspacePath $root $_.path
    -not (Test-Path $path) -or (Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant() -cne $_.sha256
})
Assert 'Application, tests, feature records, and historical checkpoints preserved' ($protectedDrift.Count -eq 0 -and $inventory.protectedFiles.Count -ge 535) "$($inventory.protectedFiles.Count) protected files; changed: $((@($protectedDrift | ForEach-Object { $_.path })) -join ', ')"
$frozen=@($before.Keys | Where-Object {
    $_ -match '^\.sdd/(contracts/|adapters/|extensions/|skills/.+/templates/)' -or
    $_ -in @('.sdd/scripts/check-sdd-gates.ps1','.sdd/scripts/check-design-rules.ps1','.sdd/scripts/format-changes.ps1')
})
$frozenDrift=@($frozen | Where-Object { (Read-Utf8 (Join-Path $root $_)) -cne $before[$_] })
Assert 'Policies, adapters, extensions, templates, gates, and formatter unchanged' ($frozenDrift.Count -eq 0) ($frozenDrift -join ', ')
$metrics=@(); $lost=@(); $unchanged=@(); $newMissing=@()
foreach ($entry in $entries) {
    $prior=$before[$entry.source]
    $current=Read-Utf8 (Join-Path $root $entry.source)
    $closure=Get-InstructionClosure -RepositoryRoot $root -Source $entry.source
    $priorClosure=Get-InstructionClosure -RepositoryRoot $root -Source $entry.source -SourceTexts $before
    if ($current -ceq $prior) { $unchanged += $entry.id }
    # Exact fences survive relocation; duplicate occurrences need only one authoritative copy.
    foreach ($block in [regex]::Matches($prior,'(?ms)^[ \t]*```[^\n]*\n.*?^[ \t]*```[^\n]*')) {
        if (-not $closure.text.Contains($block.Value)) { $lost += "$($entry.id): $($block.Value.Split("`n")[0])" }
    }
    # Compare all missing references against baseline; no filename whitelist may hide a broken dependency.
    foreach ($path in $closure.missing) {
        if ($path -notin $priorClosure.missing) { $newMissing += $path }
    }
    $metrics += [pscustomobject][ordered]@{id=$entry.id;beforeEntryBytes=[Text.Encoding]::UTF8.GetByteCount($prior);afterEntryBytes=[Text.Encoding]::UTF8.GetByteCount($current);reachableFiles=$closure.files.Count}
}
Assert 'Every existing workflow and role has an authored refactor' ($entries.Count -eq 25 -and $unchanged.Count -eq 0) ($unchanged -join ', ')
Assert 'Exact command, API, schema, and report fences remain reachable' ($lost.Count -eq 0) ($lost -join ', ')
Assert 'New conditional references resolve' ($newMissing.Count -eq 0) (($newMissing | Select-Object -Unique) -join ', ')
$beforeBytes=($metrics | Measure-Object beforeEntryBytes -Sum).Sum
$afterBytes=($metrics | Measure-Object afterEntryBytes -Sum).Sum
Assert 'Combined entry definitions are smaller' ($afterBytes -lt $beforeBytes) "$beforeBytes to $afterBytes UTF-8 bytes; not token or billing measurements."
$governance=Read-Utf8 (Join-Path $root '.sdd/skills/theshop-constitution/SKILL.md')
$ruleLines=@([regex]::Matches($before['.sdd/skills/theshop-constitution/SKILL.md'],'(?m)^\d+\. \[.+$') | ForEach-Object { $_.Value })
Assert 'All numbered constitution rules preserved verbatim' ($ruleLines.Count -eq 30 -and @($ruleLines | Where-Object { -not $governance.Contains($_) }).Count -eq 0) "$($ruleLines.Count) rules."
# Exercise traversal with a cycle and missing reference in an isolated fixture.
$fixture=Resolve-WorkspacePath $root ('.sdd/.test-work/phase7-reference-'+[Guid]::NewGuid().ToString('N'))
Write-Utf8 (Join-Path $fixture '.sdd/skills/probe/SKILL.md') 'Read `references/a.md`.'
Write-Utf8 (Join-Path $fixture '.sdd/skills/probe/references/a.md') 'Read `.sdd/skills/probe/SKILL.md` and `.sdd/skills/probe/references/missing.md`.'
$probe=Get-InstructionClosure -RepositoryRoot $fixture -Source '.sdd/skills/probe/SKILL.md'
Assert 'Reference traversal handles cycles and reports missing files' ($probe.files.Count -eq 2 -and $probe.missing.Count -eq 1 -and $probe.missing[0].EndsWith('/missing.md')) ($probe.missing -join ', ')
$oldProbe=@{'.sdd/skills/probe/SKILL.md'='Read `references/a.md`.';'.sdd/skills/probe/references/a.md'='Read `.sdd/skills/probe/SKILL.md` and `.sdd/skills/probe/references/missing.md`.'}
$priorProbe=Get-InstructionClosure -RepositoryRoot $fixture -Source '.sdd/skills/probe/SKILL.md' -SourceTexts $oldProbe
Write-Utf8 (Join-Path $fixture '.sdd/skills/probe/references/a.md') ($oldProbe['.sdd/skills/probe/references/a.md']+' Read `.sdd/skills/probe/references/required-context.md`.')
$changedProbe=Get-InstructionClosure -RepositoryRoot $fixture -Source '.sdd/skills/probe/SKILL.md'
$addedMissing=@($changedProbe.missing | Where-Object { $_ -notin $priorProbe.missing })
Assert 'New missing references fail regardless of filename' ($addedMissing.Count -eq 1 -and $addedMissing[0].EndsWith('/required-context.md')) ($addedMissing -join ', ')
# Rehearse archive recovery in a fresh workspace; never restore over the user's working tree.
$restoreRoot=Resolve-WorkspacePath $root ('.sdd/.test-work/phase7-restore-'+[Guid]::NewGuid().ToString('N'))
$zip=[IO.Compression.ZipFile]::OpenRead($archive)
try {
    foreach ($entry in $zip.Entries) {
        $destination=Resolve-WorkspacePath $restoreRoot $entry.FullName
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$destination)
    }
} finally { $zip.Dispose() }
$restoreDrift=@($inventory.files | Where-Object { (Get-FileHash -LiteralPath (Join-Path $restoreRoot $_.path)).Hash.ToLowerInvariant() -cne $_.sha256 })
Assert 'Archive recovery recreates every original managed file' ($restoreDrift.Count -eq 0 -and $inventory.files.Count -ge 300) "$($inventory.files.Count) files restored in isolated fixture."
$baselineTarget=Join-Path $restoreRoot '.sdd/baseline/inventory.json'
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($baselineTarget)) | Out-Null
Copy-Item -LiteralPath (Join-Path $root '.sdd/baseline/inventory.json') -Destination $baselineTarget
$restoredGeneration=Invoke-Captured (Get-Process -Id $PID).Path @('-NoProfile','-File',(Join-Path $restoreRoot '.sdd/scripts/sync-adapters.ps1'),'-RepositoryRoot',$restoreRoot,'-Check') $restoreRoot
Assert 'Recovered pre-phase-7 adapters remain internally consistent' ($restoredGeneration.exitCode -eq 0) ($restoredGeneration.stdout+$restoredGeneration.stderr)
$failed=@($checks | Where-Object { -not $_.passed })
Write-Json (Join-Path $root '.sdd/reports/phase7-static.json') ([ordered]@{testedUtc=[DateTime]::UtcNow.ToString('o');passed=$failed.Count -eq 0;checks=@($checks.ToArray());metrics=$metrics;beforeEntryBytes=$beforeBytes;afterEntryBytes=$afterBytes})
Write-Output "[phase7] $($checks.Count-$failed.Count)/$($checks.Count) checks passed."
if ($failed.Count) { exit 1 }
