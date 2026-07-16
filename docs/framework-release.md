# Framework release and publishing

The framework has two release surfaces:

1. package-only consumer artifacts published to NuGet;
2. repository runtime/composition projects, including `BotFramework.Host`.

The current preview publishes only the package-only surface. `BotFramework.Host`
contains PostgreSQL, Wolverine, CAP and application composition and is not yet
a supported standalone NuGet package. The durable workflow API is therefore
available to repository composition roots and source-referenced examples, but
is not advertised as an installable `BotFramework.Host` package.

## Local release build

From the repository root, run:

```bash
bash eng/verify-public-api.sh
bash eng/package-consumer-smoke.sh
bash eng/pack-framework.sh 0.9.0-preview.1
```

The last command writes packages to:

```text
.artifacts/framework-release/0.9.0-preview.1/
```

It packs the exact package list used by the release workflow and enables
NuGet package validation. The directory is intentionally versioned and must be
empty before a new pack so that a release cannot silently reuse stale artifacts.

## Manual NuGet push

Create a scoped NuGet API key that can push the `BotFramework.*` package ids,
then push the generated packages:

```bash
for package in .artifacts/framework-release/0.9.0-preview.1/*.nupkg; do
  dotnet nuget push "$package" \
    --api-key "$NUGET_API_KEY" \
    --source https://api.nuget.org/v3/index.json \
    --skip-duplicate
done
```

The key must be supplied through an environment variable or CI secret; never
commit it to `NuGet.config` or the repository.

## GitHub Release path

The checked-in `.github/workflows/publish-framework.yml` is the normal path for
the public release. After the release commit is on `master`:

```bash
git tag -a framework-v0.9.0-preview.1 -m "BotFramework 0.9.0-preview.1"
git push origin framework-v0.9.0-preview.1
```

The workflow derives the package version from the tag, runs the build, tests,
public API check and package consumer smoke test, packs the nine public
artifacts, pushes them to NuGet and creates a GitHub Release using the matching
file in `docs/releases/`.

Before pushing the tag, configure the repository secret
`NUGET_API_KEY`. GitHub token permissions are declared by the workflow for the
release creation. A tag without a matching release-notes file is rejected.

## Future runtime package

If external consumers must use durable workflows without taking a source
reference to `BotFramework.Host`, first extract the workflow contracts and
implementation into a separately versioned package (for example,
`BotFramework.Workflows`) with an explicit PostgreSQL/Wolverine dependency
boundary. That is a separate packaging change; publishing `BotFramework.Host`
as-is would expose the monolith's composition and database ownership rather
than a stable framework API.
