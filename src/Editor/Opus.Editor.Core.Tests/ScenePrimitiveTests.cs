using FluentAssertions;
using Xunit;

namespace Opus.Editor.Core.Tests;

public sealed class ScenePrimitiveTests
{
    [Theory]
    [InlineData(ScenePrimitiveKind.Cube, "primitive:cube")]
    [InlineData(ScenePrimitiveKind.Sphere, "primitive:sphere")]
    [InlineData(ScenePrimitiveKind.Cylinder, "primitive:cylinder")]
    [InlineData(ScenePrimitiveKind.Plane, "primitive:plane")]
    [InlineData(ScenePrimitiveKind.Cone, "primitive:cone")]
    public void Asset_ref_round_trips_through_parse(ScenePrimitiveKind kind, string expectedRef)
    {
        ScenePrimitive.AssetRef(kind).Should().Be(expectedRef);
        ScenePrimitive.TryParse(expectedRef).Should().Be(kind);
    }

    [Fact]
    public void Parse_is_case_insensitive_for_hand_edited_scenes()
    {
        ScenePrimitive.TryParse("PRIMITIVE:Cube").Should().Be(ScenePrimitiveKind.Cube);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("models/tank.glb")]
    [InlineData("primitive:torus")]
    [InlineData("primitive:")]
    public void Non_primitive_refs_parse_to_null(string? assetRef)
    {
        ScenePrimitive.TryParse(assetRef).Should().BeNull();
    }

    [Fact]
    public void Every_kind_has_a_distinct_name()
    {
        var names = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
        foreach (var kind in ScenePrimitive.Kinds)
        {
            names.Add(ScenePrimitive.DefaultName(kind)).Should().BeTrue();
        }

        names.Count.Should().Be(5);
    }
}
