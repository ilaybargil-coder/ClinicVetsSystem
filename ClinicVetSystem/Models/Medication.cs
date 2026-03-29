using Postgrest.Attributes;
using Postgrest.Models;
using System;

namespace ClinicVetsSystem.Models;

[Table("Medications")]
public class Medication : BaseModel {
    [PrimaryKey("id")] public int Id { get; set; }
    [Column("name")] public string Name { get; set; }
    [Column("price")] public decimal Price { get; set; }
    [Column("stock_quantity")] public int StockQuantity { get; set; } // ניהול מלאי
}