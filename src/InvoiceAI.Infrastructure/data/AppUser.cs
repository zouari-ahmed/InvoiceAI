using Microsoft.AspNetCore.Identity;

namespace InvoiceAI.Infrastructure.Data;

public class AppUser : IdentityUser
{
    public string? FullName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}