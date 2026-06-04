using System;
using System.Runtime.InteropServices;

namespace Opus.Engine.Pal.Windows.Process;

/// <summary>Thin, typed wrapper over <c>user32.dll</c>'s <c>MessageBoxW</c>. Synchronous —
/// blocks until the user dismisses the dialog. Used by crash flows where async
/// continuations cannot be relied on (the CLR is about to take the process down).</summary>
internal static class WindowsMessageBox
{
    /// <summary>Button layouts the wrapper exposes. Values mirror the Win32 <c>MB_*</c>
    /// constants so the underlying interop accepts them verbatim.</summary>
    public enum Buttons : uint
    {
        Ok = 0x0,
        OkCancel = 0x1,
        AbortRetryIgnore = 0x2,
        YesNoCancel = 0x3,
        YesNo = 0x4,
        RetryCancel = 0x5,
    }

    /// <summary>Icon decorations. Maps to Win32 <c>MB_ICON*</c>.</summary>
    public enum Icon : uint
    {
        None = 0x0,
        Error = 0x10,
        Question = 0x20,
        Warning = 0x30,
        Information = 0x40,
    }

    /// <summary>Result codes returned by <c>MessageBoxW</c>. Values mirror Win32
    /// <c>ID*</c> constants.</summary>
    public enum Result
    {
        Failed = 0,
        Ok = 1,
        Cancel = 2,
        Abort = 3,
        Retry = 4,
        Ignore = 5,
        Yes = 6,
        No = 7,
    }

    public static Result Show(string text, string caption, Buttons buttons, Icon icon = Icon.None)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(caption);
        var code = MessageBoxW(IntPtr.Zero, text, caption, (uint)buttons | (uint)icon);
        return (Result)code;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "MessageBoxW")]
    private static extern int MessageBoxW(IntPtr hWnd, string lpText, string lpCaption, uint uType);
}
