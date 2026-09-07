# GPU parity investigation — 2026-09-05

The user requested diagnosis of the remaining GPU parity failure.
[Report](../02_Investigation/Gpu-Parity-2026-09-05.md) and
[F-009](../02_Investigation/Findings/F-009.md) capture the evidence.

Identical SKPicture commands reproduce both backend outputs. Synthetic error
comes mainly from antialiased strokes; Colorado combines landcover, roads and
street-label differences. A raw translucent rectangle also shows a two-level
green-channel difference. No attribution to a precise native Skia routine yet.

19 capture pairs match production; 57 GPU replays repeat exactly. Direct stroke
AA/width/outline and rectangle-opacity controls isolate the behaviors. Requested
4/8-sample surfaces do not pass either complete fixture. Original gates remain.

Added `--parity` diagnostic mode only; no production rendering/tolerance changes.
Full Release solution build: zero warnings/errors. Tests: 48/48 pass.
Local output: artifacts/parity-investigation-final. The JSON matrix is retained
in docs; local map images stay ignored under the provenance policy.

Next: evaluate independent primitive coverage and backend-reference acceptance
criteria before claiming GPU correctness; CPU remains the practical baseline.
Production host/thread integration and native ownership remain R-001/R-002 work.
All preceding label/readback/Auto changes remain uncommitted; no remote issues sent.
