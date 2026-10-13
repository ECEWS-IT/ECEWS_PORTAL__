using System.ComponentModel.DataAnnotations;

namespace EcewsPortal.Models;

public class EmailLoginViewModel
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [Display(Name = "Work email")]
    public string Email { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }
    public string? InfoMessage { get; set; }
    public string? RedirectUri { get; set; }
}

public class VerifyOtpViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter the authentication code.")]
    [Display(Name = "Authentication code")]
    [StringLength(8, MinimumLength = 4)]
    public string Code { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }
    public string? InfoMessage { get; set; }
    public string? RedirectUri { get; set; }
}
