using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ClinicVetsSystem.Models;

namespace ClinicVetsSystem;

public partial class PetsView : UserControl
{
    private List<PetRow> _allPets = new();
    private readonly ObservableCollection<PetRow> _displayedPets = new();
    private List<Customer> _allCustomers = new();
    private bool _isEditMode = false;
    private int _editingPetId = 0;

    private static readonly string[] PetTypes = { "כלב", "חתול", "זוחל", "ציפור" };

    public PetsView()
    {
        InitializeComponent();
        lstPets.ItemsSource = _displayedPets;
        cbDialogPetType.ItemsSource = PetTypes;
    }

    public async Task LoadDataAsync()
    {
        try
        {
            lblStatus.Text = "Syncing...";
            var petsTask = SupabaseService.Client!
                .From<Pet>()
                .Order(p => p.Name, Postgrest.Constants.Ordering.Ascending)
                .Get();
            var customersTask = SupabaseService.Client!
                .From<Customer>()
                .Order(c => c.FullName, Postgrest.Constants.Ordering.Ascending)
                .Get();
            await Task.WhenAll(petsTask, customersTask);

            _allCustomers = customersTask.Result.Models;
            _allPets = petsTask.Result.Models.Select(p =>
            {
                var owner = _allCustomers.FirstOrDefault(c => c.Id == p.OwnerId);
                return new PetRow(p, owner?.FullName ?? p.OwnerId);
            }).ToList();

            // Refresh owner dropdown in case dialog is open
            cbDialogOwner.ItemsSource = _allCustomers.Select(c => c.FullName).ToList();

            RefreshDisplay();
            lblStatus.Text = "Up to date ✓";
        }
        catch (Exception ex) { lblStatus.Text = $"Error: {ex.Message}"; }
    }

    private void RefreshDisplay()
    {
        var query = txtSearch.Text?.Trim().ToLower() ?? "";
        var filtered = string.IsNullOrEmpty(query)
            ? _allPets
            : _allPets.Where(p =>
                p.Name.ToLower().Contains(query) ||
                (p.ChipNumber?.ToLower().Contains(query) ?? false)).ToList();
        _displayedPets.Clear();
        foreach (var row in filtered) _displayedPets.Add(row);
        lblPetCount.Text = $"Showing {filtered.Count} pets";
    }

    private void txtSearch_TextChanged(object? sender, TextChangedEventArgs e) => RefreshDisplay();

    private void lstPets_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        btnEdit.IsEnabled = btnDelete.IsEnabled = lstPets.SelectedItem != null;
    }

    // ─── Dialog ──────────────────────────────────────────────────────────────

    private void OpenDialog(Pet? p = null)
    {
        _isEditMode = p != null;
        _editingPetId = p?.Id ?? 0;
        lblDialogTitle.Text = _isEditMode ? "Edit Pet" : "Add New Pet";
        cbDialogOwner.ItemsSource = _allCustomers.Select(c => c.FullName).ToList();

        if (_isEditMode && p != null)
        {
            txtDialogName.Text = p.Name;
            cbDialogPetType.SelectedItem = p.PetType;
            txtDialogWeight.Text = p.Weight.ToString("F2");
            dpDialogBirthDate.SelectedDate = new DateTime(p.BirthDate.Year, p.BirthDate.Month, p.BirthDate.Day);
            dpDialogVaccineDate.SelectedDate = p.LastVaccineDate.HasValue
                ? new DateTime(p.LastVaccineDate.Value.Year, p.LastVaccineDate.Value.Month, p.LastVaccineDate.Value.Day)
                : null;
            txtDialogChip.Text = p.ChipNumber ?? "";
            var ownerIdx = _allCustomers.FindIndex(c => c.Id == p.OwnerId);
            cbDialogOwner.SelectedIndex = ownerIdx >= 0 ? ownerIdx : -1;
        }
        else
        {
            txtDialogName.Text = "";
            cbDialogPetType.SelectedIndex = -1;
            txtDialogWeight.Text = "";
            dpDialogBirthDate.SelectedDate = null;
            dpDialogVaccineDate.SelectedDate = null;
            txtDialogChip.Text = "";
            cbDialogOwner.SelectedIndex = -1;
        }

        lblDialogError.IsVisible = false;
        ModalOverlay.IsVisible = true;
    }

    private void btnCancelDialog_Click(object? sender, RoutedEventArgs e) => ModalOverlay.IsVisible = false;

    private async void btnSaveDialog_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            // Validate name – Hebrew + English letters and spaces only
            var name = txtDialogName.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(name) || !Regex.IsMatch(name, @"^[\u0590-\u05FFa-zA-Z\s]+$"))
            {
                ShowError("שם החיה חייב להכיל אותיות בלבד.");
                return;
            }

            // Validate pet type
            if (cbDialogPetType.SelectedItem is not string petType)
            {
                ShowError("יש לבחור סוג חיה.");
                return;
            }

            // Validate weight
            if (!decimal.TryParse(txtDialogWeight.Text?.Replace(',', '.'),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var weight)
                || weight < 0.1m || weight > 100m)
            {
                ShowError("המשקל חייב להיות מספר עשרוני בין 0.1 ל-100 ק\"ג.");
                return;
            }

            // Validate birth date
            if (dpDialogBirthDate.SelectedDate is not DateTime birthDateTime)
            {
                ShowError("יש לבחור תאריך לידה.");
                return;
            }
            var birthDate = DateOnly.FromDateTime(birthDateTime);
            if (birthDate > DateOnly.FromDateTime(DateTime.Today))
            {
                ShowError("תאריך לידה לא יכול להיות תאריך עתידי.");
                return;
            }
            if (birthDate.Year < 2000)
            {
                ShowError("תאריך לידה לא יכול להיות לפני שנת 2000.");
                return;
            }

            // Validate owner
            if (cbDialogOwner.SelectedIndex < 0)
            {
                ShowError("יש לשייך את החיה ללקוח קיים.");
                return;
            }
            var owner = _allCustomers[cbDialogOwner.SelectedIndex];

            // Vaccine date (optional)
            DateOnly? vaccineDate = null;
            if (dpDialogVaccineDate.SelectedDate is DateTime vaccineDateTime)
                vaccineDate = DateOnly.FromDateTime(vaccineDateTime);

            var pet = new Pet
            {
                Name = name,
                PetType = petType,
                Weight = weight,
                BirthDate = birthDate,
                OwnerId = owner.Id,
                LastVaccineDate = vaccineDate,
                ChipNumber = string.IsNullOrWhiteSpace(txtDialogChip.Text) ? null : txtDialogChip.Text.Trim()
            };

            if (_isEditMode)
            {
                pet.Id = _editingPetId;
                await SupabaseService.Client!.From<Pet>().Upsert(pet);
            }
            else
            {
                await SupabaseService.Client!.From<Pet>().Insert(pet);
            }

            ModalOverlay.IsVisible = false;
            await LoadDataAsync();
        }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    private void ShowError(string msg)
    {
        lblDialogError.Text = msg;
        lblDialogError.IsVisible = true;
    }

    // ─── Toolbar buttons ─────────────────────────────────────────────────────

    private void btnAddPet_Click(object? sender, RoutedEventArgs e) => OpenDialog();

    private void btnEdit_Click(object? sender, RoutedEventArgs e)
    {
        if (lstPets.SelectedItem is not PetRow r) return;
        OpenDialog(new Pet
        {
            Id = r.Id,
            Name = r.Name,
            PetType = r.PetType,
            Weight = r.Weight,
            BirthDate = r.BirthDate,
            OwnerId = r.OwnerId,
            LastVaccineDate = r.LastVaccineDate,
            ChipNumber = r.ChipNumber
        });
    }

    private async void btnDelete_Click(object? sender, RoutedEventArgs e)
    {
        if (lstPets.SelectedItem is PetRow r)
        {
            await SupabaseService.Client!.From<Pet>().Where(x => x.Id == r.Id).Delete();
            await LoadDataAsync();
        }
    }
}

// ─── Display row ─────────────────────────────────────────────────────────────

public class PetRow
{
    public int Id { get; set; }
    public string OwnerId { get; set; }
    public string Name { get; set; }
    public string PetType { get; set; }
    public decimal Weight { get; set; }
    public DateOnly BirthDate { get; set; }
    public DateOnly? LastVaccineDate { get; set; }
    public string? ChipNumber { get; set; }
    public string OwnerName { get; set; }

    public string BirthDateFormatted    => BirthDate.ToString("MMM dd, yyyy");
    public string VaccineDateFormatted  => LastVaccineDate?.ToString("MMM dd, yyyy") ?? "—";
    public string WeightFormatted       => $"{Weight:F1} kg";
    public string ChipDisplay           => ChipNumber ?? "—";

    public PetRow(Pet p, string ownerName)
    {
        Id = p.Id; OwnerId = p.OwnerId; Name = p.Name; PetType = p.PetType;
        Weight = p.Weight; BirthDate = p.BirthDate; LastVaccineDate = p.LastVaccineDate;
        ChipNumber = p.ChipNumber; OwnerName = ownerName;
    }
}
