# GPU failure fixes — 2026-09-05

User authorized fixing unchecked GPU readback and sticky Auto fallback.
Both are implemented locally, alongside the prior uncommitted label fix,
GPU harness and investigation docs.

[Evidence and behavior contract](../02_Investigation/Gpu-Failure-Fixes-2026-09-05.md):
failed GPU readback now throws; repeated completion cannot return an invalid
bitmap. New rendering after abandoned-context surface failure still uses CPU.
Auto probes each call's context; no process-global negative cache remains.

Hardware regression checks failed before and all seven pass after. Two portable
propagation/cache tests bring the suite to 48/48. Release solution build has zero
warnings/errors, and packaging succeeds. All 37 PNGs are unchanged from the prior
label-fix run. Hardware exit remains 1 solely for the existing synthetic geometry
256px parity gate (11/12 image cases pass).

Next: diagnose pixel parity, then production host/thread and native resource
ownership. F-001/R-001 stay open for that broader scope. F-003 still has text
placement/multilingual gaps. No commits or GitHub issue publication performed.
