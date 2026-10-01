using ContainerDefense.Domain;
using UnityEngine;

namespace ContainerDefense
{
    // Launch screen: logo, a bar that moves only as bundled files really load, each step with its state, and
    // honest error handling (RETRY for missing game files; CONTINUE allowed when only sound or music failed).
    public sealed partial class MatchHud
    {
        private static readonly string[] LaunchVerbs = { "Opening your profile","Loading characters","Loading the neighbourhood","Loading the interface","Loading sounds","Loading music" };
        private void LaunchScreen()
        {
            MenuBackground();
            var launch = session.Launch; var seq = launch.Sequence;
            var area = Cut.Inset(safe,M);
            var panel = Cut.Center(area,Mathf.Min(760,area.width),Mathf.Min(560,area.height));
            // Centre the two-line logo over the panel (it draws left-aligned from its rect).
            int logoSize = Mathf.RoundToInt(Mathf.Min(65 * 1.05f,panel.width / 6.2f));
            float logoWidth = HudTheme.TextStyle(logoSize,true,TextAnchor.MiddleLeft,false).CalcSize(new GUIContent("CONTAINER")).x;
            HudTheme.Logo(new Rect(panel.center.x - logoWidth / 2,Mathf.Max(area.y,panel.y - 150),panel.width,130));
            HudTheme.Panel(panel); var inner = Cut.Inset(panel,32);
            bool failed = seq.Finished && (seq.Blocked || seq.HasWarnings);
            var current = seq.Current; int index = current != null ? seq.Steps.IndexOf(current) : -1;
            string headline = seq.Blocked ? "Some game files did not load" : seq.HasWarnings && seq.Finished ? "Ready, with a problem" : seq.CanEnter ? "Ready!" : index >= 0 ? LaunchVerbs[index] + "..." : "Preparing...";
            HudTheme.Text(Cut.Top(ref inner,52,8),headline,HudTheme.CardTitle,seq.Blocked ? HudTheme.Bad : HudTheme.Ink,true,TextAnchor.MiddleCenter);
            HudTheme.Bar(Cut.Top(ref inner,30,18),seq.Progress,seq.Blocked ? HudTheme.Bad : HudTheme.Gold,Mathf.FloorToInt(seq.Progress * 100) + "%");
            // One row per step: state mark, name, and files loaded (or the error).
            foreach (var step in seq.Steps) {
                var row = Cut.Top(ref inner,36,4);
                var mark = Cut.Left(ref row,30,10);
                Color c = step.State == LaunchStepState.Done ? HudTheme.Good : step.State == LaunchStepState.Failed ? (step.Required ? HudTheme.Bad : HudTheme.Gold) : step.State == LaunchStepState.Loading ? HudTheme.Info : HudTheme.DisabledFill;
                float pulse = step.State == LaunchStepState.Loading ? .7f + Mathf.Sin(Time.unscaledTime * 8) * .3f : 1;
                HudTheme.Fill(Cut.Center(mark,18,18),new Color(c.r,c.g,c.b,pulse),9);
                HudTheme.Text(Cut.Left(ref row,170,8),step.Name + (step.Required ? "" : " (optional)"),HudTheme.Label,step.State == LaunchStepState.Pending ? HudTheme.Muted : HudTheme.Ink,true);
                string detail = step.State == LaunchStepState.Failed ? step.Error : step.State == LaunchStepState.Pending ? "Waiting" : step.Loaded + " / " + step.Total;
                HudTheme.Text(row,detail,HudTheme.Label,step.State == LaunchStepState.Failed ? c : HudTheme.Muted,false,TextAnchor.MiddleRight);
            }
            if (!failed) {
                HudTheme.Text(Cut.Bottom(ref inner,34),"Everything ships with the game. No download or sign-in needed.",HudTheme.Label,HudTheme.Muted,false,TextAnchor.MiddleCenter);
                return;
            }
            var buttons = Cut.Row(Cut.Bottom(ref inner,Touch),2,G);
            if (HudTheme.Button(buttons[0],"RETRY",ButtonKind.Primary,!launch.Running,false,HudTheme.Body)) launch.Retry();
            if (seq.Blocked) { if (HudTheme.Button(buttons[1],"QUIT",ButtonKind.Secondary,true,false,HudTheme.Body)) session.Quit(); }
            else if (HudTheme.Button(buttons[1],"PLAY WITHOUT IT",ButtonKind.Secondary,true,false,HudTheme.Body)) launch.Enter();
        }
    }
}
