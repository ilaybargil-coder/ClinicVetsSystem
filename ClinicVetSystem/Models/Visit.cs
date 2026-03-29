using Postgrest.Attributes;
using Postgrest.Models;
using System;

namespace ClinicVetsSystem.Models;

[Table("visits")]
public class Visit : BaseModel {
    [PrimaryKey("id")] public int Id { get; set; }
    [Column("pet_id")] public int PetId { get; set; }
    [Column("visit_date")] public DateTime VisitDate { get; set; } = DateTime.Now; // ברירת מחדל
    [Column("vet_id")] public string VetId { get; set; }
    [Column("reason")] public string Reason { get; set; }
    [Column("diagnosis")] public string Diagnosis { get; set; }
    [Column("base_cost")] public decimal BaseCost { get; set; }
    [Column("total_cost")] public decimal TotalCost { get; set; }
}