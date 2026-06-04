using System.Runtime.InteropServices;

namespace Opus.Engine.Ui.Direct3D12.Text;

/// <summary>Vertical text metrics for one font face at a fixed bake height, in pixels.
/// <see cref="Ascent"/> is positive (above the baseline); <see cref="Descent"/> is
/// negative.</summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct FontVerticalMetrics(float Ascent, float Descent, float LineGap);
