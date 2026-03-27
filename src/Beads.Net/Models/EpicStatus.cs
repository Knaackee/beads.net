namespace Beads.Net.Models;

public sealed class EpicStatus
{
    public required Issue Epic { get; set; }
    public int TotalChildren { get; set; }
    public int ClosedChildren { get; set; }
    public bool EligibleForClose => TotalChildren > 0 && ClosedChildren == TotalChildren;
    public double ProgressPercent => TotalChildren == 0 ? 0 : (double)ClosedChildren / TotalChildren * 100;
}
