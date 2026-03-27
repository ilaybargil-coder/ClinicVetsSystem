using System;
using Postgrest.Attributes;
using Postgrest.Models;

namespace ClinicVetsSystem.Models;

[Table("staff")]
public class Staff : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("username")]
    public string Username { get; set; } = string.Empty;

    [Column("password")]
    public string Password { get; set; } = string.Empty;

    [Column("employee_number")]
    public int EmployeeNumber { get; set; }

    [Column("email")]
    public string Email { get; set; } = string.Empty;

    [Column("role")]
    public string Role { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }
}
