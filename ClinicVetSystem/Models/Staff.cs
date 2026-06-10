using System;
using Postgrest.Attributes;
using Postgrest.Models;

namespace ClinicVetsSystem.Models;

[Table("staff")]
public class Staff : BaseModel
{
    // שמנו כאן סימן שאלה (Nullable) והורדנו את ה-Empty. עכשיו זה יישלח כ-null והשרת ייצר UUID לבד!
    [PrimaryKey("id", false)]
    public string? Id { get; set; }

    [Column("username")]
    public string? Username { get; set; }

    [Column("password")]
    public string? Password { get; set; }

    [Column("employee_number")]
    public int EmployeeNumber { get; set; }

    [Column("email")]
    public string? Email { get; set; }

    [Column("role")]
    public string? Role { get; set; }

    [Column("national_id")]
    public string? NationalId { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }
}