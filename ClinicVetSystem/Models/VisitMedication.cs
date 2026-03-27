using System;
using Postgrest.Attributes;
using Postgrest.Models;

namespace ClinicVetsSystem.Models;

[Table("visit_medications")]
public class VisitMedication : BaseModel
{
    [PrimaryKey("visit_id", true)]
    public int VisitId { get; set; }

    [PrimaryKey("medication_id", true)]
    public int MedicationId { get; set; }
}
