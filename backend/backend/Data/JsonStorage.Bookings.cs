using backend.Models;
using Dapper;
using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace backend.Data;

public sealed partial class JsonStorage
{
    private readonly SemaphoreSlim _bookingsLock = new(1, 1);

    private async Task LoadRoomsAsync(SqliteConnection connection)
    {
        var path = Path.Combine(JsonDirectory, "rooms.json");
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        var rooms = document.RootElement.EnumerateArray().Select(item => new Room
        {
            Id = item.GetProperty("Id").GetInt32(),
            Name = item.GetProperty("Name").GetString() ?? "",
            Capacity = item.GetProperty("Capacity").GetInt32(),
            IsAvailable = item.TryGetProperty("Status", out var status)
                ? status.GetString() ?? ""
                : item.TryGetProperty("IsAvailable", out var available)
                    ? available.GetString() ?? "" : ""
        }).ToList();
        await connection.ExecuteAsync("""
            CREATE TABLE Rooms (
                Id INTEGER PRIMARY KEY, Name TEXT NOT NULL,
                Capacity INTEGER NOT NULL, IsAvailable TEXT NOT NULL);
            """);
        await connection.ExecuteAsync("""
            INSERT INTO Rooms (Id, Name, Capacity, IsAvailable)
            VALUES (@Id, @Name, @Capacity, @IsAvailable);
            """, rooms);
    }

    // Use UTC ticks in SQLite so different timezone offsets compare correctly.
    private sealed class BookingRow
    {
        public int Id { get; set; }
        public int RoomId { get; set; }
        public int UserId { get; set; }
        public string Title { get; set; } = "";
        public long StartTicks { get; set; }
        public long EndTicks { get; set; }
        public string OwnerName { get; set; } = "";
        public Booking ToBooking() => new()
        {
            Id = Id, RoomId = RoomId, UserId = UserId, Title = Title,
            StartTime = new DateTimeOffset(StartTicks, TimeSpan.Zero),
            EndTime = new DateTimeOffset(EndTicks, TimeSpan.Zero)
        };
    }

    private async Task LoadBookingsAsync(SqliteConnection connection)
    {
        await connection.ExecuteAsync("""
            CREATE TABLE Bookings (
                Id INTEGER PRIMARY KEY, RoomId INTEGER NOT NULL,
                UserId INTEGER NOT NULL, Title TEXT NOT NULL,
                StartTicks INTEGER NOT NULL, EndTicks INTEGER NOT NULL);
            """);
        var path = Path.Combine(JsonDirectory, "bookings.json");
        if (!File.Exists(path))
            await WriteBookingsFileAsync(new List<Booking>());
        var items = JsonSerializer.Deserialize<List<Booking>>(
            await File.ReadAllTextAsync(path), JsonOptions)
            ?? throw new JsonException("bookings.json must contain an array.");
        await connection.ExecuteAsync("""
            INSERT INTO Bookings (Id, RoomId, UserId, Title, StartTicks, EndTicks)
            VALUES (@Id, @RoomId, @UserId, @Title, @StartTicks, @EndTicks);
            """, items.Select(item => new
            {
                item.Id, item.RoomId, item.UserId, item.Title,
                StartTicks = item.StartTime.UtcDateTime.Ticks,
                EndTicks = item.EndTime.UtcDateTime.Ticks
            }));
    }

    public async Task<List<BookingResponse>> GetBookingsAsync()
    {
        await _bookingsLock.WaitAsync();
        try
        {
            using var connection = OpenConnection();
            await LoadBookingsAsync(connection);
            return await ReadBookingResponsesAsync(connection);
        }
        finally { _bookingsLock.Release(); }
    }

    private async Task<List<BookingResponse>> ReadBookingResponsesAsync(
        SqliteConnection connection, int? id = null)
    {
        await _usersLock.WaitAsync();
        try { await LoadUsersAsync(connection); }
        finally { _usersLock.Release(); }

        var rows = await connection.QueryAsync<BookingRow>("""
            SELECT b.*,
                COALESCE(
                    NULLIF(TRIM(COALESCE(u.FirstName, '') || ' ' || COALESCE(u.LastName, '')), ''),
                    NULLIF(u.Username, ''), 'ผู้ใช้ #' || b.UserId) AS OwnerName
            FROM Bookings b
            LEFT JOIN Users u ON u.Id = b.UserId
            WHERE @Id IS NULL OR b.Id = @Id
            ORDER BY b.StartTicks, b.Id;
            """, new { Id = id });
        return rows.Select(row =>
        {
            var booking = row.ToBooking();
            return new BookingResponse(booking.Id, booking.RoomId, booking.UserId,
                booking.Title, booking.StartTime, booking.EndTime, row.OwnerName);
        }).ToList();
    }

    public Task<BookingResult> CreateBookingAsync(BookingRoomRequest request, int userId)
        => ChangeBookingAsync(null, request, userId);

    public Task<BookingResult> UpdateBookingAsync(int id, BookingRoomRequest request, int userId)
        => ChangeBookingAsync(id, request, userId);

    public Task<BookingResult> DeleteBookingAsync(int id, int userId)
        => ChangeBookingAsync(id, null, userId);

    private async Task<BookingResult> ChangeBookingAsync(
        int? id, BookingRoomRequest? request, int userId)
    {
        if (request != null &&
            (request.RoomId <= 0 || string.IsNullOrWhiteSpace(request.Title)
            || request.Title.Length > 120 || request.StartTime == default
            || request.EndTime == default || request.StartTime >= request.EndTime))
            return new(400, "Room, title and a valid start/end time are required.");

        await _bookingsLock.WaitAsync();
        try
        {
            using var connection = OpenConnection();
            await LoadBookingsAsync(connection);

            if (id.HasValue)
            {
                var existing = await connection.QuerySingleOrDefaultAsync<BookingRow>(
                    "SELECT * FROM Bookings WHERE Id = @Id;", new { Id = id.Value });
                if (existing == null) return new(404, "Booking not found.");
                if (existing.UserId != userId) return new(403, "Only the owner can change this booking.");
            }

            if (request == null)
            {
                await connection.ExecuteAsync(
                    "DELETE FROM Bookings WHERE Id = @Id;", new { Id = id!.Value });
                await SaveBookingsAsync(connection);
                return new(204, "");
            }

            await LoadRoomsAsync(connection);
            var room = await connection.QuerySingleOrDefaultAsync<Room>(
                "SELECT * FROM Rooms WHERE Id = @Id;", new { Id = request.RoomId });
            if (room == null) return new(404, "Room not found.");
            if (room.IsAvailable.Equals("Unavailable", StringComparison.OrdinalIgnoreCase)
                || room.IsAvailable.Equals("Maintenance", StringComparison.OrdinalIgnoreCase))
                return new(409, "Room is unavailable.");

            var parameters = new
            {
                Id = id ?? 0, request.RoomId,
                StartTicks = request.StartTime.UtcDateTime.Ticks,
                EndTicks = request.EndTime.UtcDateTime.Ticks
            };
            var overlaps = await connection.ExecuteScalarAsync<int>("""
                SELECT COUNT(*) FROM Bookings
                WHERE RoomId = @RoomId AND Id <> @Id
                    AND StartTicks < @EndTicks AND EndTicks > @StartTicks;
                """, parameters);
            if (overlaps > 0) return new(409, "This room is already booked for that time.");

            var bookingId = id ?? await connection.ExecuteScalarAsync<int>(
                "SELECT COALESCE(MAX(Id), 0) + 1 FROM Bookings;");
            var booking = new Booking
            {
                Id = bookingId, RoomId = request.RoomId, UserId = userId,
                Title = request.Title.Trim(),
                StartTime = request.StartTime.ToUniversalTime(),
                EndTime = request.EndTime.ToUniversalTime()
            };
            var values = new
            {
                booking.Id, booking.RoomId, booking.UserId, booking.Title,
                StartTicks = booking.StartTime.UtcDateTime.Ticks,
                EndTicks = booking.EndTime.UtcDateTime.Ticks
            };
            if (id.HasValue)
                await connection.ExecuteAsync("""
                    UPDATE Bookings SET RoomId = @RoomId, Title = @Title,
                        StartTicks = @StartTicks, EndTicks = @EndTicks WHERE Id = @Id;
                    """, values);
            else
                await connection.ExecuteAsync("""
                    INSERT INTO Bookings (Id, RoomId, UserId, Title, StartTicks, EndTicks)
                    VALUES (@Id, @RoomId, @UserId, @Title, @StartTicks, @EndTicks);
                    """, values);

            var response = (await ReadBookingResponsesAsync(connection, bookingId)).Single();
            await SaveBookingsAsync(connection);
            return new(id.HasValue ? 200 : 201, "", response);
        }
        finally { _bookingsLock.Release(); }
    }

    private async Task SaveBookingsAsync(SqliteConnection connection)
    {
        var rows = await connection.QueryAsync<BookingRow>("SELECT * FROM Bookings ORDER BY Id;");
        await WriteBookingsFileAsync(rows.Select(row => row.ToBooking()).ToList());
    }

    private async Task WriteBookingsFileAsync(List<Booking> bookings)
    {
        var path = Path.Combine(JsonDirectory, "bookings.json");
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporaryPath,
                JsonSerializer.Serialize(bookings, JsonOptions));
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}
