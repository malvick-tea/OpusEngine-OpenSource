using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>
/// One-line builder for <see cref="InputElementDesc"/>. Caller supplies the pinned
/// semantic name pointer (from <see cref="InputSemanticNames"/> + <c>fixed</c>).
/// </summary>
internal static unsafe class InputElementHelpers
{
    public static InputElementDesc Element(byte* semanticName, Format format, uint byteOffset, uint semanticIndex = 0u) =>
        new()
        {
            SemanticName = semanticName,
            SemanticIndex = semanticIndex,
            Format = format,
            InputSlot = 0,
            AlignedByteOffset = byteOffset,
            InputSlotClass = InputClassification.PerVertexData,
            InstanceDataStepRate = 0,
        };
}
