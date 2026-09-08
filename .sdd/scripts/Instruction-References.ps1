# Verification-only traversal of literal Markdown instruction references.
function Get-InstructionClosure {
    param([string]$RepositoryRoot, [string]$Source, [System.Collections.IDictionary]$SourceTexts)
    $pending=[Collections.Generic.Queue[string]]::new()
    $visited=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $files=[Collections.Generic.List[string]]::new()
    $missing=[Collections.Generic.List[string]]::new()
    $parts=[Collections.Generic.List[string]]::new()
    $pending.Enqueue($Source)
    while ($pending.Count) {
        $relative=$pending.Dequeue()
        if (-not $visited.Add($relative)) { continue }
        $absolute=Resolve-WorkspacePath $RepositoryRoot $relative
        if ($null -ne $SourceTexts) {
            if (-not $SourceTexts.Contains($relative)) { $missing.Add($relative); continue }
            $body=$SourceTexts[$relative]
        } else {
            if (-not (Test-Path -LiteralPath $absolute -PathType Leaf)) { $missing.Add($relative); continue }
            $body=Read-Utf8 $absolute
        }
        $files.Add($relative); $parts.Add($body)
        foreach ($match in [regex]::Matches($body,'(?<![A-Za-z0-9_./-])(?:\.sdd/(?:skills|roles|contracts)/|references/)[A-Za-z0-9_./-]+\.md\b')) {
            $reference=$match.Value
            if (-not $reference.StartsWith('.sdd/')) {
                $directory=[IO.Path]::GetDirectoryName($relative).Replace('\','/')
                $reference="$directory/$reference"
            }
            $pending.Enqueue($reference)
        }
    }
    [pscustomobject]@{files=@($files.ToArray()); missing=@($missing.ToArray()); text=($parts -join "`n")}
}
