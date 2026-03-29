using Postgrest.Attributes;
using Postgrest.Models;
using System;

namespace ClinicVetsSystem.Models;

[Table("pets")]
public class Pet : BaseModel
{
    [PrimaryKey("id", false)]
    public int Id { get; set; }

    [Column("owner_id")]
    public string OwnerId { get; set; } = string.Empty;  // ת"ז הלקוח

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("pet_type")]
    public string PetType { get; set; } = string.Empty;  // כלב / חתול / זוחל / ציפור

    [Column("weight")]
    public decimal Weight { get; set; }

    [Column("birth_date")]
    public DateOnly BirthDate { get; set; }

    [Column("last_vaccine_date")]
    public DateOnly? LastVaccineDate { get; set; }

    [Column("chip_number")]
    public string? ChipNumber { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    // מאפיין עזר להתראה (לא נשמר ב-DB)
    public bool NeedsVaccine => LastVaccineDate == null ||
        (DateTime.Now - LastVaccineDate.Value.ToDateTime(TimeOnly.MinValue)).TotalDays > 365;
}
