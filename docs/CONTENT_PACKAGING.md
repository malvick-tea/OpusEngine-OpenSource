# Content And Package Tools

This document explains the content parsing and package validation code in Opus.
It covers the library layer, archive layer, signing layer, diagnostics, and the
package CLI.

## Module Map

```text
Opus.Content
  -> low-level glTF, mesh, animation, image, texture, mip, and compression code

Opus.Content.Packaging
  -> manifests, validation, archives, signing, diagnostics, path safety

Opus.Tool.PackageValidator
  -> command-line access to package validation, generation, packing, verifying,
     and unpacking
```

The content package library is headless. It does not reference D3D12 renderer
assemblies.

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

Use this layer for parsing and transforming content bytes. Do not allocate GPU
resources here.

## Package Manifest

The package manifest describes:

- format version;
- package identity;
- target engine identity;
- authoring metadata;
- entrypoints;
- required features;
- file list;
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

Manifest parsing preserves unknown additive fields through extension data. This
lets newer manifests be read with a warning when the major version is compatible.

## Path Safety

`PackageRelativePath` rejects paths that are not safe inside a package root.

Reject examples:

- empty path;
- rooted path;
- parent-directory traversal;
- current-directory segments;
- null characters;
- malformed separators.

Package code should never combine raw manifest paths with a root without first
passing through the relative path validator.

## Validation

`PackageValidator` validates a directory in one pass and returns diagnostics.

It checks:

- package root existence;
- manifest existence;
- manifest JSON shape;
- manifest format version;
- engine identity;
- required features;
- duplicate paths;
- path safety;
- missing declared files;
- file size;
- SHA-256 hash;
- unlisted files;
- supported asset types;
- content-aware validation within the configured memory budget.

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

Diagnostics are structured:

- severity;
- code;
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

When adding a diagnostic, add a stable code and test both the triggering
condition and the reported target.

## Archives

The archive layer supports packing, reading, verifying, and extracting package
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

Archive code should preserve package structure and avoid trusting archive paths
without validation.

## Signing

Signing types cover package signature reading, signing, and verification.

Important types:

- `PackageSignature`
- `PackageSignatureAlgorithm`
- `PackageSignatureReader`
- `PackageSigner`
- `PackageSignatureVerifier`

Signing tests use fixture keys. Keep signing policy separate from ordinary
manifest validation.

## Manifest Generation

Manifest generation scans a package directory and emits file entries.

Important types:

- `PackageGenerationRequest`
- `PackageGenerationResult`
- `PackageManifestGenerator`

Generation should produce stable output. If ordering changes, update tests
intentionally.

## Package CLI

Project:

```text
src/Tools/Opus.Tool.PackageValidator
```

Main command areas:

- validate;
- generate;
- pack;
- verify;
- unpack.

Supporting types:

- `CliOptionReader`
- `CliDiagnosticReporter`
- `PackageValidatorCommand`
- `PackageGenerateCommand`
- `PackagePackCommand`
- `PackageVerifyCommand`
- `PackageUnpackCommand`
- `TextPackageValidationReporter`
- `JsonPackageValidationReporter`
- `PackageDiagnosticLocalizer`

CLI commands should be thin wrappers around the library layer.

## Validate Command

Typical command:

```powershell
dotnet run --project .\src\Tools\Opus.Tool.PackageValidator\Opus.Tool.PackageValidator.csproj -- validate --package <path>
```

Expected behavior:

- read package directory;
- validate manifest and files;
- print diagnostics;
- return a stable exit code.

Use JSON reporting when another tool needs to parse the output.

## Generate Command

Typical command:

```powershell
dotnet run --project .\src\Tools\Opus.Tool.PackageValidator\Opus.Tool.PackageValidator.csproj -- generate --package <path>
```

Expected behavior:

- scan package files;
- infer asset types;
- write or print a manifest depending on options;
- keep generated file order stable.

## Pack And Verify

Pack:

```powershell
dotnet run --project .\src\Tools\Opus.Tool.PackageValidator\Opus.Tool.PackageValidator.csproj -- pack --package <path> --output <archive>
```

Verify:

```powershell
dotnet run --project .\src\Tools\Opus.Tool.PackageValidator\Opus.Tool.PackageValidator.csproj -- verify --archive <archive>
```

Use pack/verify tests when changing archive structure, limits, or hash behavior.

## Add A New Asset Validator

1. Decide the package asset type.
2. Add or update asset type inference.
3. Implement `IPackageFileValidator`.
4. Add it to the declared file validator dispatch.
5. Add tests for valid, malformed, oversized, and mismatched files.
6. Add CLI tests if user-facing diagnostics change.

Keep validators bounded. Integrity checks can stream large files; deep validation
should respect the configured memory budget.

## Add A Manifest Field

1. Add the field to the manifest type.
2. Decide whether it is required or optional.
3. Update the reader tests.
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

Messages can change. Codes should be treated as stable.

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

This order separates manifest problems from file content problems.

## Test Map

Useful tests:

- `PackageValidatorTests`
- `PackageValidatorLargeFileTests`
- `PackageManifestGeneratorTests`
- `PackageRelativePathTests`
- `PackageFileHashTests`
- archive tests under `Archive/`
- signing tests under `Signing/`
- package tool command tests

When changing package behavior, run both library tests and CLI tests.

## Review Checklist

- Manifest paths are validated before filesystem use.
- Large files are streamed where possible.
- Deep validation respects the memory budget.
- Diagnostics have stable codes.
- CLI commands call library code rather than duplicating rules.
- Archive paths cannot escape the package root.
- Tests cover malformed input, not only happy paths.
