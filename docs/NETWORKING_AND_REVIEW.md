# Networking, Troubleshooting, And Review

## Authenticated UDP

Use `Opus.Net` for contracts and loopback transports. Use `Opus.Net.Udp` for
UDP.

UDP is authenticated by default and has no anonymous mode. Both endpoints must
receive the same `UdpTransportOptions.AuthenticationKey`. Runtime hosts should
load it with `UdpAuthentication.ReadKeyFile`; accepted key-file formats are:

- `base64:<at-least-32-random-bytes>`;
- `hex:<at-least-32-random-bytes>`;
- a high-entropy passphrase of at least 16 characters, derived with PBKDF2.

Protocol v2 authenticates the Hello exchange, derives a per-session key from
client and server nonces, MACs every frame with HMAC-SHA256, and rejects replayed
sequence numbers. Server deployments should size both the global peer cap and
the per-source-address peer cap for their expected NAT topology.

When changing transport behavior:

1. Update contract tests when observable behavior changes.
2. Add loopback coverage if higher layers can test against the contract.
3. Add UDP frame codec tests for wire changes.
4. Add integration tests for heartbeat, deadline, disconnect, queue limits, or
   rate limits.
5. Keep timing tests bounded and deterministic where possible.

## Common Problems

### Build sees old API after a file sync

Run:

```powershell
dotnet build .\OpusEngine.sln -c Release -t:Rebuild
```

This clears stale incremental references without deleting source-side build
scripts or known-good profiles.

### D3D12 device creation fails

Check:

- Windows host;
- compatible adapter;
- graphics driver;
- DXC availability;
- whether non-D3D12 tests pass;
- whether the failure is in PAL window setup, RHI device creation, swap chain,
  or renderer resource setup.

### Package validation reports path errors

Check:

- package-relative paths;
- no absolute paths in the manifest;
- no parent-directory traversal;
- file size and SHA-256 hash match the manifest;
- asset type matches the declared file.

### UDP integration test flakes

Check:

- test timing options;
- whether tests are running in parallel;
- whether ports are available;
- whether the assertion can observe an event list instead of sleeping blindly.

### Consumer assembly fails to load

Check:

- path resolves;
- file is a managed assembly;
- dependencies can resolve;
- exactly one suitable factory is visible;
- factory has a public parameterless constructor;
- `CreateIntegration()` returns a valid facade.

### Editor opens but no model appears

Check:

- `--content-root` or project content roots;
- whether the model browser lists the asset;
- whether `inspect <model>` can read the glTF/GLB;
- whether the node is hidden;
- whether the camera needs `F` to frame the scene.

## Review Checklist

Before considering a change done:

- the touched project builds;
- the closest test project passes;
- a neighboring layer test passes if the change crosses a boundary;
- public contracts have tests;
- backend behavior has backend tests;
- CLI output changes have command tests;
- diagnostics use stable codes;
- generated output is not part of the change;
- D3D12-only code stays in D3D12 projects;
- platform-only code stays in PAL projects;
- editor document mutations are undoable;
- docs and command examples match the current CLI.
