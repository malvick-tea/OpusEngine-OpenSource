using System;

namespace Opus.Editor.Ui;

/// <summary>
/// Computes the editor window's 2D chrome layout from a window size: a fixed-height toolbar and status bar,
/// a right-hand column of fixed width split into the outliner, the properties inspector, and the
/// pseudo-code mirror, and the remaining area as the 3D viewport. Pure and total — any size produces a
/// valid <see cref="EditorChrome"/>; sizes below the usable minimum are clamped up rather than rejected, so
/// a live window resize never yields a degenerate layout.
/// </summary>
public static class EditorChromeLayout
{
    public const int ToolbarHeight = 32;
    public const int StatusBarHeight = 24;
    public const int DslPanelWidth = 320;
    public const int MinViewportWidth = 160;
    public const int MinViewportHeight = 120;

    /// <summary>The smallest height any right-column panel (outliner / inspector / pseudo-code) is
    /// allowed, so the split never starves one of them.</summary>
    public const int MinRightColumnPanelHeight = 48;

    /// <summary>The inspector's preferred height: header plus the tallest row set (a spot light's 15
    /// rows), so the properties never clip on a normally sized window.</summary>
    public const int PreferredInspectorHeight =
        EditorInspector.HeaderHeight + (15 * EditorInspector.RowHeight) + 6;

    /// <summary>The smallest window width that keeps both the viewport and the pseudo-code panel usable.</summary>
    public const int MinWindowWidth = MinViewportWidth + DslPanelWidth;

    /// <summary>The smallest window height that keeps the viewport usable below the toolbar and status bar.</summary>
    public const int MinWindowHeight = ToolbarHeight + StatusBarHeight + MinViewportHeight;

    public static EditorChrome Build(int windowWidth, int windowHeight)
    {
        int width = Math.Max(windowWidth, MinWindowWidth);
        int height = Math.Max(windowHeight, MinWindowHeight);

        var toolbar = new EditorPanelRect(0, 0, width, ToolbarHeight);
        var statusBar = new EditorPanelRect(0, height - StatusBarHeight, width, StatusBarHeight);

        int bodyTop = ToolbarHeight;
        int bodyHeight = height - ToolbarHeight - StatusBarHeight;
        int panelWidth = Math.Min(DslPanelWidth, width - MinViewportWidth);
        int panelLeft = width - panelWidth;

        // The inspector takes its preferred height when the column can afford it (it shrinks first on a
        // short window); the outliner and pseudo-code mirror split what remains two-to-three.
        int inspectorHeight = Math.Clamp(
            PreferredInspectorHeight,
            MinRightColumnPanelHeight,
            Math.Max(MinRightColumnPanelHeight, bodyHeight - (2 * MinRightColumnPanelHeight)));
        int remaining = bodyHeight - inspectorHeight;
        int outlinerHeight = Math.Clamp(
            (remaining * 2) / 5, MinRightColumnPanelHeight, Math.Max(MinRightColumnPanelHeight, remaining - MinRightColumnPanelHeight));
        var outliner = new EditorPanelRect(panelLeft, bodyTop, panelWidth, outlinerHeight);
        var inspector = new EditorPanelRect(panelLeft, bodyTop + outlinerHeight, panelWidth, inspectorHeight);
        var dslPanel = new EditorPanelRect(
            panelLeft, bodyTop + outlinerHeight + inspectorHeight, panelWidth, bodyHeight - outlinerHeight - inspectorHeight);
        var viewport = new EditorPanelRect(0, bodyTop, panelLeft, bodyHeight);
        return new EditorChrome(toolbar, viewport, outliner, inspector, dslPanel, statusBar);
    }
}
