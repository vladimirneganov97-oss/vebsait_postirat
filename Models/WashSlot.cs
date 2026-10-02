namespace LaundrySite.Models;

public class WashSlot
{
    public int Id { get; set; }
    public string Day { get; set; } = "";
    public string Time { get; set; } = "";

    // Admin-controlled status of the washing slot.
    public string Status { get; set; } = "Свободно";

    // Keep the old text field for compatibility/display.
    public string? ReservedBy { get; set; }

    // Reservations are now linked to a user ID, not to their name/room.
    public int? ReservedByUserId { get; set; }

    public DateTime? ReservationWeekStart { get; set; }
}
