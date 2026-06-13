using FluentAssertions;
using Opus.Engine.Input;
using Xunit;

namespace Opus.Editor.Ui.Tests;

public sealed class RenameKeyMapTests
{
    [Fact]
    public void Letter_keys_map_to_lowercase_characters()
    {
        var input = new FakeInputSource();
        input.PressKey(Key.T);
        input.PressKey(Key.A);

        RenameKeyMap.PressedCharacters(input).Should().Equal('a', 't');
    }

    [Fact]
    public void Digits_space_period_and_hyphen_are_typed()
    {
        var input = new FakeInputSource();
        input.PressKey(Key.D7);
        input.PressKey(Key.Space);
        input.PressKey(Key.Period);
        input.PressKey(Key.Hyphen);

        RenameKeyMap.PressedCharacters(input).Should().Equal('7', ' ', '.', '-');
    }

    [Fact]
    public void Non_text_keys_yield_nothing()
    {
        var input = new FakeInputSource();
        input.PressKey(Key.Enter);
        input.PressKey(Key.Escape);
        input.PressKey(Key.F2);
        input.PressKey(Key.LeftControl);

        RenameKeyMap.PressedCharacters(input).Should().BeEmpty();
    }
}
