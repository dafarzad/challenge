using System.ComponentModel.DataAnnotations;

namespace Lottery.Application.DTOs;

public class RegisterRequestDto
{
    [Required]
    public string FirstName { get; set; } = string.Empty;
    [Required]
    public string LastName { get; set; } = string.Empty;
    [Required]
    public string Phone { get; set; } = string.Empty;
    [Required]
    public string NationalCode { get; set; } = string.Empty;
    [Required]
    public int CampaignId { get; set; }
}
