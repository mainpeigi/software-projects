using System;

namespace Datemulte_2.Models.DataManagement;

public class SessionInvitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public string Email { get; set; } = "";
    public string InviteToken { get; set; } = "";
    public SessionPermission Permission { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public bool Accepted { get; set; } = false;
    public Guid? AcceptedByUserId { get; set; }
}