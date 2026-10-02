namespace LaundrySite.Models;

public class User
{
    public int Id { get; set; }

    // Public login identifier. It is not a password or a secret.
    public string PublicId { get; set; } = "";

    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string RoomNumber { get; set; } = "";

    // Passwords are never stored in plain text.
    public string PasswordHash { get; set; } = "";

    // Maximum number of reservations allowed for this user in one calendar week.
    public int WeeklyBookingLimit { get; set; } = 2;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
