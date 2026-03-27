using System;
using Postgrest.Attributes;
using Postgrest.Models;

namespace ClinicVetsSystem.Models;

[Table("customers")]
public class Customer : BaseModel
{
    // Primary key = תעודת זהות (9 ספרות) - מוכנסת ידנית
    [PrimaryKey("id", true)]
    public string Id { get; set; } = string.Empty;

    [Column("full_name")]
    public string FullName { get; set; } = string.Empty;

    [Column("phone")]
    public string Phone { get; set; } = string.Empty;

    [Column("email")]
    public string? Email { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }
}
