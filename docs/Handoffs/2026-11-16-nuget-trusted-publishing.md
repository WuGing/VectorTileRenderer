# NuGet trusted publishing — 2026-11-16

Branch: codex/nuget-trusted-publishing
Pull request:

The publish job in `.github/workflows/nuget-pack-publish.yml` requests OIDC through
`NuGet/login@v1` with `id-token: write`. Package and symbol uploads use the returned
temporary key. The action reads the NuGet profile username from `NUGET_USER`.
GitHub Packages continues to use GITHUB_TOKEN. Existing tag/manual publish gates
and release metadata checks are preserved.

Official setup and exact policy fields are in
[publishing instructions](../00_Project/NuGet-Publishing.md).
The repository currently has no NUGET_USER Actions secret. NuGet.org policy setup
requires the package owner's account. Live OIDC exchange/upload awaits that setup.

Both workflows pass checksum-verified actionlint 1.7.12. Credential wiring checks
confirm both NuGet uploads consume the action output and the long-lived key secret
reference is removed. Documentation links and whitespace checks pass.

The existing preview.7 tag publishing run failed at
https://github.com/WuGing/VectorTileRenderer/actions/runs/43876536958.
After merge/account setup, start a new workflow-dispatch run from master with
publish enabled. GitHub reruns retain the original SHA/ref, so an older run retains
its old workflow. No package/version change is needed for this workflow repair.
