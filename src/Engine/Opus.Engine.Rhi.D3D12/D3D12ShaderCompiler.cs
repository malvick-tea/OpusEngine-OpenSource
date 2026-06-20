using System;
using System.Runtime.InteropServices;
using System.Text;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D.Compilers;
using DxcBuffer = Silk.NET.Direct3D.Compilers.Buffer;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>Runtime HLSL-to-DXIL compiler for development and integration tests.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "MA0055:Do not use finalizer",
    Justification = "The finalizer releases only unmanaged DXC COM references.")]
public sealed unsafe class D3D12ShaderCompiler : IDisposable
{
    private const int MaxCompilerBlobBytes = 64 * 1024 * 1024;
    private static readonly Guid ClsidDxcCompiler =
        new("73e22d93-e6ce-47f3-b5bf-f0664f39c1b0");
    private static readonly Guid ClsidDxcUtils =
        new("6245d6af-66e0-48fd-80b4-4d271796748c");

    private readonly DXC _dxc;
    private IDxcCompiler3* _compiler;
    private IDxcUtils* _utils;
    private bool _disposed;

    ~D3D12ShaderCompiler()
    {
        ReleaseNative();
    }

    public D3D12ShaderCompiler()
    {
        _dxc = DXC.GetApi();

        var compilerClsid = ClsidDxcCompiler;
        var compilerInterfaceGuid = IDxcCompiler3.Guid;
        IDxcCompiler3* compiler = null;
        SilkMarshal.ThrowHResult(
            _dxc.CreateInstance(
                &compilerClsid,
                &compilerInterfaceGuid,
                (void**)&compiler));
        _compiler = compiler;

        try
        {
            var utilsClsid = ClsidDxcUtils;
            var utilsInterfaceGuid = IDxcUtils.Guid;
            IDxcUtils* utils = null;
            SilkMarshal.ThrowHResult(
                _dxc.CreateInstance(
                    &utilsClsid,
                    &utilsInterfaceGuid,
                    (void**)&utils));
            _utils = utils;
        }
        catch
        {
            _compiler->Release();
            _compiler = null;
            throw;
        }
    }

    public byte[] Compile(
        string source,
        string entryPoint,
        string profile,
        string? sourceName = null)
    {
        ObjectDisposedException.ThrowIf(_compiler == null, this);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(entryPoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(profile);

        var sourceBytes = Encoding.UTF8.GetBytes(source);
        var args = new[]
        {
            "-E", entryPoint,
            "-T", profile,
            "-HV", "2021",
            "-O3",
        };

        var argumentPointers = stackalloc char*[args.Length];
        new Span<nint>(argumentPointers, args.Length).Clear();
        try
        {
            for (var index = 0; index < args.Length; index++)
            {
                argumentPointers[index] =
                    (char*)Marshal.StringToHGlobalUni(args[index]);
            }

            fixed (byte* sourcePointer = sourceBytes)
            {
                var sourceBuffer = new DxcBuffer
                {
                    Ptr = sourcePointer,
                    Size = (nuint)sourceBytes.Length,
                    Encoding = 0u,
                };

                IDxcResult* result = null;
                try
                {
                    var resultGuid = IDxcResult.Guid;
                    SilkMarshal.ThrowHResult(
                        _compiler->Compile(
                            &sourceBuffer,
                            argumentPointers,
                            (uint)args.Length,
                            pIncludeHandler: null,
                            &resultGuid,
                            (void**)&result));

                    int compileStatus;
                    SilkMarshal.ThrowHResult(result->GetStatus(&compileStatus));
                    if (compileStatus < 0)
                    {
                        throw new ShaderCompilationException(
                            sourceName ?? entryPoint,
                            ExtractErrors(result));
                    }

                    return ExtractDxil(result);
                }
                finally
                {
                    if (result != null)
                    {
                        result->Release();
                    }
                }
            }
        }
        finally
        {
            for (var index = 0; index < args.Length; index++)
            {
                if (argumentPointers[index] != null)
                {
                    Marshal.FreeHGlobal((nint)argumentPointers[index]);
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

        ReleaseNative();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void ReleaseNative()
    {
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
    }

    private static byte[] ExtractDxil(IDxcResult* result)
    {
        IDxcBlob* blob = null;
        try
        {
            var blobGuid = IDxcBlob.Guid;
            SilkMarshal.ThrowHResult(
                result->GetOutput(
                    OutKind.Object,
                    &blobGuid,
                    (void**)&blob,
                    ppOutputName: null));

            var size = CheckedBlobSize(blob->GetBufferSize(), "DXIL");
            var bytes = new byte[size];
            new Span<byte>(blob->GetBufferPointer(), size).CopyTo(bytes);
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
            var resultCode = result->GetOutput(
                OutKind.Errors,
                &blobGuid,
                (void**)&errors,
                ppOutputName: null);
            if (resultCode < 0
                || errors == null
                || errors->GetBufferSize() == 0)
            {
                return "(no diagnostic output)";
            }

            var size = CheckedBlobSize(
                errors->GetBufferSize(),
                "DXC diagnostics");
            return Encoding.UTF8.GetString(
                (byte*)errors->GetBufferPointer(),
                size);
        }
        finally
        {
            if (errors != null)
            {
                errors->Release();
            }
        }
    }

    private static int CheckedBlobSize(nuint size, string label)
    {
        if (size > MaxCompilerBlobBytes)
        {
            throw new InvalidOperationException(
                $"{label} blob is {size} bytes, exceeding the {MaxCompilerBlobBytes}-byte limit.");
        }

        return checked((int)size);
    }
}

/// <summary>Reports a DXC compilation failure with its diagnostics.</summary>
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
