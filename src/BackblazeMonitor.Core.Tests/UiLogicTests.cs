using BackblazeMonitor.Core;
using Xunit;

namespace BackblazeMonitor.Core.Tests;

public class UiLogicTests
{
    [Theory]
    [InlineData(0, ActionOutcome.Done)]
    [InlineData(1, ActionOutcome.Failed)]
    [InlineData(2, ActionOutcome.Failed)]
    [InlineData(-1, ActionOutcome.Failed)]
    public void ExitCodeMapsToOutcome(int code, ActionOutcome expected) =>
        Assert.Equal(expected, ActionOutcomes.FromExitCode(code));

    [Theory]
    [InlineData(1223, ActionOutcome.Cancelled)]
    [InlineData(5, ActionOutcome.Failed)]
    [InlineData(0, ActionOutcome.Failed)]
    public void NativeErrorMapsToOutcome(int code, ActionOutcome expected) =>
        Assert.Equal(expected, ActionOutcomes.FromNativeError(code));

    // BAC466.14: a failed stop/restart still counts as requested here; only a declined UAC prompt does not
    [Theory]
    [InlineData(ActionOutcome.Done, true)]
    [InlineData(ActionOutcome.Failed, true)]
    [InlineData(ActionOutcome.Cancelled, false)]
    public void Only_a_declined_uac_prompt_is_not_a_request(ActionOutcome outcome, bool expected) =>
        Assert.Equal(expected, ActionOutcomes.CountsAsAsked(outcome));

    [Fact]
    public void ServiceNoticeDistinguishesCancelledFromFailed()
    {
        Assert.Null(ActionNotices.ForServiceAction(ServiceAction.Stop, ActionOutcome.Done, "Stopped"));

        var c = ActionNotices.ForServiceAction(ServiceAction.Restart, ActionOutcome.Cancelled, "Running")!;
        Assert.Equal("Restart cancelled", c.Title);
        Assert.Equal("UAC prompt declined.", c.Text);
        Assert.False(c.IsError);

        var f = ActionNotices.ForServiceAction(ServiceAction.Start, ActionOutcome.Failed, "Stopped")!;
        Assert.Equal("Start failed", f.Title);
        Assert.Equal("Backblaze: Stopped", f.Text);
        Assert.True(f.IsError);
    }

    [Fact]
    public void StartupApprovedFlagUsesTheOddByte()
    {
        Assert.False(StartupApproved.IsDisabled(null));
        Assert.False(StartupApproved.IsDisabled(Array.Empty<byte>()));
        Assert.False(StartupApproved.IsDisabled(new byte[] { 2, 0, 0, 0 }));
        Assert.True(StartupApproved.IsDisabled(new byte[] { 3, 0, 0, 0 }));
        Assert.False(StartupApproved.IsDisabled("not bytes"));
    }

    [Fact]
    public void StartupNeedsTheShortcutAndNoDisableFlag()
    {
        Assert.False(StartupApproved.IsEnabled(false, null));
        Assert.True(StartupApproved.IsEnabled(true, null));
        Assert.True(StartupApproved.IsEnabled(true, new byte[] { 2 }));
        Assert.False(StartupApproved.IsEnabled(true, new byte[] { 3 }));
    }

    [Fact]
    public void AgentRunFinishesOnlyWhenTheRunMovedAndIsNotRunning()
    {
        var before = new DateTime(2026, 9, 30, 10, 0, 0);
        Assert.False(AgentRun.IsFinished(before, before, 0));
        Assert.False(AgentRun.IsFinished(before, before.AddSeconds(2), AgentRun.RunningResult));
        Assert.True(AgentRun.IsFinished(before, before.AddSeconds(2), 0));
        Assert.True(AgentRun.IsFinished(before, before.AddHours(-1), 1)); // clocks went back
        Assert.True(AgentRun.Succeeded(0));
        Assert.False(AgentRun.Succeeded(2));
    }

    [Fact]
    public void AgentStartGapOnlyWithinTheSameSecond()
    {
        var before = new DateTime(2026, 9, 30, 10, 0, 0);
        Assert.Equal(0, AgentRun.GetStartGapMs(before, before.AddSeconds(5)));
        Assert.Equal(600, AgentRun.GetStartGapMs(before, before.AddMilliseconds(500)), 3);
    }

    [Fact]
    public void LimitMessageShowsResultThenNextSwitch()
    {
        var s = Schedule.Parse("08:00-22:00=3");
        var now = new DateTime(2026, 9, 30, 12, 0, 0);
        var m = new LimitMessageState();
        m.Set("applied", true);
        Assert.Equal(MessageTone.Good, m.Tone);
        for (var i = 0; i < LimitMessageState.MessageTicks - 1; i++)
        {
            Assert.False(m.Tick(s, now));
            Assert.Equal("applied", m.Text);
        }

        Assert.True(m.Tick(s, now));
        Assert.Equal("22:00 -> none", m.Text);
        Assert.Equal(MessageTone.Neutral, m.Tone);
        Assert.False(m.Tick(s, now));
        Assert.True(m.Tick(null, now));
        Assert.Equal("", m.Text);
    }

    [Fact]
    public void QosPacerReadsFirstTickThenEveryTwentyAndSoonOnRequest()
    {
        var p = new QosReadPacer();
        Assert.True(p.Due());
        for (var i = 0; i < QosReadPacer.Period - 1; i++) Assert.False(p.Due());
        Assert.True(p.Due());
        p.RequestSoon();
        Assert.True(p.Due());
    }

    [Theory]
    [InlineData("", 0.0)]
    [InlineData("  \r\n", 0.0)]
    [InlineData("3000000", 3000000.0)]
    [InlineData("2500000.5\r\n", 2500000.5)]
    public void QosOutputParses(string output, double expected) =>
        Assert.Equal(expected, QosReadResult.Parse(output));

    [Theory]
    [InlineData("abc")]
    [InlineData("-5")]
    public void QosGarbageIsUnknown(string output) => Assert.Null(QosReadResult.Parse(output));

    [Fact]
    public void QosScriptTargetsTheOwnPolicy() =>
        Assert.Contains("'" + BzConstants.QosName + "'", QosReadResult.GetScript());

    [Fact]
    public void BzCliArgumentsQuoteThePath() =>
        Assert.Equal("configure -j \"C:\\Temp\\a b.json\"", BzAutoCommands.GetArguments("C:\\Temp\\a b.json"));
}
