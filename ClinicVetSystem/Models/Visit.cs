using Newtonsoft.Json;
using Postgrest.Attributes;
using Postgrest.Models;
using System;

namespace ClinicVetsSystem.Models;

[Table("visits")]
public class Visit : BaseModel {
    [PrimaryKey("id")]
    public int Id { get; set; }

    [Column("pet_id")]
    public int PetId { get; set; }

    [Column("visit_date")]
    public DateTime VisitDate { get; set; } = DateTime.Now;

    [Column("vet_id")]
    public string? VetId { get; set; }

    // מאפיין עזר לתצוגה בלבד — לא נשמר ב-DB
    [JsonIgnore]
    public string VetName { get; set; } = string.Empty;

    [Column("reason")]
    public string Reason { get; set; } = string.Empty;

    [Column("diagnosis")]
    public string Diagnosis { get; set; } = string.Empty;

    [Column("base_cost")]
    public decimal BaseCost { get; set; }

    [Column("total_cost")]
    public decimal TotalCost { get; set; }
}