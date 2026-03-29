using Avalonia.Controls;
using Avalonia.Interactivity;
using ClinicVetsSystem.Models;
using System;
using System.Collections.Generic;
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
        var result = await SupabaseService.Client.From<Medication>().Get();
        lstMedications.ItemsSource = result.Models;
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
        var result = await SupabaseService.Client.From<Pet>().Where(p => p.Id == petId).Get();
        var pet = result.Models.FirstOrDefault();
        
        if (pet != null && pet.NeedsVaccine)
        {
            // הצגת התראה בולטת בממשק
            lblVaccineWarning.Text = "⚠️ התראה: החיה זקוקה לחיסון שנתי!";
            lblVaccineWarning.IsVisible = true;
        }
    }

    private async void SaveVisit_Click(object sender, RoutedEventArgs e)
    {
        var total = decimal.Parse(lblTotalPrice.Text.Replace("סה''כ לתשלום: ₪ ", ""));
        
        var visit = new Visit {
            Diagnosis = txtDiagnosis.Text,
            VetName = _currentVet.Username, // שם הווטרינר המטפל
            TotalCost = total,
            VisitDate = DateTime.Now // תאריך ושעה נוכחיים
        };

        await SupabaseService.Client.From<Visit>().Insert(visit);
    }
}