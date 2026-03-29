using Avalonia.Controls;
using Avalonia.Interactivity;
using ClinicVetsSystem.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ClinicVetsSystem;

public partial class VisitsView : UserControl
{
    private decimal _baseVisitPrice = 150; // מחיר ביקור בסיסי
    private Staff _currentVet;

    public VisitsView(Staff vet)
    {
        InitializeComponent();
        _currentVet = vet;
        LoadMedications();
        CheckVaccinationStatus(1); // דוגמה עבור חיה מס' 1
    }

    private async void LoadMedications()
    {
        if (SupabaseService.Client == null) return;
        try
        {
            var result = await SupabaseService.Client.From<Medication>().Get();
            lstMedications.ItemsSource = result.Models;
        }
        catch { }
    }

    private void CalculateTotal_Click(object sender, RoutedEventArgs e)
    {
        decimal totalMeds = 0;
        foreach (Medication med in lstMedications.SelectedItems)
        {
            totalMeds += med.Price;
        }
        lblTotalPrice.Text = $"סה''כ לתשלום: ₪ {_baseVisitPrice + totalMeds}";
    }

    private async void CheckVaccinationStatus(int petId)
    {
        if (SupabaseService.Client == null) return;
        try
        {
            var result = await SupabaseService.Client.From<Pet>().Where(p => p.Id == petId).Get();
            var pet = result.Models.FirstOrDefault();
            if (pet != null && pet.NeedsVaccine)
            {
                lblVaccineWarning.Text = "התראה: החיה זקוקה לחיסון שנתי!";
                lblVaccineWarning.IsVisible = true;
            }
        }
        catch { }
    }

    private async void SaveVisit_Click(object sender, RoutedEventArgs e)
    {
        if (SupabaseService.Client == null) return;
        try
        {
            var totalText = lblTotalPrice.Text?.Replace("סה''כ לתשלום: ₪ ", "") ?? "0";
            var total = decimal.TryParse(totalText, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : _baseVisitPrice;

            var visit = new Visit {
                PetId = 1, // TODO: replace with actual selected pet
                VetId = _currentVet.Id,
                Diagnosis = txtDiagnosis.Text,
                BaseCost = _baseVisitPrice,
                TotalCost = total,
                VisitDate = DateTime.Now
            };
            var insertResult = await SupabaseService.Client.From<Visit>().Insert(visit);
            var savedVisit = insertResult.Models.FirstOrDefault();

            if (savedVisit != null && lstMedications.SelectedItems.Count > 0)
            {
                foreach (Medication med in lstMedications.SelectedItems)
                {
                    var vm = new VisitMedication { VisitId = savedVisit.Id, MedicationId = med.Id };
                    await SupabaseService.Client.From<VisitMedication>().Insert(vm);
                }
            }
        }
        catch { }
    }
}