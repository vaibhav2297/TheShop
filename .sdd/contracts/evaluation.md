# Execution and optimization decisions

Production defaults stay explicit: layered implementation via `theshop-implement`; single-context alternative via `theshop-execute`. Test writer/runner remain separate under `theshop-test`; `theshop-test-merged` remains separately invoked. An evaluation cannot silently switch active workflow or remove review independence.

Run native pilots with `.sdd/scripts/test-sdd-native.ps1 -Runtime claude|codex -Case routing|single|layered|test-merged|test-separate`. Use PowerShell 7. Each case gets isolated temporary workspace, raw event logs, exact oracle results, scope checks, elapsed time, runtime-reported usage, and model identities when emitted. `-Repeat` distinguishes samples; no automatic retry or model substitution. A blocked model is environment evidence, not a failed architecture.

Current execution fixture tests four CommonJS boundaries and rejection semantics. Separate contexts run sequentially; full parallel SDD implementation and production integrations are outside this pilot. Testing pilots require both defect detection and passing the corrected behavior. Never promote a workflow default from timing alone, one sample, a different model, or a synthetic fixture.

Before changing defaults for a task class, collect paired real SDD runs in both clients with matching model/settings and source revision. Include at least three representative tasks and repeated runs. Compare correctness, scope, retries, human corrections, elapsed time, and total reported usage. Include all failed attempts. Keep cached input, uncached input, and output separately; never call bytes tokens or compare incompatible billing fields as costs. Retain old default when quality regresses or evidence is inconclusive.

Run `.sdd/scripts/test-optimization.ps1` for offline tool-output, memory, and loopback proxy candidates. Originals and restored bytes remain available. No global memory file, credentials, network endpoint, or live model route changes. Candidate adoption requires measured workload benefit, exact literals/ordering/negation, unchanged gate results, model-level quality proof, and documented reversal. Local proxy fidelity does not prove streaming, cancellation, authentication, provider routing, or Cloud compatibility.

Optimization reports are evidence, not instructions. Keep maintenance separately requested. A failed or inconclusive trial ends with no adoption; do not install a proxy or weaken acceptance to obtain a saving.
