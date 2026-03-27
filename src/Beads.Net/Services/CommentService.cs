using Beads.Net.Internal;
using Beads.Net.Models;
using Microsoft.Data.Sqlite;

namespace Beads.Net.Services;

public sealed class CommentService
{
    private readonly Db _db;
    private readonly BeadsConfig _config;
    private readonly EventService _events;

    internal CommentService(Db db, BeadsConfig config, EventService events)
    {
        _db = db;
        _config = config;
        _events = events;
    }

    public Comment Add(string issueId, string body, string? author = null)
    {
        var now = DateTime.UtcNow;
        var actualAuthor = author ?? _config.Actor;

        _db.Execute(
            $"INSERT INTO {_db.T("comments")} (issue_id, author, text, created_at) VALUES (@id, @author, @text, @now)",
            cmd =>
            {
                cmd.Parameters.AddWithValue("@id", issueId);
                cmd.Parameters.AddWithValue("@author", actualAuthor);
                cmd.Parameters.AddWithValue("@text", body);
                cmd.Parameters.AddWithValue("@now", now.ToString("o"));
            });

        var commentId = _db.QueryScalar<long>("SELECT last_insert_rowid()") ?? 0;
        _events.Record(issueId, "commented", null, body.Length > 100 ? body[..100] + "…" : body);

        return new Comment
        {
            Id = commentId,
            IssueId = issueId,
            Author = actualAuthor,
            Body = body,
            CreatedAt = now,
        };
    }

    public List<Comment> List(string issueId)
    {
        return _db.Query(
            $"SELECT * FROM {_db.T("comments")} WHERE issue_id = @id ORDER BY created_at",
            cmd => cmd.Parameters.AddWithValue("@id", issueId),
            IssueService.ReadComment);
    }
}
