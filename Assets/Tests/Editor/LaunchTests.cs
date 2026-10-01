using System;
using ContainerDefense.Domain;

// Launch screen rules: progress counts files actually loaded, required failures block with a retry,
// optional failures only warn, and a retry reloads just what failed.
public static class LaunchTests
{
    private static int passed;
    public static string Run()
    {
        passed = 0;
        Check("Progress counts loaded files; entry opens only when every step has finished", () => {
            var l = New(); True(l.Progress == 0 && !l.Finished && !l.CanEnter && l.Current.Name == "Save data");
            var save = l.Find("Save data"); l.Begin(save); l.Complete(save);
            var art = l.Find("Characters"); l.Begin(art); l.Report(art,3); True(l.Current == art && Near(l.Progress,4f / 13));
            l.Report(art,99); True(art.Loaded == 7); l.Complete(art);
            var music = l.Find("Music"); l.Begin(music); l.Complete(music);
            True(l.Finished && l.CanEnter && !l.Blocked && !l.HasWarnings && l.Progress == 1);
        });
        Check("A required failure blocks entry; retry reloads only the failed step", () => {
            var l = New(); var save = l.Find("Save data"); l.Begin(save); l.Complete(save);
            var art = l.Find("Characters"); l.Begin(art); l.Report(art,5); l.Fail(art,"kiko_default missing");
            var music = l.Find("Music"); l.Begin(music); l.Complete(music);
            True(l.Finished && l.Blocked && !l.CanEnter && art.Error == "kiko_default missing");
            l.Retry(); True(l.Attempts == 1 && art.State == LaunchStepState.Pending && art.Loaded == 0 && art.Error == null);
            True(save.State == LaunchStepState.Done && music.State == LaunchStepState.Done && !l.Finished && l.Current == art);
            l.Begin(art); l.Complete(art); True(l.CanEnter);
        });
        Check("An optional failure only warns; the game can still be entered", () => {
            var l = New(); foreach (var s in l.Steps) { l.Begin(s); if (s.Name == "Music") l.Fail(s,null); else l.Complete(s); }
            True(l.Finished && l.CanEnter && l.HasWarnings && !l.Blocked && l.Find("Music").Error == "Could not load.");
        });
        Check("Reports outside a running step are ignored", () => {
            var l = New(); var art = l.Find("Characters");
            l.Report(art,4); l.Complete(art); l.Fail(art,"x"); True(art.State == LaunchStepState.Pending && art.Loaded == 0);
            l.Begin(art); l.Complete(art); l.Fail(art,"late"); True(art.State == LaunchStepState.Done && art.Error == null);
        });
        return passed + " launch scenarios passed.";
    }
    private static LaunchSequence New() { return new LaunchSequence().Add("Save data",true,1).Add("Characters",true,7).Add("Music",false,5); }
    private static bool Near(float a,float b) { return Math.Abs(a - b) < 1e-4f; }
    private static void Check(string name,Action test) { test(); passed++; Console.WriteLine("PASS " + name); }
    private static void True(bool value) { if (!value) throw new Exception("Launch assertion failed"); }
}
