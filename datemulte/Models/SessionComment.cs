using Datemulte_2.Models.DataManagement;

namespace Datemulte_2.Models;

public class SessionComment
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; } 
    public Guid UserId { get; set; }
    public string CommentText { get; set; } = "";
    public Guid? ParentCommentId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EditedAt { get; set; }
    public bool IsDeleted { get; set; } = false;

    public DataSession Session { get; set; } = null!;
    public UserProfile User { get; set; } = null!;
    public SessionComment? ParentComment { get; set; }
    public ICollection<SessionComment> Replies { get; set; } = new List<SessionComment>();
}