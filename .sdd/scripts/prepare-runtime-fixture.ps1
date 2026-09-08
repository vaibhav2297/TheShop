# Build an isolated disposable workspace. Never copies project source or credentials.
[CmdletBinding()]
param([string]$RepositoryRoot=(Join-Path $PSScriptRoot '../..'))
. (Join-Path $PSScriptRoot 'Common.ps1')
$root=[IO.Path]::GetFullPath($RepositoryRoot)
$fixture=Resolve-WorkspacePath $root ('.sdd/.test-work/native-' + [Guid]::NewGuid().ToString('N'))
$pwsh=(Get-Process -Id $PID).Path
$generation=Invoke-Captured $pwsh @('-NoProfile','-File',(Join-Path $root '.sdd/scripts/sync-adapters.ps1'),'-RepositoryRoot',$root,'-OutputRoot',$fixture) $root
if ($generation.exitCode -ne 0) { throw $generation.stderr }
foreach ($directory in @('skills','roles','contracts','adapters','scripts')) {
    Copy-Item (Join-Path $root ".sdd/$directory") (Join-Path $fixture '.sdd') -Recurse
}
Copy-Item (Join-Path $root '.sdd/catalog.json') (Join-Path $fixture '.sdd/catalog.json')
# Native discovery is exercised with the real generated entry points. Scope is only this fixture.
Write-Utf8 (Join-Path $fixture 'codex-prompt.txt') @'
$theshop-spec sdd-portability-probe --desc A signed-in store admin can maintain a personal, session-only checklist of up to 5 labels for preparing a product photo shoot. Add a nonempty label of at most 40 characters, mark it done or undone, and remove it. Trim leading and trailing spaces. Reject duplicate labels ignoring case, empty labels, labels over the limit, and additions beyond 5 items with a clear message; preserve the existing list on rejection. Ending the session clears the checklist. Other users cannot see or change it. No sharing, notifications, scheduling, persistence, or product changes. English and French text, keyboard operation, visible focus, and screen-reader announcements for changes are required. All of these product choices are confirmed; log no unresolved assumptions.

This is an authorized isolated compatibility test. Work only in this fixture. Load the generated native skill and its contract; execute its normal spec and ledger steps. Run the actual spec gate. No production implementation, connectors, installing tools, git changes, or external mutations. Final response: artifact paths, actual gate result, and loaded skill/reference paths. Do not claim a tool ran unless it did.
'@
Write-Utf8 (Join-Path $fixture 'claude-prompt.txt') @'
/theshop.clarify sdd-portability-probe

This is an authorized isolated compatibility test. Resume the spec and status ledger created by Codex in this fixture. Invoke the legacy dotted skill alias and follow its generated native canonical workflow. Every product choice in the spec was confirmed by the user; there should be no unresolved assumptions. Run the actual shared spec gate, verify the result, and update only the existing spec/status artifacts as the clarify workflow requires. Work only in this fixture. No plan, implementation, connectors, installations, git actions, or external mutations. Report the loaded alias, canonical skill, reference paths, gate evidence, and next workflow. Do not claim a tool ran unless it did.
'@
Write-Output $fixture
