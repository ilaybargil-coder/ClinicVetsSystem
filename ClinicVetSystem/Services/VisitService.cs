using System;
using System.Collections.Generic;
using System.Linq;
using ClinicVetsSystem.Models;

namespace ClinicVetSystem.Services
{
    public static class VisitService
    {
        // חישוב עלות ביקור: מחיר בסיס + סכום מחירי התרופות
        public static decimal CalculateTotal(decimal basePrice, IEnumerable<Medication> meds) 
        {
            return basePrice + (meds?.Sum(m => m.Price) ?? 0);
        }

        // בדיקה האם חלפה שנה מהחיסון האחרון
        public static bool IsVaccineExpired(DateOnly? lastDate)
        {
            if (lastDate == null) return true;
            return lastDate.Value.ToDateTime(TimeOnly.MinValue).AddYears(1) <= DateTime.Now;
        }
    }
}