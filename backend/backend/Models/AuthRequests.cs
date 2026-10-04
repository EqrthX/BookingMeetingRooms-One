namespace backend.Models
{
    // Keep the existing names so references in the current project remain valid.
    public class LoginReqest
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
    }

    public class RegisterReqest : LoginReqest
    {
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
    }
}
