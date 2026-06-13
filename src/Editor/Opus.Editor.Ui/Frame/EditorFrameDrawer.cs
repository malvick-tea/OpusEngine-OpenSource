using System;
using Opus.Engine.Ui;

namespace Opus.Editor.Ui;

/// <summary>
/// Replays an <see cref="EditorFrameView"/> onto an <see cref="IDrawSurface"/>: clears the window, fills the
/// chrome panels, draws the projected viewport lines coloured by role, and renders the toolbar / status text
/// and the live pseudo-code mirror clipped to its panel. Backend-agnostic — it talks only to the
/// <see cref="IDrawSurface"/> contract, so it is unit-tested against a recording surface and runs unchanged
/// on the D3D12 draw surface (one render path, ADR-0028 / ADR-0033).
/// </summary>
public sealed class EditorFrameDrawer
{
    public const int ToolbarTextSize = 16;
    public const int StatusTextSize = 14;
    public const int PseudoCodeTextSize = 14;

    private const int TextPadX = 10;
    private const int ButtonTextPadX = 8;
    private const int LineThickness = 1;
    private const int SelectionThickness = 2;
    private const int GizmoThickness = 3;
    private const int PanelBorderThickness = 1;
    private const int PseudoCodeLineHeight = 18;
    private const int PanelHeaderHeight = 26;
    private const int PanelHeaderPadY = 6;

    /// <summary>The pixel column inside the inspector panel where row values start — the labels get the
    /// left side, the values line up on the right.</summary>
    private const int InspectorValueColumnX = 90;

    /// <summary>The pixel column inside the stats overlay where row values start.</summary>
    private const int StatsValueColumnX = 110;

    public void Draw(IDrawSurface surface, EditorFrameView view)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(view);

        surface.Clear(EditorViewportColors.WindowBackground);
        DrawViewport(surface, view);
        DrawToolbar(surface, view);
        DrawToolbarButtons(surface, view);
        DrawOutliner(surface, view);
        DrawInspector(surface, view);
        DrawDslPanel(surface, view);
        DrawStatusBar(surface, view);
        DrawStats(surface, view);
        DrawHelp(surface, view);
        DrawSceneBrowser(surface, view);
    }

    private static void DrawStats(IDrawSurface surface, EditorFrameView view)
    {
        if (view.Stats is not { } stats)
        {
            return;
        }

        var rect = stats.Panel;
        surface.FillRect(rect.X, rect.Y, rect.Width, rect.Height, EditorViewportColors.PanelFill);
        surface.StrokeRect(rect.X, rect.Y, rect.Width, rect.Height, PanelBorderThickness, EditorViewportColors.PanelBorder);
        surface.DrawText(stats.Title, rect.X + EditorStatsOverlay.PaddingX, rect.Y + PanelHeaderPadY, StatusTextSize, EditorViewportColors.PrimaryText);

        int y = rect.Y + EditorStatsOverlay.HeaderHeight;
        int lastRowTop = rect.Bottom - EditorStatsOverlay.RowHeight;
        foreach (var row in stats.Rows)
        {
            if (y > lastRowTop)
            {
                break;
            }

            surface.DrawText(row.Label, rect.X + EditorStatsOverlay.PaddingX, y, PseudoCodeTextSize, EditorViewportColors.DimText);
            surface.DrawText(
                row.Value,
                rect.X + EditorStatsOverlay.PaddingX + StatsValueColumnX,
                y,
                PseudoCodeTextSize,
                EditorViewportColors.PrimaryText);
            y += EditorStatsOverlay.RowHeight;
        }
    }

    private static void DrawSceneBrowser(IDrawSurface surface, EditorFrameView view)
    {
        if (view.SceneBrowser is not { } browser)
        {
            return;
        }

        var rect = browser.Panel;
        surface.FillRect(rect.X, rect.Y, rect.Width, rect.Height, EditorViewportColors.PanelFill);
        surface.StrokeRect(rect.X, rect.Y, rect.Width, rect.Height, PanelBorderThickness, EditorViewportColors.PanelBorder);
        surface.DrawText(browser.Title, rect.X + TextPadX, rect.Y + PanelHeaderPadY, StatusTextSize, EditorViewportColors.PrimaryText);

        if (browser.Rows.Count == 0)
        {
            surface.DrawText(
                browser.EmptyHint,
                rect.X + EditorSceneBrowser.PaddingX,
                rect.Y + EditorSceneBrowser.HeaderHeight,
                PseudoCodeTextSize,
                EditorViewportColors.DimText);
            return;
        }

        foreach (var row in browser.Rows)
        {
            if (row.Highlighted)
            {
                surface.FillRect(
                    row.Rect.X, row.Rect.Y, row.Rect.Width, row.Rect.Height, EditorViewportColors.OutlinerSelectedFill);
            }

            var color = row.Highlighted ? EditorViewportColors.PrimaryText : EditorViewportColors.DimText;
            surface.DrawText(row.Label, row.Rect.X + EditorSceneBrowser.PaddingX, row.Rect.Y, PseudoCodeTextSize, color);
        }
    }

    private static void DrawInspector(IDrawSurface surface, EditorFrameView view)
    {
        var rect = view.Chrome.Inspector;
        surface.FillRect(rect.X, rect.Y, rect.Width, rect.Height, EditorViewportColors.PanelFill);
        surface.StrokeRect(rect.X, rect.Y, rect.Width, rect.Height, PanelBorderThickness, EditorViewportColors.PanelBorder);
        surface.DrawText(
            view.InspectorHeader, rect.X + TextPadX, rect.Y + PanelHeaderPadY, StatusTextSize, EditorViewportColors.DimText);

        foreach (var row in view.InspectorRows)
        {
            if (row.Editing)
            {
                surface.FillRect(
                    row.Rect.X, row.Rect.Y, row.Rect.Width, row.Rect.Height, EditorViewportColors.OutlinerSelectedFill);
            }

            surface.DrawText(row.Label, row.Rect.X + TextPadX, row.Rect.Y, PseudoCodeTextSize, EditorViewportColors.DimText);
            var valueColor = row.Editing || row.Editable
                ? EditorViewportColors.PrimaryText
                : EditorViewportColors.DimText;
            surface.DrawText(
                row.Value, row.Rect.X + InspectorValueColumnX, row.Rect.Y, PseudoCodeTextSize, valueColor);
        }
    }

    private static void DrawHelp(IDrawSurface surface, EditorFrameView view)
    {
        if (view.Help is not { } help)
        {
            return;
        }

        var rect = help.Panel;
        surface.FillRect(rect.X, rect.Y, rect.Width, rect.Height, EditorViewportColors.PanelFill);
        surface.StrokeRect(rect.X, rect.Y, rect.Width, rect.Height, PanelBorderThickness, EditorViewportColors.PanelBorder);
        surface.DrawText(help.Title, rect.X + TextPadX, rect.Y + PanelHeaderPadY, StatusTextSize, EditorViewportColors.PrimaryText);

        int y = rect.Y + EditorHelpOverlay.HeaderHeight;
        int lastRowTop = rect.Bottom - EditorHelpOverlay.RowHeight;
        foreach (var entry in help.Entries)
        {
            if (y > lastRowTop)
            {
                break;
            }

            surface.DrawText(entry.Keys, rect.X + EditorHelpOverlay.PaddingX, y, PseudoCodeTextSize, EditorViewportColors.DimText);
            surface.DrawText(
                entry.Description,
                rect.X + EditorHelpOverlay.PaddingX + EditorHelpOverlay.KeysColumnWidth,
                y,
                PseudoCodeTextSize,
                EditorViewportColors.PrimaryText);
            y += EditorHelpOverlay.RowHeight;
        }
    }

    private static void DrawOutliner(IDrawSurface surface, EditorFrameView view)
    {
        var rect = view.Chrome.Outliner;
        surface.FillRect(rect.X, rect.Y, rect.Width, rect.Height, EditorViewportColors.PanelFill);
        surface.StrokeRect(rect.X, rect.Y, rect.Width, rect.Height, PanelBorderThickness, EditorViewportColors.PanelBorder);
        surface.DrawText(view.OutlinerHeader, rect.X + TextPadX, rect.Y + PanelHeaderPadY, StatusTextSize, EditorViewportColors.DimText);

        foreach (var row in view.OutlinerRows)
        {
            if (row.Selected)
            {
                surface.FillRect(
                    row.Rect.X, row.Rect.Y, row.Rect.Width, row.Rect.Height, EditorViewportColors.OutlinerSelectedFill);
            }

            var color = row.Selected ? EditorViewportColors.PrimaryText : EditorViewportColors.DimText;
            surface.DrawText(row.Label, row.Rect.X + TextPadX, row.Rect.Y, PseudoCodeTextSize, color);
        }
    }

    private static void DrawToolbarButtons(IDrawSurface surface, EditorFrameView view)
    {
        foreach (var button in view.ToolbarButtons)
        {
            var rect = button.Rect;
            var fill = button.Enabled ? EditorViewportColors.ButtonFill : EditorViewportColors.ToolbarFill;
            surface.FillRect(rect.X, rect.Y, rect.Width, rect.Height, fill);
            surface.StrokeRect(rect.X, rect.Y, rect.Width, rect.Height, PanelBorderThickness, EditorViewportColors.PanelBorder);
            var textColor = button.Enabled ? EditorViewportColors.PrimaryText : EditorViewportColors.DimText;
            surface.DrawText(button.Label, rect.X + ButtonTextPadX, CenterTextY(rect, StatusTextSize), StatusTextSize, textColor);
        }
    }

    private static void DrawViewport(IDrawSurface surface, EditorFrameView view)
    {
        var rect = view.Chrome.Viewport;
        surface.FillRect(rect.X, rect.Y, rect.Width, rect.Height, EditorViewportColors.ViewportBackground);
        foreach (var line in view.ViewportLines)
        {
            surface.DrawLine(
                (int)line.A.X, (int)line.A.Y, (int)line.B.X, (int)line.B.Y, ThicknessFor(line.Role), EditorViewportColors.ForRole(line.Role));
        }
    }

    private static void DrawToolbar(IDrawSurface surface, EditorFrameView view)
    {
        var rect = view.Chrome.Toolbar;
        surface.FillRect(rect.X, rect.Y, rect.Width, rect.Height, EditorViewportColors.ToolbarFill);
        surface.DrawText(
            view.ToolbarText,
            EditorToolbarButtons.TitleStartX(rect),
            CenterTextY(rect, ToolbarTextSize),
            ToolbarTextSize,
            EditorViewportColors.PrimaryText);
    }

    private static void DrawStatusBar(IDrawSurface surface, EditorFrameView view)
    {
        var rect = view.Chrome.StatusBar;
        surface.FillRect(rect.X, rect.Y, rect.Width, rect.Height, EditorViewportColors.StatusFill);
        surface.DrawText(
            view.StatusText, rect.X + TextPadX, CenterTextY(rect, StatusTextSize), StatusTextSize, EditorViewportColors.DimText);
    }

    /// <summary>How many mirror lines fit in the pseudo-code panel below its header — the same arithmetic
    /// as <see cref="DrawDslPanel"/>'s bottom clip, so the composer's scroll window and the drawn lines can
    /// never disagree.</summary>
    public static int MirrorLineCapacity(EditorPanelRect dslPanel) =>
        Math.Max(0, (dslPanel.Height - PanelHeaderHeight) / PseudoCodeLineHeight);

    private static void DrawDslPanel(IDrawSurface surface, EditorFrameView view)
    {
        var rect = view.Chrome.DslPanel;
        surface.FillRect(rect.X, rect.Y, rect.Width, rect.Height, EditorViewportColors.PanelFill);
        surface.StrokeRect(rect.X, rect.Y, rect.Width, rect.Height, PanelBorderThickness, EditorViewportColors.PanelBorder);
        surface.DrawText(view.PseudoCodeHeader, rect.X + TextPadX, rect.Y + PanelHeaderPadY, StatusTextSize, EditorViewportColors.DimText);

        int y = rect.Y + PanelHeaderHeight;
        int lastLineTop = rect.Bottom - PseudoCodeLineHeight;
        foreach (var codeLine in view.PseudoCodeLines)
        {
            if (y > lastLineTop)
            {
                break;
            }

            if (codeLine.Length > 0)
            {
                surface.DrawText(codeLine, rect.X + TextPadX, y, PseudoCodeTextSize, EditorViewportColors.PrimaryText);
            }

            y += PseudoCodeLineHeight;
        }
    }

    private static int CenterTextY(EditorPanelRect rect, int textSize) => rect.Y + ((rect.Height - textSize) / 2);

    private static int ThicknessFor(ViewportLineRole role) => role switch
    {
        ViewportLineRole.Selection => SelectionThickness,
        ViewportLineRole.GizmoX or ViewportLineRole.GizmoY or ViewportLineRole.GizmoZ or ViewportLineRole.GizmoActive => GizmoThickness,
        _ => LineThickness,
    };
}
