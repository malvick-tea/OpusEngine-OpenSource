using System;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>
/// Single-responsibility serializer: takes a populated parameter / sampler arrays,
/// hands them to D3D12 SerializeVersionedRootSignature, then creates the native
/// root signature on the device. Centralises COM ref-count cleanup and error-blob
/// extraction so every Create* method drops to 10-15 lines.
/// </summary>
internal static unsafe class RootSignatureSerializer
{
    private const int MaxErrorBlobBytes = 1024 * 1024;
    private static readonly D3D12 D3D12Api = D3D12.GetApi();

    public static D3D12RootSignature Build(
        D3D12RhiDevice device,
        RootParameter* parameters,
        uint parameterCount,
        StaticSamplerDesc* staticSamplers,
        uint staticSamplerCount,
        RootSignatureFlags flags)
    {
        if (device == null)
        {
            throw new ArgumentNullException(nameof(device));
        }

        var desc = new VersionedRootSignatureDesc { Version = D3DRootSignatureVersion.Version10 };
        desc.Anonymous.Desc10 = new RootSignatureDesc
        {
            NumParameters = parameterCount,
            PParameters = parameters,
            NumStaticSamplers = staticSamplerCount,
            PStaticSamplers = staticSamplers,
            Flags = flags,
        };

        ID3D10Blob* serialized = null;
        ID3D10Blob* errorBlob = null;
        try
        {
            var hr = D3D12Api.SerializeVersionedRootSignature(&desc, &serialized, &errorBlob);
            if (hr < 0)
            {
                var msg = ReadErrorBlob(errorBlob);
                throw new InvalidOperationException($"D3D12SerializeVersionedRootSignature failed: {msg}");
            }

            var rsGuid = ID3D12RootSignature.Guid;
            ID3D12RootSignature* rootSig = null;
            SilkMarshal.ThrowHResult(device.NativeDevice->CreateRootSignature(
                nodeMask: 0u,
                serialized->GetBufferPointer(),
                serialized->GetBufferSize(),
                &rsGuid,
                (void**)&rootSig));

            return new D3D12RootSignature(rootSig);
        }
        finally
        {
            if (serialized != null)
            {
                serialized->Release();
            }

            if (errorBlob != null)
            {
                errorBlob->Release();
            }
        }
    }

    private static string ReadErrorBlob(ID3D10Blob* errorBlob)
    {
        if (errorBlob == null || errorBlob->GetBufferSize() == 0)
        {
            return "(no error blob)";
        }

        var size = errorBlob->GetBufferSize();
        if (size > MaxErrorBlobBytes)
        {
            return $"(error blob exceeded {MaxErrorBlobBytes} bytes)";
        }

        return System.Text.Encoding.ASCII.GetString(
            (byte*)errorBlob->GetBufferPointer(),
            checked((int)size));
    }
}
