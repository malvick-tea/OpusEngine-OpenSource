using System;
using System.Runtime.InteropServices;
using System.Text;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D.Compilers;
using DxcBuffer = Silk.NET.Direct3D.Compilers.Buffer;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>
/// Runtime HLSL → DXIL compiler. Wraps DXC's <c>IDxcCompiler3</c> + <c>IDxcUtils</c>
/// for inline HLSL string compilation. Returns the bytecode the D3D12 PSO factory feeds
/// to <see cref="GraphicsPipelineFactory"/>.
///
/// Note vs ADR-0017: runtime shader bytecode comes from the **offline asset bake**
/// (per ADR-0012) — `opus-import` invokes DXC at build time and ships `.gshd` blobs.
/// Runtime DXC stays available for dev-mode hot reload + integration tests + sandbox
/// programs, but the ship build does not invoke it on the hot path.
///
/// Disposable singleton — one DXC instance lives as long as the engine; dispose at
/// shutdown only.
/// </summary>
public sealed unsafe class D3D12ShaderCompiler : IDisposable
{
    private readonly DXC _dxc;
    private IDxcCompiler3* _compiler;
    private IDxcUtils* _utils;
    private bool _disposed;

    // DXC class identifiers — fixed by Microsoft, hard-coded here so we don't depend on
    // a CLSID table the binding doesn't expose.
    private static readonly Guid ClsidDxcCompiler = new("73e22d93-e6ce-47f3-b5bf-f0664f39c1b0");
    private static readonly Guid ClsidDxcUtils = new("6245d6af-66e0-48fd-80b4-4d271796748c");

    public D3D12ShaderCompiler()
    {
        _dxc = DXC.GetApi();

        var compilerClsid = ClsidDxcCompiler;
        var compilerInterfaceGuid = IDxcCompiler3.Guid;
        IDxcCompiler3* compiler = null;
        SilkMarshal.ThrowHResult(_dxc.CreateInstance(&compilerClsid, &compilerInterfaceGuid, (void**)&compiler));
        _compiler = compiler;

        var utilsClsid = ClsidDxcUtils;
        var utilsInterfaceGuid = IDxcUtils.Guid;
        IDxcUtils* utils = null;
        SilkMarshal.ThrowHResult(_dxc.CreateInstance(&utilsClsid, &utilsInterfaceGuid, (void**)&utils));
        _utils = utils;
    }

    /// <summary>
    /// Compiles <paramref name="source"/> as the given DXC profile (e.g. <c>"vs_6_6"</c>,
    /// <c>"ps_6_6"</c>, <c>"cs_6_6"</c>) entering at <paramref name="entryPoint"/>. Returns
    /// the DXIL bytecode ready for ID3D12Device::CreateGraphicsPipelineState. Throws
    /// <see cref="ShaderCompilationException"/> on compile error with the diagnostic text.
    /// </summary>
    public byte[] Compile(string source, string entryPoint, string profile, string? sourceName = null)
    {
        if (_compiler == null)
        {
            throw new ObjectDisposedException(nameof(D3D12ShaderCompiler));
        }

        var sourceBytes = Encoding.UTF8.GetBytes(source);

        // Argument list mirrors the dxc.exe CLI: -E entry -T profile -Zi (debug info)
        // -Qstrip_reflect (we don't need reflection back yet) -Qstrip_debug for ship.
        // R-1.3 ships dev defaults; R-1.4 will tighten for asset bake.
        var args = new string[]
        {
            "-E", entryPoint,
            "-T", profile,
            "-HV", "2021",
            "-O3",
        };

        var argPtrs = stackalloc char*[args.Length];
        for (var i = 0; i < args.Length; i++)
        {
            argPtrs[i] = (char*)Marshal.StringToHGlobalUni(args[i]);
        }

        DxcBuffer sourceBuffer;
        fixed (byte* pSource = sourceBytes)
        {
            sourceBuffer = new DxcBuffer
            {
                Ptr = pSource,
                Size = (nuint)sourceBytes.Length,
                Encoding = (uint)0u, // DXC_CP_ACP — but we feed UTF-8; auto-detect kicks in
            };

            IDxcResult* result = null;
            try
            {
                var resultGuid = IDxcResult.Guid;
                SilkMarshal.ThrowHResult(_compiler->Compile(
                    &sourceBuffer,
                    (char**)argPtrs,
                    (uint)args.Length,
                    pIncludeHandler: null,
                    &resultGuid,
                    (void**)&result));

                int compileStatus;
                SilkMarshal.ThrowHResult(result->GetStatus(&compileStatus));

                if (compileStatus < 0)
                {
                    var errors = ExtractErrors(result);
                    throw new ShaderCompilationException(sourceName ?? entryPoint, errors);
                }

                return ExtractDxil(result);
            }
            finally
            {
                if (result != null)
                {
                    result->Release();
                }

                for (var i = 0; i < args.Length; i++)
                {
                    Marshal.FreeHGlobal((nint)argPtrs[i]);
                }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_compiler != null)
        {
            _compiler->Release();
            _compiler = null;
        }

        if (_utils != null)
        {
            _utils->Release();
            _utils = null;
        }

        // Don't Dispose the DXC API — that unloads dxcompiler.dll. Process-wide load/unload
        // churn (xUnit creates a new compiler per D3D12 integration test) eventually exhausts
        // Windows loader resources; testhost hangs on subsequent SDL/D3D12 init.
        _disposed = true;
    }

    private static byte[] ExtractDxil(IDxcResult* result)
    {
        IDxcBlob* blob = null;
        try
        {
            var blobGuid = IDxcBlob.Guid;
            var hr = result->GetOutput(OutKind.Object, &blobGuid, (void**)&blob, ppOutputName: null);
            SilkMarshal.ThrowHResult(hr);

            var size = (int)blob->GetBufferSize();
            var ptr = (byte*)blob->GetBufferPointer();
            var bytes = new byte[size];
            new Span<byte>(ptr, size).CopyTo(bytes);
            return bytes;
        }
        finally
        {
            if (blob != null)
            {
                blob->Release();
            }
        }
    }

    private static string ExtractErrors(IDxcResult* result)
    {
        IDxcBlobUtf8* errors = null;
        try
        {
            var blobGuid = IDxcBlobUtf8.Guid;
            var hr = result->GetOutput(OutKind.Errors, &blobGuid, (void**)&errors, ppOutputName: null);
            if (hr < 0 || errors == null || errors->GetBufferSize() == 0)
            {
                return "(no diagnostic output)";
            }

            var size = (int)errors->GetBufferSize();
            var ptr = (byte*)errors->GetBufferPointer();
            return Encoding.UTF8.GetString(ptr, size);
        }
        finally
        {
            if (errors != null)
            {
                errors->Release();
            }
        }
    }
}

/// <summary>Thrown when DXC reports a compile error. <see cref="Diagnostics"/> carries
/// the warning + error log verbatim for surfacing to the developer.</summary>
public sealed class ShaderCompilationException : Exception
{
    public ShaderCompilationException(string sourceName, string diagnostics)
        : base($"shader compile failed for '{sourceName}':\n{diagnostics}")
    {
        SourceName = sourceName;
        Diagnostics = diagnostics;
    }

    public string SourceName { get; }

    public string Diagnostics { get; }
}
