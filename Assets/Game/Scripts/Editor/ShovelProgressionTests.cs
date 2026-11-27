#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

public sealed class ShovelProgressionTests
{
    [SetUp]
    public void SetUp() => GameState.ResetRun();

    [TearDown]
    public void TearDown() => GameState.ResetRun();

    [Test]
    public void Shovel_CannotBeTakenBeforeKeeperNote()
    {
        Assert.That(GameState.TryTakeShovel(), Is.False);
        Assert.That(GameState.HasShovel, Is.False);
        Assert.That(GameState.GetNotes(), Is.Empty);
    }

    [Test]
    public void Shovel_IsUnlockedByKeeperNote_NotEveningLetter()
    {
        GameState.GiveLetter(1, true);
        Assert.That(GameState.TryTakeShovel(), Is.False);

        GameState.MarkDayEventOnce(KeeperNoteInteractable.ReadEventId);
        Assert.That(GameState.TryTakeShovel(), Is.True);
        Assert.That(GameState.HasShovel, Is.True);
    }

    [Test]
    public void Shovel_CanOnlyBeTakenOnce_WithoutDuplicateNotes()
    {
        GameState.MarkDayEventOnce(KeeperNoteInteractable.ReadEventId);
        Assert.That(GameState.TryTakeShovel(), Is.True);
        Assert.That(GameState.TryTakeShovel(), Is.False);
        Assert.That(GameState.GetNotes().Count, Is.EqualTo(1));
    }

    [Test]
    public void ResetRun_RemovesShovelAndNoteUnlock()
    {
        GameState.MarkDayEventOnce(KeeperNoteInteractable.ReadEventId);
        GameState.TryTakeShovel();

        GameState.ResetRun();

        Assert.That(GameState.HasShovel, Is.False);
        Assert.That(GameState.TryTakeShovel(), Is.False);
        Assert.That(GameState.GetNotes(), Is.Empty);
    }
}
#endif
