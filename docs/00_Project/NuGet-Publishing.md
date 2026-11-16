# NuGet trusted publishing

The release workflow is `.github/workflows/nuget-pack-publish.yml`. The publish
job requests GitHub OIDC credentials through `NuGet/login@v1` immediately before
uploading. Both NuGet package and symbol uploads use its temporary API key.
GitHub Packages uses the job's GITHUB_TOKEN.

## One-time account setup

1. On NuGet.org, open your profile's Trusted Publishing page and add a GitHub policy.
2. Set repository owner to `WuGing`, repository to `VectorTileRenderer`, and
   workflow file to `nuget-pack-publish.yml`.
3. Leave the environment field empty. The workflow uses no GitHub environment.
4. Select the owner and publishing scopes covering `WuGing.VectorTileRenderer`.
5. In the GitHub repository's Actions secrets, set `NUGET_USER` to the NuGet.org
   profile username associated with the policy. This is the profile name.

The policy fields and authentication flow follow
[Microsoft's setup instructions](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing).
The action's required username input and temporary-key output are documented in
[NuGet/login](https://github.com/NuGet/login).

## Publish or retry preview.7

After merging the workflow fix and configuring the account settings, open GitHub
Actions → NuGet Pack and Publish → Run workflow. Select `master` and enable
`publish`. This builds and publishes the version in the project file.

For future version tags, the existing `v*` trigger performs publishing and creates
the GitHub release. Manual branch runs publish packages through the existing
workflow-dispatch path. GitHub release creation runs on tag events.

Start a new run from the updated branch when migrating a failed older tag run.
GitHub reruns retain the original commit and ref. See
[GitHub's rerun behavior](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/re-run-workflows-and-jobs).

## Validation boundary

Workflow syntax and credential wiring can be checked locally. A real OIDC exchange
requires GitHub Actions and the matching NuGet.org policy. Successful package
upload provides the end-to-end publishing check.
