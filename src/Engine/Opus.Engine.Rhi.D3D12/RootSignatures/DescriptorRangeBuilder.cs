using Silk.NET.Direct3D12;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>
/// Value-returning factories for <see cref="DescriptorRange"/>. Callers stackalloc
/// the range array and assign each slot. Defaults <c>OffsetInDescriptorsFromTableStart</c>
/// to 0 so single-range tables stay one-liners. Suffixed with <c>Range</c> so
/// <c>using static</c> doesn't collide with <see cref="RootParameterBuilder"/>.
/// </summary>
internal static class DescriptorRangeBuilder
{
    public static DescriptorRange SrvRange(uint count, uint baseShaderRegister = 0u, uint registerSpace = 0u, uint offsetFromTableStart = 0u) =>
        new()
        {
            RangeType = DescriptorRangeType.Srv,
            NumDescriptors = count,
            BaseShaderRegister = baseShaderRegister,
            RegisterSpace = registerSpace,
            OffsetInDescriptorsFromTableStart = offsetFromTableStart,
        };

    public static DescriptorRange UavRange(uint count, uint baseShaderRegister = 0u, uint registerSpace = 0u, uint offsetFromTableStart = 0u) =>
        new()
        {
            RangeType = DescriptorRangeType.Uav,
            NumDescriptors = count,
            BaseShaderRegister = baseShaderRegister,
            RegisterSpace = registerSpace,
            OffsetInDescriptorsFromTableStart = offsetFromTableStart,
        };

    public static DescriptorRange CbvRange(uint count, uint baseShaderRegister = 0u, uint registerSpace = 0u, uint offsetFromTableStart = 0u) =>
        new()
        {
            RangeType = DescriptorRangeType.Cbv,
            NumDescriptors = count,
            BaseShaderRegister = baseShaderRegister,
            RegisterSpace = registerSpace,
            OffsetInDescriptorsFromTableStart = offsetFromTableStart,
        };
}
