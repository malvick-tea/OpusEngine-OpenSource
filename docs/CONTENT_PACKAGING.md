# Content And Package Tools

Opus content code is split into two layers:

- `Opus.Content` reads and prepares asset data.
- `Opus.Content.Packaging` describes, validates, archives, signs, verifies, and
  extracts Opus packages.

The command-line tool in `Opus.Tool.PackageValidator` is a thin shell over those
library layers.

## Module Map

```text
Opus.Content
  glTF/GLB, scene tree math, mesh data, animation sampling, image decoding,
  mip generation, block compression.

Opus.Content.Packaging
  Package manifests, validation diagnostics, archive packing, archive reading,
  signing, verification, extraction, relative path safety.

Opus.Tool.PackageValidator
  CLI commands: validate, generate, pack, verify, unpack.

Opus.Editor.Content
  Editor-facing model summaries, scene content reports, and material-set
  inspection.
```

None of these libraries allocate D3D12 resources. Renderer upload happens in the
renderer layer.

## Low-Level Content

`Opus.Content` contains reusable readers and helpers:

- `GltfBinaryReader`
- `GltfDocument`
- `GltfFilePacker`
- `GltfImageReader`
- `GltfScene`
- `SceneTreeMath`
- `MeshData`
- `AnimationClip`
- `AnimationSampler`
- `ImageDecoder`
- `MipChain`
- `CompressedTexture`
- `BcnTextureEncoder`
- `CompressedTextureCache`

Use this layer for bytes-in/data-out work. Keep GPU allocation, package trust,
and editor UI out of it.

## Package Manifest

The package manifest describes:

- manifest format version;
- package identity;
- target engine identity;
- authoring metadata;
- entry points;
- required features;
- declared files;
- optional extension data.

Important types:

- `ContentPackageManifest`
- `ContentPackageInfo`
- `ContentPackageTarget`
- `ContentPackageFile`
- `ContentPackageEntrypoints`
- `ManifestFormatVersion`
- `PackageFeatures`
- `PackageAssetTypes`
- `PackageAssetTypeInference`

Unknown additive fields are preserved through extension data when the format is
compatible. This lets newer manifests remain inspectable by older tooling where
safe.

## Path Safety

`PackageRelativePath` protects package roots from unsafe paths.

Rejected examples:

- empty paths;
- rooted paths;
- parent-directory traversal;
- current-directory segments;
- null characters;
- malformed separators.

Package code should never combine raw manifest paths with a package root until
the path has passed the relative-path validator.

## Validation

`PackageValidator` validates package directories and returns structured
diagnostics.

It checks:

- package root existence;
- manifest existence;
- manifest JSON shape;
- manifest format version;
- engine identity compatibility;
- required features;
- duplicate paths;
- path safety;
- missing declared files;
- file size;
- SHA-256 hash;
- unlisted files;
- supported asset types;
- content-aware validation within a configured memory budget.

Important types:

- `PackageValidationRequest`
- `PackageValidationResult`
- `PackageUnlistedFilePolicy`
- `DeclaredFileValidator`
- `IPackageFileValidator`
- `PackageFileHash`
- `PackageDiagnosticBuilder`

Content-aware validators include:

- glTF validator;
- texture validator;
- font validator;
- localisation validator.

## Diagnostics

Package diagnostics are structured:

- severity;
- stable code;
- target;
- message;
- hint;
- localisation key;
- arguments.

Important types:

- `PackageDiagnostic`
- `PackageDiagnosticCode`
- `PackageDiagnosticSeverity`
- `PackageDiagnosticTarget`
- `PackageDiagnosticArguments`
- `PackageDiagnosticLocalizer`

Messages can improve over time. Codes should be treated as stable.

## Archives

The archive layer supports packing, reading, verifying, and extracting `.opkg`
archives.

Important types:

- `OpusPackageArchive`
- `OpusPackageArchiveReader`
- `OpusPackageArchiveWriter`
- `OpusPackageArchiveStructure`
- `OpusPackageArchiveLimits`
- `PackageArchivePacker`
- `PackageArchiveVerifier`
- `OpusPackageExtractor`
- `ArchiveEntryHash`

Archive extraction must preserve package structure and must not trust archive
paths without validation.

## Signing

Signing types cover package signature reading, signing, and verification.

Important types:

- `PackageSignature`
- `PackageSignatureAlgorithm`
- `PackageSignatureReader`
- `PackageSigner`
- `PackageSignatureVerifier`

Signing policy is separate from ordinary manifest validation. A package can be
structurally valid while failing a signature requirement.

## Manifest Generation

Manifest generation scans a content root and emits declared file entries.

Important types:

- `PackageGenerationRequest`
- `PackageGenerationResult`
- `PackageManifestGenerator`

Generation should produce stable output. If ordering changes, update tests
intentionally.

## CLI

Project:

```text
src/Tools/Opus.Tool.PackageValidator
```

Show help:

```powershell
dotnet run -c Release --project .\src\Tools\Opus.Tool.PackageValidator\Opus.Tool.PackageValidator.csproj -- --help
```

Current command shape:

```text
validate <package-root> [--format text|json] [--locale en|ru] [--unlisted warning|error|ignore] [--max-deep-validation-bytes <bytes>]
generate <content-root> --id <id> --name <display-name> --version <semver> [--created <iso-utc>] [--output <path>] [--locale en|ru]
pack <content-root> [--id <id> --name <name> --version <semver>] [--manifest <path>] [--output <path.opkg>] [--key <private-key.pem> --key-id <id>] [--locale en|ru]
verify <package.opkg> --key <trusted-public-key.pem> [--locale en|ru]
verify <package.opkg> --integrity-only [--locale en|ru]
unpack <package.opkg> <target-dir> --key <trusted-public-key.pem> [--locale en|ru]
```

The unpack target must be empty. The extraction library requires a trusted
public key; verification and extraction share one open archive handle so the
package path cannot be swapped after signature checks.

## Validate

Validate a package directory:

```powershell
dotnet run -c Release --project .\src\Tools\Opus.Tool.PackageValidator\Opus.Tool.PackageValidator.csproj -- validate .\content\sample-package
```

Emit JSON diagnostics:

```powershell
dotnet run -c Release --project .\src\Tools\Opus.Tool.PackageValidator\Opus.Tool.PackageValidator.csproj -- validate .\content\sample-package --format json
```

Treat unlisted files as errors:

```powershell
dotnet run -c Release --project .\src\Tools\Opus.Tool.PackageValidator\Opus.Tool.PackageValidator.csproj -- validate .\content\sample-package --unlisted error
```

## Generate

Generate a manifest:

```powershell
dotnet run -c Release --project .\src\Tools\Opus.Tool.PackageValidator\Opus.Tool.PackageValidator.csproj -- generate .\content\sample-package --id sample.package --name "Sample Package" --version 0.1.0 --output .\content\sample-package\opus.package.json
```

Generation scans files, infers asset types where possible, and writes stable
manifest output.

## Pack

Pack a content root:

```powershell
dotnet run -c Release --project .\src\Tools\Opus.Tool.PackageValidator\Opus.Tool.PackageValidator.csproj -- pack .\content\sample-package --output .\.local\sample.opkg
```

Pack and sign:

```powershell
dotnet run -c Release --project .\src\Tools\Opus.Tool.PackageValidator\Opus.Tool.PackageValidator.csproj -- pack .\content\sample-package --output .\.local\sample.opkg --key .\keys\private-key.pem --key-id local-dev
```

Use `--manifest <path>` when the manifest is outside the default location.

## Verify

Verify archive structure, hashes, and publisher signature:

```powershell
dotnet run -c Release --project .\src\Tools\Opus.Tool.PackageValidator\Opus.Tool.PackageValidator.csproj -- verify .\.local\sample.opkg --key .\keys\public-key.pem
```

For a non-executable development artifact, verify integrity without asserting a
publisher identity:

```powershell
dotnet run -c Release --project .\src\Tools\Opus.Tool.PackageValidator\Opus.Tool.PackageValidator.csproj -- verify .\.local\sample.opkg --integrity-only
```

## Unpack

Extract into a target directory:

```powershell
dotnet run -c Release --project .\src\Tools\Opus.Tool.PackageValidator\Opus.Tool.PackageValidator.csproj -- unpack .\.local\sample.opkg .\.local\unpacked-sample --key .\keys\public-key.pem
```

Extraction first verifies a trusted P-256 publisher signature, then applies
package path safety rules. Archive entries must not escape the target root.

## Editor Content Helpers

`Opus.Editor.Content` provides authoring-facing inspection:

- `ModelInspector` summarizes glTF/GLB meshes and triangle counts.
- `SceneContentReporter` reports scene asset usage and rough content cost.
- `MaterialSetInspector` checks PBR material set conventions.

Editor CLI examples:

```powershell
dotnet run -c Release --project .\src\Editor\Opus.App.Editor\Opus.App.Editor.csproj -- inspect .\content\models\tank.glb
dotnet run -c Release --project .\src\Editor\Opus.App.Editor\Opus.App.Editor.csproj -- report .\scene.json --content-root .\content
dotnet run -c Release --project .\src\Editor\Opus.App.Editor\Opus.App.Editor.csproj -- materials .\content\materials
```

## Add A New Asset Validator

1. Decide the package asset type.
2. Add or update asset type inference.
3. Implement `IPackageFileValidator`.
4. Add it to declared-file validation dispatch.
5. Add tests for valid, malformed, oversized, and mismatched files.
6. Add CLI/reporting tests if user-facing diagnostics change.

Keep validators bounded. Integrity checks can stream large files; deep
validation should respect the configured memory budget.

## Add A Manifest Field

1. Add the field to the manifest type.
2. Decide whether it is required or optional.
3. Update reader tests.
4. Update validation if the field has rules.
5. Update generation if the field should be emitted.
6. Preserve compatibility behavior for unknown additive fields.

Do not add validation rules in the CLI when they belong in the library.

## Add A Diagnostic Code

1. Add a new code to `PackageDiagnosticCode`.
2. Add builder usage at the validation point.
3. Include a target and arguments.
4. Add tests that assert the code and target.
5. Add localisation/reporting coverage if message output changes.

## Debugging Validation

Use this order:

1. Confirm the package root path.
2. Confirm the manifest file exists.
3. Parse the manifest alone.
4. Check path normalization.
5. Check duplicate paths.
6. Check size and hash.
7. Check asset-type inference.
8. Check deep validator behavior.
9. Check reporter output.
10. Check CLI argument parsing last.

This separates manifest problems from filesystem, file-content, and command-line
problems.

## Test Map

Useful areas:

- package validation tests in `Opus.Content.Packaging.Tests`;
- archive tests under archive-related test files;
- signing tests under signing-related test files;
- manifest generation tests;
- package tool command tests in `Opus.Tool.PackageValidator.Tests`;
- editor content tests in `Opus.Editor.Content.Tests`.

When changing package behavior, run both the library tests and package-tool
tests.

## Review Checklist

- Manifest paths are validated before filesystem use.
- Large files are streamed where possible.
- Deep validation respects the memory budget.
- Diagnostics have stable codes.
- CLI commands call library code rather than duplicating rules.
- Archive paths cannot escape the package root.
- Signature requirements are explicit.
- Tests cover malformed input, not only happy paths.
