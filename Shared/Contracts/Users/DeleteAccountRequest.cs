using System.ComponentModel.DataAnnotations;

namespace Shared.Contracts.Users;

public class DeleteAccountRequest
{
    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
    
    [Required]
    public bool ConfirmUnderstanding { get; set; }
}
