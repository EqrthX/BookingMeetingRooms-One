using backend.Models;
using Dapper;
using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Text.Json;

namespace backend.Data
{
    public sealed partial class JsonStorage : IDisposable
    {
        private readonly string _connectionString;
        private readonly SemaphoreSlim _usersLock = new(1, 1);
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        public string JsonDirectory { get; }

        public JsonStorage(IConfiguration configuration, IWebHostEnvironment environment)
        {
            _connectionString = configuration.GetConnectionString("MeetingRoom")
                ?? throw new InvalidOperationException("Missing MeetingRoom connection string.");

            // Each operation loads JSON into its own temporary database.
            if (_connectionString != "Data Source=:memory:")
                throw new InvalidOperationException("This JSON adapter requires Data Source=:memory:.");

            var directory = configuration["JsonStorage:Directory"]
                ?? throw new InvalidOperationException("Missing JSON storage directory.");
            JsonDirectory = Path.Combine(environment.ContentRootPath, directory);
            Directory.CreateDirectory(JsonDirectory);
        }

        private SqliteConnection OpenConnection()
        {
            var connection = new SqliteConnection(_connectionString);
            connection.Open();
            return connection;
        }

        public async Task<Users?> GetUserByUsernameAsync(string username)
        {
            await _usersLock.WaitAsync();
            try
            {
                using var connection = OpenConnection();
                await LoadUsersAsync(connection);
                return await connection.QuerySingleOrDefaultAsync<Users>(
                    "SELECT * FROM Users WHERE Username = @Username;",
                    new { Username = username });
            }
            finally
            {
                _usersLock.Release();
            }
        }

        // Returns null when the username already exists.
        public async Task<Users?> CreateUserAsync(Users user)
        {
            await _usersLock.WaitAsync();
            try
            {
                using var connection = OpenConnection();
                await LoadUsersAsync(connection);

                var exists = await connection.ExecuteScalarAsync<long>(
                    "SELECT COUNT(*) FROM Users WHERE Username = @Username;",
                    new { user.Username });
                if (exists > 0)
                    return null;

                var nextId = await connection.ExecuteScalarAsync<int>(
                    "SELECT COALESCE(MAX(Id), 0) + 1 FROM Users;");
                var now = DateTime.UtcNow;
                var created = new Users
                {
                    Id = nextId,
                    Username = user.Username,
                    Password = user.Password,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    CreatedAt = now,
                    LastUpdatedAt = now
                };

                await InsertUsersAsync(connection, new[] { created });
                await SaveUsersAsync(connection);
                return created;
            }
            finally
            {
                _usersLock.Release();
            }
        }

        private async Task LoadUsersAsync(SqliteConnection connection)
        {
            await connection.ExecuteAsync("""
                CREATE TABLE Users (
                    Id INTEGER PRIMARY KEY,
                    Username TEXT NOT NULL UNIQUE,
                    Password TEXT NOT NULL,
                    FirstName TEXT NOT NULL,
                    LastName TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    LastUpdatedAt TEXT NOT NULL
                );
                """);

            var path = Path.Combine(JsonDirectory, "users.json");
            if (!File.Exists(path))
            {
                await WriteUsersFileAsync(new List<Users>());
            }

            var json = await File.ReadAllTextAsync(path);
            // Invalid JSON must fail instead of silently overwriting existing data.
            var users = JsonSerializer.Deserialize<List<Users>>(json, JsonOptions)
                ?? throw new JsonException("users.json must contain a JSON array.");
            await InsertUsersAsync(connection, users);
        }

        private static async Task InsertUsersAsync(
            SqliteConnection connection, IEnumerable<Users> users)
        {
            // This INSERT loads existing JSON rows as well as newly created users.
            await connection.ExecuteAsync("""
                INSERT INTO Users
                    (Id, Username, Password, FirstName, LastName, CreatedAt, LastUpdatedAt)
                VALUES
                    (@Id, @Username, @Password, @FirstName, @LastName, @CreatedAt, @LastUpdatedAt);
                """, users.Select(user => new
                {
                    user.Id,
                    user.Username,
                    user.Password,
                    user.FirstName,
                    user.LastName,
                    CreatedAt = user.CreatedAt.ToString("O", CultureInfo.InvariantCulture),
                    LastUpdatedAt = user.LastUpdatedAt.ToString("O", CultureInfo.InvariantCulture)
                }));
        }

        private async Task SaveUsersAsync(SqliteConnection connection)
        {
            var users = (await connection.QueryAsync<Users>(
                "SELECT * FROM Users ORDER BY Id;")).ToList();
            await WriteUsersFileAsync(users);
        }

        private async Task WriteUsersFileAsync(List<Users> users)
        {
            var path = Path.Combine(JsonDirectory, "users.json");
            var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await File.WriteAllTextAsync(
                    temporaryPath, JsonSerializer.Serialize(users, JsonOptions));
                // Replace only after the complete JSON has been written.
                File.Move(temporaryPath, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        public async Task<List<Room>> GetRoomAsync()
        {
            using var connection = OpenConnection();
            await LoadRoomsAsync(connection);
            return (await connection.QueryAsync<Room>("SELECT * FROM Rooms ORDER BY Id;")).ToList();
        }

        public void Dispose() { _usersLock.Dispose(); _bookingsLock.Dispose(); }
    }
}
