using System.ComponentModel.DataAnnotations;

namespace WebService.Contracts.RequestModels
{
    public class RegisterRequest
    {
        [Required]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }
}
