using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Opus.Engine.Pal.Windows.Filesystem;
using Opus.Engine.Pal.Windows.Persistence;
using Opus.Foundation;
using Opus.Persistence;
using Xunit;

namespace Opus.Persistence.Tests;

/// <summary>Integration tests for <see cref="FileSaveStore"/> against a real
/// <see cref="WindowsVfs"/> rooted in a per-test temporary directory. Filesystem-level
/// behaviour (atomic rename, missing-file handling, delete) is exercised end-to-end —
/// the test isn't mocking IVfs, so any regression in WindowsVfs surfaces here too.</summary>
public sealed class FileSaveStoreTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly WindowsVfs _vfs;
    private readonly FileSaveStore _store;

    public FileSaveStoreTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"opus-savestore-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
        _vfs = new WindowsVfs(resRoot: _tempRoot, userRoot: _tempRoot);
        _store = new FileSaveStore(_vfs);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup — Windows may hold a file handle briefly after delete.
        }
    }

    [Fact]
    public async Task ReadAsync_returns_NotFound_when_slot_does_not_exist()
    {
        var result = await _store.ReadAsync(SaveSlot.Autosave, CancellationToken.None);

        result.IsErr.Should().BeTrue();
        result.UnwrapErr().Code.Should().Be(ErrorCode.NotFound);
    }

    [Fact]
    public async Task WriteAsync_then_ReadAsync_round_trips_payload()
    {
        var payload = new byte[] { 0x47, 0x52, 0x50, 0x53, 0x41, 0x56, 0x31 };
        var write = await _store.WriteAsync(new SaveSlot(3), payload, CancellationToken.None);
        write.IsOk.Should().BeTrue();

        var read = await _store.ReadAsync(new SaveSlot(3), CancellationToken.None);
        read.IsOk.Should().BeTrue();
        read.Unwrap().Should().Equal(payload);
    }

    [Fact]
    public async Task WriteAsync_overwrites_previous_payload_atomically()
    {
        var slot = new SaveSlot(1);
        await _store.WriteAsync(slot, new byte[] { 0xAA }, CancellationToken.None);
        await _store.WriteAsync(slot, new byte[] { 0xBB, 0xCC }, CancellationToken.None);

        var read = await _store.ReadAsync(slot, CancellationToken.None);
        read.IsOk.Should().BeTrue();
        read.Unwrap().Should().Equal(new byte[] { 0xBB, 0xCC });
    }

    [Fact]
    public async Task ExistsAsync_returns_false_before_write_and_true_after()
    {
        var slot = new SaveSlot(2);
        (await _store.ExistsAsync(slot, CancellationToken.None)).Should().BeFalse();
        await _store.WriteAsync(slot, new byte[] { 0x01 }, CancellationToken.None);
        (await _store.ExistsAsync(slot, CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_removes_the_slot_file()
    {
        var slot = new SaveSlot(4);
        await _store.WriteAsync(slot, new byte[] { 0xDE, 0xAD }, CancellationToken.None);
        (await _store.ExistsAsync(slot, CancellationToken.None)).Should().BeTrue();

        var del = await _store.DeleteAsync(slot, CancellationToken.None);
        del.IsOk.Should().BeTrue();
        (await _store.ExistsAsync(slot, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_is_noop_when_slot_does_not_exist()
    {
        var result = await _store.DeleteAsync(new SaveSlot(99), CancellationToken.None);

        result.IsOk.Should().BeTrue();
    }

    [Fact]
    public void ResolveSlotPath_uses_autosave_filename_for_slot_zero()
    {
        FileSaveStore.ResolveSlotPath(SaveSlot.Autosave)
            .Should().Be($"user://{FileSaveStore.SavesDirectory}/{FileSaveStore.AutosaveFileName}");
    }

    [Fact]
    public void ResolveSlotPath_uses_indexed_filename_for_manual_slots()
    {
        FileSaveStore.ResolveSlotPath(new SaveSlot(5))
            .Should().Be($"user://{FileSaveStore.SavesDirectory}/slot_5{FileSaveStore.SaveFileExtension}");
    }

    [Fact]
    public async Task Slot_zero_and_slot_one_persist_independently()
    {
        await _store.WriteAsync(SaveSlot.Autosave, new byte[] { 0x00 }, CancellationToken.None);
        await _store.WriteAsync(new SaveSlot(1), new byte[] { 0x11 }, CancellationToken.None);

        var zero = await _store.ReadAsync(SaveSlot.Autosave, CancellationToken.None);
        var one = await _store.ReadAsync(new SaveSlot(1), CancellationToken.None);

        zero.Unwrap().Should().Equal(new byte[] { 0x00 });
        one.Unwrap().Should().Equal(new byte[] { 0x11 });
    }
}
