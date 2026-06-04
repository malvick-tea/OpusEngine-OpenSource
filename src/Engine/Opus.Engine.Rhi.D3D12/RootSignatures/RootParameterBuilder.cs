using Silk.NET.Direct3D12;

namespace Opus.Engine.Rhi.Direct3D12;

/// <summary>
/// Value-returning factories for <see cref="RootParameter"/>. Callers stackalloc
/// the parameter array and assign each slot from these helpers. Eliminates the
/// 12-line copy-paste boilerplate that previously lived in every Create* method.
/// </summary>
internal static unsafe class RootParameterBuilder
{
    public static RootParameter Cbv(uint shaderRegister, ShaderVisibility visibility, uint registerSpace = 0u)
    {
        var p = new RootParameter
        {
            ParameterType = RootParameterType.TypeCbv,
            ShaderVisibility = visibility,
        };
        p.Anonymous.Descriptor = new RootDescriptor { ShaderRegister = shaderRegister, RegisterSpace = registerSpace };
        return p;
    }

    public static RootParameter Srv(uint shaderRegister, ShaderVisibility visibility, uint registerSpace = 0u)
    {
        var p = new RootParameter
        {
            ParameterType = RootParameterType.TypeSrv,
            ShaderVisibility = visibility,
        };
        p.Anonymous.Descriptor = new RootDescriptor { ShaderRegister = shaderRegister, RegisterSpace = registerSpace };
        return p;
    }

    public static RootParameter Uav(uint shaderRegister, ShaderVisibility visibility, uint registerSpace = 0u)
    {
        var p = new RootParameter
        {
            ParameterType = RootParameterType.TypeUav,
            ShaderVisibility = visibility,
        };
        p.Anonymous.Descriptor = new RootDescriptor { ShaderRegister = shaderRegister, RegisterSpace = registerSpace };
        return p;
    }

    public static RootParameter RootConstants(uint shaderRegister, uint num32BitValues, ShaderVisibility visibility, uint registerSpace = 0u)
    {
        var p = new RootParameter
        {
            ParameterType = RootParameterType.Type32BitConstants,
            ShaderVisibility = visibility,
        };
        p.Anonymous.Constants = new RootConstants
        {
            ShaderRegister = shaderRegister,
            RegisterSpace = registerSpace,
            Num32BitValues = num32BitValues,
        };
        return p;
    }

    public static RootParameter DescriptorTable(DescriptorRange* ranges, uint rangeCount, ShaderVisibility visibility)
    {
        var p = new RootParameter
        {
            ParameterType = RootParameterType.TypeDescriptorTable,
            ShaderVisibility = visibility,
        };
        p.Anonymous.DescriptorTable = new RootDescriptorTable
        {
            NumDescriptorRanges = rangeCount,
            PDescriptorRanges = ranges,
        };
        return p;
    }
}
