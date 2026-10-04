using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

namespace backend.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CrudController : ControllerBase
{
    private readonly JsonStorage _jsonStorage;
    public CrudController(JsonStorage jsonStorage) => _jsonStorage = jsonStorage;

    [HttpGet("rooms")]
    public async Task<IActionResult> GetRooms()
    {
        if (!System.IO.File.Exists(Path.Combine(_jsonStorage.JsonDirectory, "rooms.json")))
            return NotFound("Rooms data not found.");
        var rooms = await _jsonStorage.GetRoomAsync();
        // Preserve the existing frontend's PascalCase room response.
        return new JsonResult(rooms.Select(room => new
        {
            room.Id, room.Name, room.Capacity, Status = room.IsAvailable
        }), new JsonSerializerOptions { PropertyNamingPolicy = null });
    }

    [HttpGet("bookings")]
    public async Task<IActionResult> GetBookings()
        => Ok(await _jsonStorage.GetBookingsAsync());

    [HttpGet("bookings/{id:int}")]
    public async Task<IActionResult> GetBooking(int id)
    {
        var booking = (await _jsonStorage.GetBookingsAsync()).FirstOrDefault(item => item.Id == id);
        return booking == null ? NotFound("Booking not found.") : Ok(booking);
    }

    [HttpPost("booking")]
    [HttpPost("bookings")]
    public async Task<IActionResult> BookingRoom([FromBody] BookingRoomRequest request)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await _jsonStorage.CreateBookingAsync(request, userId);
        if (result.StatusCode == 201)
            return CreatedAtAction(nameof(GetBooking), new { id = result.Booking!.Id }, result.Booking);
        return StatusCode(result.StatusCode, result.Message);
    }

    [HttpPut("bookings/{id:int}")]
    [HttpPatch("update/{id:int}")]
    public async Task<IActionResult> UpdateBooking(int id, [FromBody] BookingRoomRequest request)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await _jsonStorage.UpdateBookingAsync(id, request, userId);
        return result.StatusCode == 200
            ? Ok(result.Booking) : StatusCode(result.StatusCode, result.Message);
    }

    [HttpDelete("bookings/{id:int}")]
    [HttpDelete("delete/{id:int}")]
    public async Task<IActionResult> DeleteBooking(int id)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await _jsonStorage.DeleteBookingAsync(id, userId);
        return result.StatusCode == 204
            ? NoContent() : StatusCode(result.StatusCode, result.Message);
    }

    private bool TryGetUserId(out int id)
        => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out id) && id > 0;
}
