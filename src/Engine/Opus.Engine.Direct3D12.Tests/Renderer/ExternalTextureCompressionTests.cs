using FluentAssertions;
using Opus.Content.Textures;
using Opus.Engine.Renderer.Direct3D12.Scene;
using Opus.Engine.Rhi;
using Xunit;

namespace Opus.Engine.Direct3D12.Tests.Renderer;

/// <summary>Pins the block-compression policy <see cref="ExternalTextureCompression"/> applies per
/// authored map kind: colour data (base colour, emissive) compresses BC7 and samples through an
/// sRGB view; the packed ORM is linear BC7; tangent-space normals are BC5. A regression here would
/// silently mis-gamma a city's albedo or destroy normal precision.</summary>
public sealed class ExternalTextureCompressionTests
{
    [Theory]
    [InlineData(ExternalMaterialMapKind.BaseColor, BlockCompressionFormat.Bc7, RhiTextureFormat.Bc7UnormSrgb)]
    [InlineData(ExternalMaterialMapKind.Emissive, BlockCompressionFormat.Bc7, RhiTextureFormat.Bc7UnormSrgb)]
    [InlineData(ExternalMaterialMapKind.Orm, BlockCompressionFormat.Bc7, RhiTextureFormat.Bc7Unorm)]
    [InlineData(ExternalMaterialMapKind.Normal, BlockCompressionFormat.Bc5, RhiTextureFormat.Bc5Unorm)]
    public void For_maps_each_kind_to_its_encoder_and_gpu_format(
        ExternalMaterialMapKind kind, BlockCompressionFormat encoder, RhiTextureFormat gpu)
    {
        var (resolvedEncoder, resolvedGpu) = ExternalTextureCompression.For(kind);

        resolvedEncoder.Should().Be(encoder);
        resolvedGpu.Should().Be(gpu);
    }
}
