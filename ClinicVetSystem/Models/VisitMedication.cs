using System;
using Postgrest.Attributes;
using Postgrest.Models;

namespace ClinicVetsSystem.Models;

[Table("visit_medications")]
public class VisitMedication : BaseModel
{
    // shouldInsert חייב להיות true — אחרת ה-Insert שולח JSON ריק וה-DB דוחה
    [PrimaryKey("visit_id", true)]
    public int VisitId { get; set; }

    [PrimaryKey("medication_id", true)]
    public int MedicationId { get; set; }
}
