using System;
using Postgrest.Attributes;
using Postgrest.Models;

namespace ClinicVetsSystem.Models;

[Table("visits")]
public class Visit : BaseModel
{
    [PrimaryKey("id", false)]
    public int Id { get; set; }

    [Column("pet_id")]
    public int PetId { get; set; }

    [Column("vet_id")]
    public string VetId { get; set; } = string.Empty;  // UUID של הוטרינר

    [Column("visit_date")]
    public DateTime? VisitDate { get; set; }

    [Column("reason")]
    public string Reason { get; set; } = string.Empty;

    [Column("diagnosis")]
    public string? Diagnosis { get; set; }

    [Column("base_cost")]
    public decimal BaseCost { get; set; }

    [Column("total_cost")]
    public decimal TotalCost { get; set; }
}
