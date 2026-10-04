namespace backend.Models;

public class BookingRoomRequest
{
    public int RoomId { get; set; }
    public string Title { get; set; } = "";
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
}
