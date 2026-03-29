using Postgrest.Attributes;
using Postgrest.Models;
using System;

namespace ClinicVetsSystem.Models;

[Table("Visits")]
public class Visit : BaseModel {
    [PrimaryKey("id")] public int Id { get; set; }
    [Column("pet_id")] public int PetId { get; set; }
    [Column("visit_date")] public DateTime VisitDate { get; set; } = DateTime.Now; // ברירת מחדל
    [Column("reason")] public string Reason { get; set; }
    [Column("diagnosis")] public string Diagnosis { get; set; }
    [Column("vet_name")] public string VetName { get; set; }
    [Column("total_cost")] public decimal TotalCost { get; set; }
}