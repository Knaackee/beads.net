using System.Security.Cryptography;
using System.Text;
using Beads.Net.Models;

namespace Beads.Net.Internal;

public static class ContentHash
{
    public static string Compute(Issue issue)
    {
        var sb = new StringBuilder();
        sb.AppendLine(issue.Title);
        sb.AppendLine(issue.Description);
        sb.AppendLine(issue.Design);
        sb.AppendLine(issue.AcceptanceCriteria);
        sb.AppendLine(issue.Notes);
        sb.AppendLine(issue.Status);
        sb.AppendLine(issue.Priority.ToString());
        sb.AppendLine(issue.IssueType);
        sb.AppendLine(issue.Assignee ?? "");
        sb.AppendLine(issue.ExternalRef ?? "");
        sb.AppendLine(issue.Pinned ? "1" : "0");
        sb.AppendLine(issue.IsTemplate ? "1" : "0");

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
