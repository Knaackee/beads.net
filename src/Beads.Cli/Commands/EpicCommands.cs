using System.CommandLine;

namespace Beads.Cli.Commands;

internal static class EpicCommands
{
    internal static void Register(RootCommand root)
    {
        var epic = new Command("epic", "Epic management");

        var statusCmd = new Command("status", "Show epic status");
        var statusId = new Argument<string>("id") { Description = "Epic issue ID" };
        statusCmd.Add(statusId);
        statusCmd.SetAction(pr => Cli.Run(pr, client =>
        {
            var s = client.Epics.Status(pr.GetRequiredValue(statusId));
            if (Cli.IsJson(pr))
                Cli.WriteJson(s);
            else
            {
                Console.WriteLine($"Epic:     {s.Epic.Id} — {s.Epic.Title}");
                Console.WriteLine($"Progress: {s.ClosedChildren}/{s.TotalChildren} ({s.ProgressPercent:F0}%)");
                Console.WriteLine($"Eligible: {(s.EligibleForClose ? "yes" : "no")}");
            }
        }));

        var ceCmd = new Command("close-eligible", "Check if epic can be closed");
        var ceId = new Argument<string>("id") { Description = "Epic issue ID" };
        ceCmd.Add(ceId);
        ceCmd.SetAction(pr => Cli.Run(pr, client =>
        {
            var s = client.Epics.CloseEligible(pr.GetRequiredValue(ceId));
            if (Cli.IsJson(pr))
                Cli.WriteJson(new { eligible = s.EligibleForClose, closed = s.ClosedChildren, total = s.TotalChildren });
            else
                Console.WriteLine(s.EligibleForClose ? "Yes — all children closed." : $"No — {s.TotalChildren - s.ClosedChildren} open children remain.");
        }));

        epic.Add(statusCmd);
        epic.Add(ceCmd);
        root.Add(epic);
    }
}
