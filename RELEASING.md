# Releasing

A release is a tag on `master`. The tag starts a workflow that builds, tests and packs the two NuGet packages. The workflow does not publish them: you download them from GitHub and upload them to nuget.org yourself.

## Steps

1. **Set the version.** It is in two places, and they must be the same, because the Rust workspace version tracks the NuGet package version:
   - `<Version>` in `dotnet/src/Directory.Build.props`
   - `version` in `[workspace.package]` of `rust/Cargo.toml`, and the matching entries in `rust/Cargo.lock`

   Add the release notes to `dotnet/README.md` too. That file is the readme of both packages, so write it before the packages are made.
2. **Merge `develop` into `master`** with a pull request. CI runs on it, and only `develop` can be the source of a pull request to `master`.
3. **Tag the merge on `master`**, and push the tag:

   ```bash
   git switch master
   git pull
   git tag v4.0.0
   git push origin v4.0.0
   ```

4. **Wait for the Release workflow** (Actions tab). It stops with an error when:
   - the tag is not `v` plus the version in `Directory.Build.props`, or the Rust workspace has a different version;
   - the commit of a release tag is not on `master` (a tag with a suffix, such as `v4.0.0-rc.1`, can come from any branch);
   - a test fails;
   - the API of a package differs from the newest earlier release of the same major version on nuget.org (see below).

5. **Download the artifact** `nuget-packages` from the run (it is kept for 30 days) and unzip it. It holds four files: a `.nupkg` and a `.snupkg` for each package. The summary of the run gives their SHA-256 hashes.
6. **Upload the packages**, `Primitives` first, because `Compilers` depends on it. `dotnet nuget push` uploads the `.snupkg` with the `.nupkg` that is in the same folder:

   ```bash
   dotnet nuget push Zametek.Maths.Graphs.Primitives.4.0.0.nupkg --api-key <key> --source https://api.nuget.org/v3/index.json
   dotnet nuget push Zametek.Maths.Graphs.Compilers.4.0.0.nupkg --api-key <key> --source https://api.nuget.org/v3/index.json
   ```

## Dry run

To test the packing without a tag: Actions, Release, Run workflow, and choose a branch. The checks of the tag are skipped, and the summary of the run says that it is a dry run. The workflow file must be on the default branch (`develop`) for the button to appear.

## The API check

Every CI build and the Release workflow pack the packages with `.github/scripts/pack.sh`. For each package, the script finds the newest earlier release of the same major version on nuget.org, and package validation compares the new API with it. A removed or changed public member fails the build (error `CP0002`).

To break the API on purpose, do one of two things:

- Raise the major version. A new major version has nothing to be compared with, and a first release has no earlier release at all.
- Record the break in the `CompatibilitySuppressions.xml` file of the project that breaks (`dotnet/src/Zametek.Maths.Graphs.Compilers/` for the Compilers package). The version number then stays, which suits a library that only its maintainer uses. Set the new version in `Directory.Build.props` first. Then generate the entry from `dotnet/`, with the newest earlier release as the baseline:

  ```bash
  dotnet pack src/Zametek.Maths.Graphs.Compilers --configuration Release --output /tmp/packages \
    -p:EnablePackageValidation=true -p:PackageValidationBaselineVersion=4.0.0 \
    -p:ApiCompatGenerateSuppressionFile=true
  ```

  Describe the break in the "Breaking changes" section of `dotnet/README.md`. After the release the entry does nothing, because the next release compares with a package that already has the change. Delete it then. The repository has no such file at the moment, because 4.0.0 is a new major version and nothing earlier is compared with it.

## Pins

Nothing in the workflows moves by itself:

- The actions are pinned to commit SHAs, and Dependabot proposes the updates (weekly, as pull requests against `develop`). It does the same for the NuGet and Cargo dependencies.
- The .NET SDK is pinned in `ci.yml` and `release.yml`. Bump the two together.
- The Rust compiler is pinned in `rust/rust-toolchain.toml`. After a bump, run `rustup toolchain install` in `rust/` once.
- The runners are `ubuntu-24.04`, not `ubuntu-latest`.
