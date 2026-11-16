# Dependencies

## Third-Party Packages

Opera et Dies uses as few third-party packages as it can. Each one is checked before it's added, pinned to an exact version, and listed in `ThirdPartyNotices.txt` with its license. The game itself uses none; Markdig is used only by the mod editor (to show its help).

### Safeguards

These apply to every restore and build of the solution:

| Safeguard | Where | What it does |
|---|---|---|
| Exact versions | `Directory.Packages.props` | Third-party packages are pinned with `[x.y.z]`, so no other version can be used. |
| Lock files | `Directory.Build.props`, each project's `packages.lock.json` | Each project records the version and content hash of every package it uses, directly or not. Restores run in locked mode: they fail if a package's contents differ from the hash, or if a version would change. |
| One source | `nuget.config` | Packages only come from nuget.org, whatever sources the machine lists, and package source mapping stops a package of the same name on another source from being used. |
| Signatures | `nuget.config` | Every package must be signed by a trusted signer: nuget.org's repository signature, by its signing certificates' fingerprints. |
| Vulnerability audit | `Directory.Build.props` | Restores check every package, direct or not, against nuget.org's list of known vulnerabilities, of any severity. A vulnerable package stops the build (its warning is an error). A restore that can't reach nuget.org to check only warns (NU1900). |
| Limited reach | The project files | A package is referenced only by the project that needs it. |

`nuget.config` sits next to the solution, so it also applies to the GoldenAnvil.Utility projects the solution builds. The lock files and the audit only cover the projects in this repository.

### Adding or updating a package

1. **Choose a version** that has been out for a few months, with many downloads, and that isn't marked deprecated or vulnerable on nuget.org. A brand-new release is the most likely to be a compromised one.
2. **Inspect it before it's restored.** Download the exact package into an empty scratch folder (from `https://api.nuget.org/v3-flatcontainer/<id>/<version>/<id>.<version>.nupkg`, with the id in lower case) and list its contents (it's a zip). It should hold only `lib/` assemblies (and their documentation), with no `build/`, `buildTransitive/`, `analyzers/`, `tools/`, or `content/` folders, which run or add code during a build. Read its `.nuspec` for its dependencies (each one needs the same checks) and license, and run `dotnet nuget verify <file> --all` to check its signature.
3. **Add it** to `Directory.Packages.props` with an exact version (`[x.y.z]`), and reference it only from the projects that need it.
4. **Update the lock files**, which locked mode otherwise refuses:

   ```bash
   dotnet restore -p:RestoreLockedMode=false --force-evaluate
   ```

   Check that the new `contentHash` in the lock file is the one `dotnet nuget verify` showed.
5. **Add its license** to `ThirdPartyNotices.txt`, using the exact text from its repository at the commit the release was built from (the `.nuspec` names it).

When nuget.org starts signing with a new certificate, restores fail with a signature error until it's trusted. Check the certificate on nuget.org, then add it:

```bash
dotnet nuget trust source nuget.org --configfile nuget.config
```

When the audit finds a vulnerability, update the package (or the package that brings it in) to a version that fixes it, rather than turning the audit off.
