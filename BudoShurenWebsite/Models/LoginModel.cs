using System.ComponentModel.DataAnnotations;

namespace BudoShurenWebsite.Models
{
    public class UsernameLoginModel
    {
        [Required]
        public string Email { get; set; }
    }

    public class LoginModel
    {
        [Required]
        public string Email { get; set; }

        [Required]
        public string Password { get; set; }

        public bool RememberMe { get; set; }
    }
}
