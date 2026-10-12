using Xunit;

namespace FocusOn.Tests;

public sealed class ResidencyTests
{
    [Fact]
    public void Unix_aliases_activate_the_same_writer_including_case_insensitive_Mac_volumes()
    {
        if (OperatingSystem.IsWindows()) return;
        var directory = Path.Combine(Path.GetTempPath(), "FocusOn-alias-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var primary = new SingleInstance(Path.Combine(directory, "data"));
            using var activated = new ManualResetEventSlim(); primary.Listen(activated.Set);
            Directory.CreateSymbolicLink(Path.Combine(directory, "alias"), Path.Combine(directory, "data"));
            using (var alias = new SingleInstance(Path.Combine(directory, "alias")))
            {
                Assert.False(alias.IsPrimary); alias.SignalExisting();
                Assert.True(activated.Wait(TimeSpan.FromSeconds(5)));
            }
            if (Directory.Exists(Path.Combine(directory, "DATA")))
            {
                activated.Reset(); using var caseAlias = new SingleInstance(Path.Combine(directory, "DATA"));
                Assert.False(caseAlias.IsPrimary); caseAlias.SignalExisting();
                Assert.True(activated.Wait(TimeSpan.FromSeconds(5)));
            }
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void Startup_command_uses_the_exact_fixed_program_location_with_startup_mode()
    {
        Assert.Equal("\"C:\\Program Files\\FocusOn\\FocusOn.exe\" --startup",
            StartupRegistration.BuildCommand(@"C:\Program Files\FocusOn\FocusOn.exe"));
    }

    [Theory]
    [InlineData("FocusOn.exe")]
    [InlineData(@"C:\Apps\FocusOn.dll")]
    [InlineData("C:\\Apps\\bad\"name.exe")]
    public void Startup_command_rejects_locations_that_cannot_be_registered_as_a_fixed_program(string path)
    {
        Assert.Throws<ArgumentException>(() => StartupRegistration.BuildCommand(path));
    }

    [Fact]
    public void Startup_command_rejects_a_location_exceeding_the_Windows_Run_limit()
    {
        var path = @"C:\Apps\" + new string('a', 260) + @"\FocusOn.exe";
        Assert.Throws<ArgumentException>(() => StartupRegistration.BuildCommand(path));
    }

    [Fact]
    public void Duplicate_start_cannot_own_recording_and_wakes_the_existing_instance()
    {
        var directory = Path.Combine(Path.GetTempPath(), "FocusOn-instance-" + Guid.NewGuid().ToString("N"));
        using var primary = new SingleInstance(directory);
        Assert.True(primary.IsPrimary);
        using var activated = new ManualResetEventSlim();
        primary.Listen(activated.Set);
        bool? secondaryIsPrimary = null;
        Exception? error = null;
        var duplicate = new Thread(() =>
        {
            try
            {
                using var secondary = new SingleInstance(directory);
                secondaryIsPrimary = secondary.IsPrimary;
                secondary.SignalExisting();
            }
            catch (Exception exception) { error = exception; }
        });
        duplicate.Start();
        Assert.True(duplicate.Join(TimeSpan.FromSeconds(5)));
        Assert.Null(error);
        Assert.False(secondaryIsPrimary);
        Assert.True(activated.Wait(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void Activation_sent_during_initialization_is_delivered_when_listening_starts()
    {
        var directory = Path.Combine(Path.GetTempPath(), "FocusOn-instance-" + Guid.NewGuid().ToString("N"));
        using var primary = new SingleInstance(directory);
        using var activated = new ManualResetEventSlim();
        Exception? error = null;
        var duplicate = new Thread(() =>
        {
            try
            {
                using var secondary = new SingleInstance(directory);
                secondary.SignalExisting();
            }
            catch (Exception exception) { error = exception; }
        });
        duplicate.Start();
        Assert.True(duplicate.Join(TimeSpan.FromSeconds(5)));
        Assert.Null(error);
        primary.Listen(activated.Set);
        Assert.True(activated.Wait(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void Explicit_release_allows_reopening_and_separate_data_directories_do_not_share_a_writer()
    {
        var directory = Path.Combine(Path.GetTempPath(), "FocusOn-instance-" + Guid.NewGuid().ToString("N"));
        var primary = new SingleInstance(directory);
        Assert.True(primary.IsPrimary);
        using (var isolated = new SingleInstance(directory + "-smoke")) Assert.True(isolated.IsPrimary);
        primary.Dispose();
        using var reopened = new SingleInstance(directory);
        Assert.True(reopened.IsPrimary);
    }
}
