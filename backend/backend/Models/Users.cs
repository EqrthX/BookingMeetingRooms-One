namespace backend.Models
{
    public class Users
    {
        public int Id { get; set; }
        public string Username { get; set; } = "";
        // Stored BCrypt hash; never include this field in API responses.
        public string Password { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastUpdatedAt { get; set; }
    }
}
