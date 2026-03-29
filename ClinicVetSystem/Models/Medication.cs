using Postgrest.Attributes;
using Postgrest.Models;
using System;

namespace ClinicVetsSystem.Models;

[Table("medications")]
public class Medication : BaseModel {
    [PrimaryKey("id")] public int Id { get; set; }
    [Column("name")] public string Name { get; set; }
    [Column("price")] public decimal Price { get; set; }

    public override string ToString() => $"{Name} — ₪{Price}";
}