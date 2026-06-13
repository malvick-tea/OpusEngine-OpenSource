using System.Text;
using FluentAssertions;
using Opus.Content.Sample;
using Opus.Foundation;
using Xunit;

namespace Opus.Editor.Content.Tests;

public sealed class ModelInspectorTests
{
    [Fact]
    public void Inspects_the_sample_tank_model()
    {
        var glb = SampleAlphaTankGltfWriter.BuildGlb();

        var result = ModelInspector.TryInspect(glb, "sample-tank.glb");

        result.IsOk.Should().BeTrue();
        var model = result.Unwrap();
        model.MeshCount.Should().Be(1);
        model.PrimitiveCount.Should().Be(1);
        model.VertexCount.Should().Be(3);
        model.TriangleCount.Should().Be(1);
        model.MaterialReferenceCount.Should().Be(1);
        model.NodeCount.Should().Be(1);
        model.RootNodeCount.Should().Be(1);
        model.HasUvs.Should().BeTrue();
        model.HasTangents.Should().BeFalse();
        model.Meshes.Should().ContainSingle(m => m.Name == "alpha-sample-tank-marker");
    }

    [Fact]
    public void Computes_local_bounds_over_all_vertices()
    {
        var model = ModelInspector.TryInspect(SampleAlphaTankGltfWriter.BuildGlb(), "sample.glb").Unwrap();

        model.BoundsMin.X.Should().BeApproximately(-0.6f, 1e-5f);
        model.BoundsMax.X.Should().BeApproximately(0.6f, 1e-5f);
        model.BoundsMax.Y.Should().BeApproximately(0.9f, 1e-5f);
        model.BoundsSize.Y.Should().BeApproximately(0.9f, 1e-5f);
    }

    [Fact]
    public void Malformed_bytes_are_a_typed_data_validation_error()
    {
        var garbage = Encoding.ASCII.GetBytes("this is not a glb file at all");

        var result = ModelInspector.TryInspect(garbage, "broken.glb");

        result.IsErr.Should().BeTrue();
        result.UnwrapErr().Code.Should().Be(ErrorCode.DataValidationFailed);
    }
}
