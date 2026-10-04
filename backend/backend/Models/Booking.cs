namespace backend.Models;

public class Booking
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public int UserId { get; set; }
    public string Title { get; set; } = "";
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
}

// Response data includes the current owner name; bookings.json still stores only UserId.
public record BookingResponse(int Id, int RoomId, int UserId, string Title,
    DateTimeOffset StartTime, DateTimeOffset EndTime, string OwnerName);

public record BookingResult(int StatusCode, string Message, BookingResponse? Booking = null);
