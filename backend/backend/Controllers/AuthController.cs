using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly JsonStorage _jsonStorage;
        private readonly IConfiguration _configuration;

        public AuthController(JsonStorage jsonStorage, IConfiguration configuration)
        {
            _jsonStorage = jsonStorage;
            _configuration = configuration;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginReqest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) ||
                string.IsNullOrEmpty(request.Password))
                return BadRequest("Username and password are required.");

            if (Encoding.UTF8.GetByteCount(request.Password) > 72)
                return BadRequest("Password must not exceed 72 UTF-8 bytes.");

            var user = await _jsonStorage.GetUserByUsernameAsync(request.Username);
            if (user == null ||
                !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
                return Unauthorized("Invalid username or password.");

            // Exercise: issue a cookie or JWT here, then protect CRUD with Authorize.
            var token = GenerateJwtToken(user);


            return Ok(new
            {
                message = "Login successful.",
                token,
                expiration = DateTime.UtcNow.AddHours(double.Parse(_configuration["Jwt:ExpireMinutes"]!)),
                user = new
                {
                    user.Id,
                    user.Username,
                    user.FirstName,
                    user.LastName
                }
            });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterReqest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) ||
                string.IsNullOrEmpty(request.Password))
                return BadRequest("Username and password are required.");

            if (request.Password.Length < 8 ||
                Encoding.UTF8.GetByteCount(request.Password) > 72)
                return BadRequest("Password must be at least 8 characters and at most 72 UTF-8 bytes.");

            var user = new Users
            {
                Username = request.Username,
                Password = BCrypt.Net.BCrypt.HashPassword(request.Password),
                FirstName = request.FirstName,
                LastName = request.LastName
            };

            // Duplicate checking, ID assignment, and persistence happen under one lock.
            var created = await _jsonStorage.CreateUserAsync(user);
            if (created == null)
                return Conflict("Username already exists.");

            return Ok(new
            {
                message = "Registration successful.",
                created.Id,
                created.Username,
                created.FirstName,
                created.LastName
            });
        }

        private string GenerateJwtToken(Users user)
        {
            var jwtSettings = _configuration.GetSection("Jwt");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!));
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.GivenName, user.FirstName),
                new Claim(ClaimTypes.Surname, user.LastName)
            };

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
