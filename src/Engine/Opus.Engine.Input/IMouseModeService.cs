namespace Opus.Engine.Input;

public interface IMouseModeService
{
    bool IsRelativeMouseMode { get; }

    void SetRelativeMouseMode(bool enabled);
}
