using System;
using Postgrest.Attributes;
using Postgrest.Models;

namespace ClinicVetsSystem.Models;

[Table("medications")]
public class Medication : BaseModel
{
    [PrimaryKey("id", false)]
    public int Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("price")]
    public decimal Price { get; set; }
}
