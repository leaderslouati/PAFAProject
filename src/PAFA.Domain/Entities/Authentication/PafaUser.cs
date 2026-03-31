namespace PAFA.Domain.Entities.Authentication;  

public class PafaUser : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public string? Department { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public ICollection<PafaUserRole> UserRoles { get; set; }
        = new List<PafaUserRole>();

}
