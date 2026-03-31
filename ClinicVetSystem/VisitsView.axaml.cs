using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using ClinicVetsSystem.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ClinicVetsSystem;

public partial class VisitsView : UserControl
{
    private readonly Staff _loggedInStaff;
    private List<Medication> _allMeds = new();
    private decimal _baseVisitPrice = 150;

    public VisitsView(Staff staff)
    {
        InitializeComponent();
        _loggedInStaff = staff;
        lblVetNameDisplay.Text = _loggedInStaff.Username; // הצגת שם הווטרינר בטופס
        LoadInitialData();
    }

    private async void LoadInitialData()
    {
        try 
        {
            var visitsResponse = await SupabaseService.Client!.From<Visit>()
                .Order(v => v.VisitDate, Postgrest.Constants.Ordering.Descending)
                .Get();
            
            var visits = visitsResponse.Models;
            
            // הצמדת שם הווטרינר לכל ביקור (כרגע לפי המשתמש המחובר)
            foreach (var v in visits)
            {
                // כאן בדרך כלל שולפים מה-DB, כרגע נציג את השם אם המזהה תואם
                if (v.VetId == _loggedInStaff.Id) 
                    v.VetName = _loggedInStaff.Username ?? "וטרינר";
                else
                    v.VetName = "צוות מרפאה";
            }

            dgVisits.ItemsSource = visits;

            var petsResp = await SupabaseService.Client!.From<Pet>().Get();
            cbPets.ItemsSource = petsResp.Models;

            var medsResp = await SupabaseService.Client!.From<Medication>().Get();
            _allMeds = medsResp.Models;
            lstMedications.ItemsSource = _allMeds;
        }
        catch (Exception) { }
    }

    private void OpenNewVisit_Click(object sender, RoutedEventArgs e) 
    {
        lblStatusMessage.IsVisible = false;
        lblVetNameDisplay.Text = _loggedInStaff.Username;
        NewVisitOverlay.IsVisible = true;
    }

    private void CloseOverlay_Click(object sender, RoutedEventArgs e) => NewVisitOverlay.IsVisible = false;

    private async void SaveVisit_Click(object sender, RoutedEventArgs e)
    {
        if (cbPets.SelectedItem is not Pet selectedPet)
        {
            ShowStatus("חובה לבחור חיה!", false);
            return;
        }

        var selectedMeds = lstMedications.SelectedItems?.Cast<Medication>() ?? Enumerable.Empty<Medication>();
        
        var newVisit = new Visit
        {
            PetId = selectedPet.Id,
            VisitDate = dpDate.SelectedDate?.DateTime ?? DateTime.Now,
            Reason = txtReason.Text ?? "",
            Diagnosis = txtDiagnosis.Text ?? "",
            VetId = _loggedInStaff.Id ?? "", // שומר את ה-ID ב-DB
            BaseCost = _baseVisitPrice,
            TotalCost = _baseVisitPrice + selectedMeds.Sum(m => m.Price)
        };

        try
        {
            await SupabaseService.Client!.From<Visit>().Insert(newVisit);
            ShowStatus("הביקור נשמר!", true);
            await Task.Delay(1000);
            NewVisitOverlay.IsVisible = false;
            LoadInitialData();
        }
        catch (Exception) { ShowStatus("שגיאה בשמירה", false); }
    }

    private void ShowStatus(string message, bool isSuccess)
    {
        lblStatusMessage.Text = message;
        lblStatusMessage.Foreground = isSuccess ? Brushes.Green : Brushes.Red;
        lblStatusMessage.IsVisible = true;
    }

    private void LstMedications_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var selectedMeds = lstMedications.SelectedItems?.Cast<Medication>() ?? Enumerable.Empty<Medication>();
        lblTotalPrice.Text = $"סה''כ: ₪ {_baseVisitPrice + selectedMeds.Sum(m => m.Price)}";
    }
}