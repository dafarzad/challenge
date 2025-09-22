using System.ComponentModel.DataAnnotations;

namespace Lottery.Application.DTOs;

public class RegisterRequestDto
{
    [Required, StringLength(100, MinimumLength = 1)]
    public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(100, MinimumLength = 1)]
    public string LastName { get; set; } = string.Empty;
    [Required, StringLength(15, MinimumLength = 7)]
    public string Phone { get; set; } = string.Empty;
    [Required, StringLength(10, MinimumLength = 10)]
    public string NationalCode { get; set; } = string.Empty;
    [Required]
    public int CampaignId { get; set; }
}
