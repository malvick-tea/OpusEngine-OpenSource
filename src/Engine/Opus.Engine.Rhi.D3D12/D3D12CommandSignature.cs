using System;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>
/// Wraps an <c>ID3D12CommandSignature</c> for GPU-driven indirect dispatches. A command
/// signature describes the layout of a single command in an indirect argument buffer —
/// what shape of arguments the GPU should consume per command record (draw counts,
/// instance counts, root constants, etc.).
///
/// R-17.a baseline: <see cref="CreateDrawIndexedIndirect"/> produces the canonical
/// "single <c>D3D12_DRAW_INDEXED_ARGUMENTS</c> per command" signature with a 20-byte
/// stride. Higher-R milestones bolt on root-constant + root-CBV/SRV argument types
/// for multi-draw indirect with per-draw material lookup.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "MA0055:Do not use finalizer",
    Justification = "The finalizer releases only one unmanaged COM reference.")]
public sealed unsafe class D3D12CommandSignature : IDisposable
{
    private ID3D12CommandSignature* _signature;
    private bool _disposed;

    private D3D12CommandSignature(string debugName, ID3D12CommandSignature* signature, uint strideBytes)
    {
        DebugName = debugName;
        _signature = signature;
        StrideBytes = strideBytes;
    }

    public string DebugName { get; }

    public uint StrideBytes { get; }

    public ID3D12CommandSignature* Native => _signature;

    ~D3D12CommandSignature()
    {
        ReleaseNative();
    }

    /// <summary>Creates the simplest signature: one <c>DrawIndexedInstanced</c> argument
    /// per command, no per-command root-binding mutations. Argument buffer must contain
    /// <c>D3D12_DRAW_INDEXED_ARGUMENTS</c> records (5 × uint = 20 bytes each):
    /// <c>{IndexCountPerInstance, InstanceCount, StartIndexLocation, BaseVertexLocation, StartInstanceLocation}</c>.
    /// Pass <paramref name="rootSignature"/> = null — DRAW_INDEXED alone doesn't touch root state.</summary>
    public static D3D12CommandSignature CreateDrawIndexedIndirect(D3D12RhiDevice device, string debugName = "DrawIndexedIndirect")
    {
        var arg = new IndirectArgumentDesc { Type = IndirectArgumentType.DrawIndexed };
        return Build(device, debugName, &arg, argumentCount: 1u, strideBytes: 20u, rootSignature: null);
    }

    /// <summary>Multi-arg signature for per-draw root-constant mutations followed by a
    /// DRAW_INDEXED. Argument layout per command: <c>uint[numRootConstants]</c> at
    /// <paramref name="rootParameterIndex"/> + <c>D3D12_DRAW_INDEXED_ARGUMENTS</c>.
    /// Stride = <c>numRootConstants*4 + 20</c>. Pass the matching root signature so the
    /// driver can validate the parameter index.</summary>
    public static D3D12CommandSignature CreateRootConstantsAndDrawIndexed(
        D3D12RhiDevice device,
        D3D12RootSignature rootSignature,
        uint rootParameterIndex,
        uint numRootConstants,
        string debugName = "RootConstantsAndDrawIndexed")
    {
        var args = stackalloc IndirectArgumentDesc[2];
        args[0] = new IndirectArgumentDesc
        {
            Type = IndirectArgumentType.Constant,
        };
        args[0].Anonymous.Constant.RootParameterIndex = rootParameterIndex;
        args[0].Anonymous.Constant.DestOffsetIn32BitValues = 0u;
        args[0].Anonymous.Constant.Num32BitValuesToSet = numRootConstants;
        args[1] = new IndirectArgumentDesc { Type = IndirectArgumentType.DrawIndexed };

        var stride = numRootConstants * 4u + 20u;
        return Build(device, debugName, args, argumentCount: 2u, strideBytes: stride, rootSignature: rootSignature.Native);
    }

    private static D3D12CommandSignature Build(
        D3D12RhiDevice device,
        string debugName,
        IndirectArgumentDesc* args,
        uint argumentCount,
        uint strideBytes,
        ID3D12RootSignature* rootSignature)
    {
        var desc = new CommandSignatureDesc
        {
            ByteStride = strideBytes,
            NumArgumentDescs = argumentCount,
            PArgumentDescs = args,
            NodeMask = 0u,
        };

        ID3D12CommandSignature* signature = null;
        var guid = ID3D12CommandSignature.Guid;
        SilkMarshal.ThrowHResult(device.NativeDevice->CreateCommandSignature(
            &desc,
            rootSignature,
            &guid,
            (void**)&signature));

        return new D3D12CommandSignature(debugName, signature, strideBytes);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ReleaseNative();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void ReleaseNative()
    {
        if (_signature != null)
        {
            _signature->Release();
            _signature = null;
        }
    }
}
