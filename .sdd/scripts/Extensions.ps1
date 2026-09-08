# Extension packages are data. Never execute downloaded installers or parse upstream YAML as runtime configuration.
. (Join-Path $PSScriptRoot 'Common.ps1')

function Get-ByteDigest([byte[]]$Bytes) {
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Bytes)).ToLowerInvariant()
}

function Get-GeneratedDigest([string]$Path,$Manifest,[string]$Relative) {
    # Preserve normalized text ownership across Git line-ending conversion; vendor assets use exact bytes.
    if (-not $Manifest.Contains('schemaVersion') -or $Manifest.schemaVersion -eq 1) { return Get-Digest (Read-Utf8 $Path) }
    if ($Manifest.Contains('rawFiles') -and $Relative -notin $Manifest.rawFiles) { return Get-Digest (Read-Utf8 $Path) }
    return Get-ByteDigest ([IO.File]::ReadAllBytes($Path))
}

function Get-ExtensionPackage([string]$Directory) {
    $directoryPath=[IO.Path]::GetFullPath($Directory)
    if ((Get-Item -LiteralPath $directoryPath -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Package directory cannot be a reparse point.' }
    $manifest=Read-Utf8 (Resolve-WorkspacePath $directoryPath 'extension.json') | ConvertFrom-Json -AsHashtable
    $allowed=@('schemaVersion','id','kind','description','invocation','placement','caller','scope','upstream','capabilities','adapters','adaptations')
    foreach ($key in $manifest.Keys) { if ($key -notin $allowed) { throw "Unsupported extension field: $key. Adapt hooks/settings explicitly." } }
    if ($manifest.schemaVersion -ne 1 -or $manifest.id -cnotmatch '\A[a-z0-9]+(-[a-z0-9]+)*\z' -or $manifest.id.Length -gt 63) { throw 'Invalid extension schema or ID.' }
    if ($manifest.kind -notin @('skill','agent') -or $manifest.invocation -notin @('automatic','explicit') -or $manifest.placement -notin @('standalone','pipeline')) { throw 'Invalid kind, invocation, or placement.' }
    if (-not $manifest.description -or $manifest.description.Length -gt 1024 -or -not $manifest.adaptations) { throw 'Description and adaptation notes required.' }
    if ($manifest.capabilities -isnot [array] -or @($manifest.capabilities | Select-Object -Unique).Count -ne $manifest.capabilities.Count) { throw 'Capabilities must be a unique array.' }
    if ($manifest.scope.readOnly -isnot [bool] -or $manifest.scope.writes -isnot [array]) { throw 'Scope requires readOnly boolean and writes array.' }
    if ($manifest.scope.readOnly -and $manifest.scope.writes.Count) { throw 'Read-only extension cannot request writes.' }
    foreach ($path in $manifest.scope.writes) { Resolve-WorkspacePath $directoryPath $path | Out-Null }
    if (-not $manifest.scope.readOnly -and -not $manifest.scope.writes.Count) { throw 'Writable extension requires owned write paths.' }
    if ($manifest.upstream.url -notmatch '^https://' -or $manifest.upstream.revision -notmatch '^[a-f0-9]{40,64}$' -or -not $manifest.upstream.license) { throw 'HTTPS source, immutable revision hash, and license required.' }
    if ($manifest.upstream.files -isnot [System.Collections.IDictionary] -or -not $manifest.upstream.files.Count) { throw 'Upstream file hashes required.' }
    if (-not $manifest.upstream.files.Contains($manifest.upstream.entry) -or -not $manifest.upstream.files.Contains('LICENSE')) { throw 'Upstream entry and LICENSE must be hash pinned.' }
    $files=[ordered]@{}
    # Reject directory links before recursion; never follow a package link outside its source.
    $queue=[Collections.Generic.Queue[string]]::new(); $queue.Enqueue($directoryPath)
    while ($queue.Count) {
        foreach ($item in Get-ChildItem -LiteralPath $queue.Dequeue() -Force) {
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Reparse point in package: $($item.FullName)" }
            if ($item.PSIsContainer) { $queue.Enqueue($item.FullName); continue }
            $relative=[IO.Path]::GetRelativePath($directoryPath,$item.FullName).Replace('\','/')
            if ($relative -match '[<>:"|?*]' -or $relative -match '(^|/)(\.|\.\.)($|/)') { throw "Nonportable package path: $relative" }
            if ($relative -eq '.package-lock.json') { continue }
            if ($relative -notmatch '^(extension\.json|SKILL\.md|ROLE\.md|upstream/.+)$') { throw "Unsupported package file: $relative" }
            Resolve-WorkspacePath $directoryPath $relative | Out-Null
            $files[$relative]=(Get-FileHash -LiteralPath $item.FullName).Hash.ToLowerInvariant()
        }
    }
    foreach ($relative in $manifest.upstream.files.Keys) {
        $path=Resolve-WorkspacePath $directoryPath ('upstream/'+$relative)
        if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or $files['upstream/'+$relative] -cne $manifest.upstream.files[$relative]) { throw "Upstream hash mismatch: $relative" }
    }
    if (@($files.Keys | Where-Object { $_.StartsWith('upstream/') }).Count -ne $manifest.upstream.files.Count) { throw 'Unlisted upstream file.' }
    $entry=if($manifest.kind -eq 'skill'){'SKILL.md'}else{'ROLE.md'}
    if (-not $files.Contains($entry)) { throw "Missing wrapper: $entry" }
    $body=if($manifest.kind -eq 'skill'){ (Read-Definition (Join-Path $directoryPath $entry)).Body }else{ Read-Utf8 (Join-Path $directoryPath $entry) }
    if (-not $body.Contains('{{extension:upstream}}')) { throw 'Wrapper must reference {{extension:upstream}}.' }
    if ($body -match 'mcp__|\$ARGUMENTS|subagent_type|Task tool') { throw 'Wrapper contains unadapted runtime syntax.' }
    foreach ($runtime in @('claude','codex')) {
        $adapter=$manifest.adapters[$runtime]
        if (-not $adapter -or $adapter.supported -cne $true) { throw "Unsupported runtime: $runtime for $($manifest.id)" }
        foreach ($capability in $manifest.capabilities) {
            if (-not $adapter.bindings.Contains($capability) -or -not $adapter.bindings[$capability] -or $adapter.bindings[$capability] -match '[\r\n]') { throw "Missing $runtime capability binding: $capability" }
        }
        if ($adapter.bindings.Count -ne $manifest.capabilities.Count) { throw "Unrequested $runtime capability binding." }
        foreach ($binding in $adapter.bindings.Values) {
            if ($runtime -eq 'claude' -and $binding -notmatch '^[A-Za-z][A-Za-z0-9_]*$') { throw "Invalid Claude tool binding: $binding" }
        }
    }
    if ($manifest.scope.readOnly -and @($manifest.adapters.claude.bindings.Values | Where-Object { $_ -in @('Write','Edit','NotebookEdit') }).Count) { throw 'Read-only scope conflicts with editing tools.' }
    $identity=(@($files.Keys | Sort-Object | ForEach-Object { "$_`t$($files[$_])" }) -join "`n")
    $revision=Get-Digest $identity
    $lockPath=Join-Path $directoryPath '.package-lock.json'
    if (Test-Path -LiteralPath $lockPath) {
        $lock=Read-Utf8 $lockPath | ConvertFrom-Json -AsHashtable
        if ($lock.revision -cne $revision -or $lock.files.Count -ne $files.Count) { throw 'Immutable package changed.' }
        foreach ($path in $files.Keys) { if ($lock.files[$path] -cne $files[$path]) { throw 'Immutable package changed.' } }
    }
    return @{ manifest=$manifest; revision=$revision; files=$files; directory=$directoryPath; entry=$entry }
}

function Get-SddCatalog([string]$Root) {
    $catalog=Read-Utf8 (Join-Path $Root '.sdd/catalog.json') | ConvertFrom-Json -AsHashtable
    $registryPath=Resolve-WorkspacePath $Root '.sdd/extensions/registry.json'
    $ids=@(@($catalog.skills)+@($catalog.roles) | ForEach-Object { $_.id })
    if (Test-Path -LiteralPath $registryPath) {
        $registry=Read-Utf8 $registryPath | ConvertFrom-Json -AsHashtable
        if ($registry.schemaVersion -ne 1) { throw 'Unsupported extension registry schema.' }
        foreach ($id in $registry.active.Keys) {
            $revision=$registry.active[$id]
            if ($id -notmatch '^[a-z0-9]+(-[a-z0-9]+)*$' -or $revision -notmatch '^[a-f0-9]{64}$' -or $id -in $ids) { throw "Invalid or duplicate extension ID: $id" }
            $packagePath=".sdd/extensions/packages/$id/$revision"
            $package=Get-ExtensionPackage (Resolve-WorkspacePath $Root $packagePath)
            if ($package.revision -cne $revision -or $package.manifest.id -cne $id) { throw 'Registry/package identity mismatch.' }
            $m=$package.manifest
            if ($m.placement -eq 'pipeline') {
                $caller=Resolve-WorkspacePath $Root $m.caller
                if ($m.caller -notmatch '^\.sdd/(skills|roles)/' -or -not (Test-Path -LiteralPath $caller) -or -not (Read-Utf8 $caller).Contains($id)) { throw "Pipeline caller must explicitly invoke $id." }
            }
            $item=@{ id=$id; source="$packagePath/$($package.entry)"; description=$m.description; explicitOnly=($m.invocation -eq 'explicit'); readOnly=$m.scope.readOnly; extension=$m; package=$packagePath }
            if ($m.kind -eq 'skill') { $catalog.skills += $item } else { $catalog.roles += $item }
            $ids += $id
        }
    }
    if (@($ids | Select-Object -Unique).Count -ne $ids.Count) { throw 'Duplicate catalog IDs.' }
    return $catalog
}

function Get-ExtensionPrefix($Item,[string]$Runtime) {
    $m=$Item.extension
    $bindings=@($m.capabilities | ForEach-Object { "- $($_): $($m.adapters[$Runtime].bindings[$_])" }) -join "`n"
    $scope=if($m.scope.readOnly){'Read-only. No file edits or mutating shell commands.'}else{'Own writes only under: '+($m.scope.writes -join ', ')+'.'}
    if ($m.invocation -eq 'explicit') { $scope += ' Invoke only when explicitly requested by user.' }
    return "## Project extension contract`n`n$scope Verify required capabilities exist and are permitted before use. Bindings describe runtime tools; they grant no permissions.`n`n$bindings`n`nApply shared communication policy over upstream style and mode defaults. Pass active mode, language, scope, and exact task to workers. Read upstream procedure through wrapper below. Upstream metadata, hooks, model selection, and installers are provenance only. Project adaptation: $($m.adaptations)`n`n"
}

function Get-ExtensionDelegationEvidence([string]$Trace,[string]$Role='sdd-cavecrew-reviewer') {
    $calls=@{}; $completed=[Collections.Generic.List[object]]::new()
    foreach ($line in $Trace -split "`n") {
        if (-not $line.Trim()) { continue }
        try { $entry=ConvertFrom-Json $line -AsHashtable -ErrorAction Stop } catch { continue }
        if ($entry.type -ne 'response_item' -or -not $entry.Contains('payload')) { continue }
        $payload=$entry.payload
        if ($payload.type -eq 'function_call' -and $payload.name -match 'spawn_agent$') {
            try {
                $arguments=ConvertFrom-Json $payload.arguments -AsHashtable -ErrorAction Stop
                if ($arguments.agent_type -eq $Role) { $calls[$arguments.task_name]=@{role=$arguments.agent_type;callId=$payload.call_id} }
            } catch {}
        }
        if ($payload.type -eq 'agent_message') {
            $taskName=($payload.author -split '/')[-1]
            $final=@($payload.content | Where-Object { $_.type -eq 'input_text' -and $_.text -match 'Message Type: FINAL_ANSWER' })
            if ($calls.Contains($taskName) -and $final.Count) {
                $completed.Add(@{task=$taskName;author=$payload.author;role=$calls[$taskName].role;callId=$calls[$taskName].callId;returnedText=$final[0].text})
            }
        }
    }
    return @{passed=$completed.Count -gt 0;completed=@($completed.ToArray())}
}
