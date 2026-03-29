using Postgrest.Attributes;
using Postgrest.Models;
using System;

namespace ClinicVetsSystem.Models;

[Table("pets")]
public class Pet : BaseModel {
    [PrimaryKey("id")] public int Id { get; set; }
    [Column("name")] public string Name { get; set; }
    [Column("last_vaccination")] public DateTime LastVaccination { get; set; }
    
    // מאפיין עזר להתראה (לא נשמר ב-DB)
    public bool NeedsVaccine => (DateTime.Now - LastVaccination).TotalDays > 365;
}