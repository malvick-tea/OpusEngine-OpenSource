using System;
using System.Numerics;
using FluentAssertions;
using Opus.Engine.Ui;
using Xunit;

namespace Opus.Engine.Ui.Direct3D12.Tests.Ink;

/// <summary>Headless coverage for <see cref="InkAnnotationLayer"/> — the pure persistent-marks +
/// undo/redo model behind the hand-drawn annotation surface (ADR-0033). No GPU is touched.</summary>
public sealed class InkAnnotationLayerTests
{
    private static readonly Color Ink = Color.FromRgb(220, 40, 40);

    [Fact]
    public void Begin_add_end_commits_one_stroke_with_its_points_width_and_colour()
    {
        var layer = new InkAnnotationLayer();

        layer.BeginStroke(6f, Ink);
        layer.AddPoint(new Vector2(10, 10));
        layer.AddPoint(new Vector2(40, 10));
        var committed = layer.EndStroke();

        committed.Should().BeTrue();
        layer.IsDrawing.Should().BeFalse();
        layer.CommittedStrokes.Should().ContainSingle();
        var stroke = layer.CommittedStrokes[0];
        stroke.WidthPx.Should().Be(6f);
        stroke.Color.Should().Be(Ink);
        stroke.Points.Should().Equal(new Vector2(10, 10), new Vector2(40, 10));
    }

    [Fact]
    public void AddPoint_without_a_begin_is_a_no_op()
    {
        var layer = new InkAnnotationLayer();

        layer.AddPoint(new Vector2(10, 10));

        layer.InProgressStroke.Should().BeNull();
        layer.IsDrawing.Should().BeFalse();
    }

    [Fact]
    public void AddPoint_drops_a_sample_closer_than_the_minimum_distance()
    {
        var layer = new InkAnnotationLayer(minPointDistancePx: 5f);

        layer.BeginStroke(4f, Ink);
        layer.AddPoint(new Vector2(0, 0));
        layer.AddPoint(new Vector2(2, 0));  // 2px < 5px -> dropped
        layer.AddPoint(new Vector2(20, 0)); // 18px -> kept

        layer.InProgressStroke!.Points.Should().Equal(new Vector2(0, 0), new Vector2(20, 0));
    }

    [Fact]
    public void EndStroke_with_no_points_commits_nothing()
    {
        var layer = new InkAnnotationLayer();

        layer.BeginStroke(4f, Ink);
        var committed = layer.EndStroke();

        committed.Should().BeFalse();
        layer.CommittedStrokes.Should().BeEmpty();
    }

    [Fact]
    public void A_single_point_stroke_commits_as_a_dot()
    {
        var layer = new InkAnnotationLayer();

        layer.BeginStroke(8f, Ink);
        layer.AddPoint(new Vector2(50, 50));
        layer.EndStroke();

        layer.CommittedStrokes.Should().ContainSingle()
            .Which.Points.Should().ContainSingle().Which.Should().Be(new Vector2(50, 50));
    }

    [Fact]
    public void Undo_removes_the_last_stroke_and_redo_restores_it()
    {
        var layer = new InkAnnotationLayer();
        CommitStroke(layer, new Vector2(0, 0), new Vector2(10, 0));
        CommitStroke(layer, new Vector2(0, 20), new Vector2(10, 20));

        layer.CanUndo.Should().BeTrue();
        layer.Undo().Should().BeTrue();
        layer.CommittedStrokes.Should().ContainSingle();
        layer.CanRedo.Should().BeTrue();

        layer.Redo().Should().BeTrue();
        layer.CommittedStrokes.Should().HaveCount(2);
        layer.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void Undo_on_an_empty_layer_returns_false()
    {
        var layer = new InkAnnotationLayer();

        layer.Undo().Should().BeFalse();
        layer.Redo().Should().BeFalse();
    }

    [Fact]
    public void Committing_a_new_stroke_clears_the_redo_history()
    {
        var layer = new InkAnnotationLayer();
        CommitStroke(layer, new Vector2(0, 0), new Vector2(10, 0));
        layer.Undo();
        layer.CanRedo.Should().BeTrue();

        CommitStroke(layer, new Vector2(0, 30), new Vector2(10, 30));

        layer.CanRedo.Should().BeFalse("a fresh stroke invalidates the redo branch");
        layer.CommittedStrokes.Should().ContainSingle();
    }

    [Fact]
    public void Clear_wipes_committed_strokes_and_redo()
    {
        var layer = new InkAnnotationLayer();
        CommitStroke(layer, new Vector2(0, 0), new Vector2(10, 0));
        layer.Undo();

        layer.Clear();

        layer.CommittedStrokes.Should().BeEmpty();
        layer.CanUndo.Should().BeFalse();
        layer.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void CancelStroke_abandons_the_in_progress_stroke_without_committing()
    {
        var layer = new InkAnnotationLayer();

        layer.BeginStroke(4f, Ink);
        layer.AddPoint(new Vector2(5, 5));
        layer.CancelStroke();

        layer.IsDrawing.Should().BeFalse();
        layer.InProgressStroke.Should().BeNull();
        layer.CommittedStrokes.Should().BeEmpty();
    }

    [Fact]
    public void InProgressStroke_previews_the_active_points_and_is_null_when_idle()
    {
        var layer = new InkAnnotationLayer();
        layer.InProgressStroke.Should().BeNull();

        layer.BeginStroke(5f, Ink);
        layer.AddPoint(new Vector2(1, 1));
        layer.AddPoint(new Vector2(30, 1));

        var preview = layer.InProgressStroke;
        preview.Should().NotBeNull();
        preview!.WidthPx.Should().Be(5f);
        preview.Points.Should().Equal(new Vector2(1, 1), new Vector2(30, 1));
    }

    [Fact]
    public void BeginStroke_rejects_a_non_positive_width()
    {
        var layer = new InkAnnotationLayer();

        var act = () => layer.BeginStroke(0f, Ink);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_rejects_a_negative_minimum_distance()
    {
        var act = () => new InkAnnotationLayer(minPointDistancePx: -1f);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static void CommitStroke(InkAnnotationLayer layer, params Vector2[] points)
    {
        layer.BeginStroke(4f, Ink);
        foreach (var point in points)
        {
            layer.AddPoint(point);
        }

        layer.EndStroke();
    }
}
