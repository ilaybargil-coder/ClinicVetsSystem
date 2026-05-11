using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ClinicVetsSystem.Models;

namespace ClinicVetsSystem;

public partial class PetsView : UserControl
{
    private List<PetRow> _allPets = new();
    private readonly ObservableCollection<PetRow> _displayedPets = new();
    private List<Customer> _allCustomers = new();
    private List<PetType> _allPetTypes = new();
    private bool _isEditMode = false;
    private int _editingPetId = 0;
    private int _pendingDeleteId = 0;
    private string _currentFilterCustomerId = "";

    public PetsView()
    {
        InitializeComponent();
        lstPets.ItemsSource = _displayedPets;
    }

    // ─── Data loading ─────────────────────────────────────────────────────────

    public async Task LoadDataAsync()
    {
        try
        {
            lblStatus.Text = "Syncing...";
            var petsTask      = SupabaseService.Client!.From<Pet>().Order(p => p.Name, Postgrest.Constants.Ordering.Ascending).Get();
            var customersTask = SupabaseService.Client!.From<Customer>().Order(c => c.FullName, Postgrest.Constants.Ordering.Ascending).Get();
            var typesTask     = SupabaseService.Client!.From<PetType>().Order(t => t.Name, Postgrest.Constants.Ordering.Ascending).Get();
            await Task.WhenAll(petsTask, customersTask, typesTask);

            _allCustomers = customersTask.Result.Models;
            _allPetTypes  = typesTask.Result.Models;
            _allPets = petsTask.Result.Models.Select(p =>
            {
                var owner = _allCustomers.FirstOrDefault(c => c.Id == p.OwnerId);
                return new PetRow(p, owner?.FullName ?? p.OwnerId);
            }).ToList();

            RefreshTypeFilter();
            RefreshDisplay();
            lblStatus.Text = "Up to date ✓";
        }
        catch (Exception ex) { lblStatus.Text = $"Error: {ex.Message}"; }
    }

    private void RefreshTypeFilter()
    {
        var selected = cbTypeFilter.SelectedItem as string;
        var items = new List<string> { "All" };
        items.AddRange(_allPetTypes.Select(p => p.Name));
        cbTypeFilter.ItemsSource = items;
        cbTypeFilter.SelectedItem = (selected != null && items.Contains(selected)) ? selected : "All";
    }

    private void RefreshDisplay()
    {
        var query      = txtSearch.Text?.Trim().ToLower() ?? "";
        var typeFilter = cbTypeFilter.SelectedItem as string;

        var filtered = _allPets.AsEnumerable();

        if (!string.IsNullOrEmpty(_currentFilterCustomerId))
            filtered = filtered.Where(p => p.OwnerId == _currentFilterCustomerId);

        if (!string.IsNullOrEmpty(query))
            filtered = filtered.Where(p =>
                p.Name.ToLower().Contains(query) ||
                (p.ChipNumber?.ToLower().Contains(query) ?? false));

        if (!string.IsNullOrEmpty(typeFilter) && typeFilter != "All")
            filtered = filtered.Where(p => p.PetType == typeFilter);

        var filteredList = filtered.ToList();
        _displayedPets.Clear();
        foreach (var row in filteredList) _displayedPets.Add(row);

        if (!string.IsNullOrEmpty(_currentFilterCustomerId))
        {
            var ownerName = _allCustomers.FirstOrDefault(c => c.Id == _currentFilterCustomerId)?.FullName ?? _currentFilterCustomerId;
            lblPetCount.Text = $"Showing {filteredList.Count} pets (Filtered by: {ownerName})";
        }
        else
        {
            lblPetCount.Text = $"Showing {filteredList.Count} pets";
        }
    }

    public void FilterByCustomer(string customerId)
    {
        _currentFilterCustomerId = customerId;
        txtSearch.Text = "";
        RefreshDisplay();
    }

    public void ClearFilter()
    {
        _currentFilterCustomerId = "";
        RefreshDisplay();
    }

    private void txtSearch_TextChanged(object? sender, TextChangedEventArgs e) => RefreshDisplay();
    private void cbTypeFilter_SelectionChanged(object? sender, SelectionChangedEventArgs e) => RefreshDisplay();

    private void lstPets_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        btnEdit.IsEnabled = btnDelete.IsEnabled = lstPets.SelectedItem != null;
    }

    // ─── Add / Edit dialog ────────────────────────────────────────────────────

    private void OpenDialog(Pet? p = null)
    {
        _isEditMode    = p != null;
        _editingPetId  = p?.Id ?? 0;
        lblDialogTitle.Text = _isEditMode ? "Edit Pet" : "Add New Pet";

        cbDialogPetType.ItemsSource = _allPetTypes.Select(t => t.Name).ToList();
        cbDialogOwner.ItemsSource   = _allCustomers.Select(c => new OwnerItem(c.Id, c.FullName)).ToList();

        if (_isEditMode && p != null)
        {
            txtDialogName.Text        = p.Name;
            cbDialogPetType.SelectedItem = p.PetType;
            txtDialogWeight.Text      = p.Weight.ToString("F2");
            dpDialogBirthDate.SelectedDate = new DateTime(p.BirthDate.Year, p.BirthDate.Month, p.BirthDate.Day);
            dpDialogVaccineDate.SelectedDate = p.LastVaccineDate.HasValue
                ? new DateTime(p.LastVaccineDate.Value.Year, p.LastVaccineDate.Value.Month, p.LastVaccineDate.Value.Day)
                : null;
            txtDialogChip.Text = p.ChipNumber ?? "";

            var ownerItems = cbDialogOwner.ItemsSource as List<OwnerItem>;
            cbDialogOwner.SelectedItem = ownerItems?.FirstOrDefault(o => o.Id == p.OwnerId);
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

            if (!string.IsNullOrEmpty(_currentFilterCustomerId))
            {
                var ownerItems = cbDialogOwner.ItemsSource as List<OwnerItem>;
                cbDialogOwner.SelectedItem = ownerItems?.FirstOrDefault(o => o.Id == _currentFilterCustomerId);
            }
        }

        lblDialogError.IsVisible = false;
        ModalOverlay.IsVisible = true;
    }

    private void btnCancelDialog_Click(object? sender, RoutedEventArgs e) => ModalOverlay.IsVisible = false;

    private async void btnSaveDialog_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            // 1. Name — letters only
            var name = txtDialogName.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(name) || !Regex.IsMatch(name, @"^[\u0590-\u05FFa-zA-Z\s]+$"))
            { ShowError("שם החיה חייב להכיל אותיות בלבד."); return; }

            // 2. Pet type
            if (cbDialogPetType.SelectedItem is not string petType)
            { ShowError("יש לבחור סוג חיה."); return; }

            // 3. Weight 0.1 – 100
            if (!decimal.TryParse(txtDialogWeight.Text?.Replace(',', '.'),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var weight)
                || weight < 0.1m || weight > 100m)
            { ShowError("המשקל חייב להיות מספר עשרוני בין 0.1 ל-100 ק\"ג."); return; }

            // 4. Birth date — not future, not before 2000
            if (dpDialogBirthDate.SelectedDate is not DateTime birthDt)
            { ShowError("יש לבחור תאריך לידה."); return; }
            var birthDate = DateOnly.FromDateTime(birthDt);
            if (birthDate > DateOnly.FromDateTime(DateTime.Today))
            { ShowError("תאריך לידה לא יכול להיות תאריך עתידי."); return; }
            if (birthDate.Year < 2000)
            { ShowError("תאריך לידה לא יכול להיות לפני שנת 2000."); return; }

            // 5. Owner — must select existing customer
            if (cbDialogOwner.SelectedItem is not OwnerItem owner)
            { ShowError("יש לשייך את החיה ללקוח קיים."); return; }

            // 6. Vaccine date — optional, but cannot be future
            DateOnly? vaccineDate = null;
            if (dpDialogVaccineDate.SelectedDate is DateTime vaccineDt)
            {
                var vd = DateOnly.FromDateTime(vaccineDt);
                if (vd > DateOnly.FromDateTime(DateTime.Today))
                { ShowError("תאריך חיסון לא יכול להיות תאריך עתידי."); return; }
                vaccineDate = vd;
            }

            // 7. Chip — optional, but exactly 15 digits if provided
            string? chip = null;
            if (!string.IsNullOrWhiteSpace(txtDialogChip.Text))
            {
                chip = txtDialogChip.Text.Trim();
                if (!Regex.IsMatch(chip, @"^\d{15}$"))
                { ShowError("מספר שבב חייב להכיל בדיוק 15 ספרות."); return; }
            }

            if (_isEditMode)
            {
                await SupabaseService.Client!.From<Pet>()
                    .Where(x => x.Id == _editingPetId)
                    .Set(x => x.Name!,           name)
                    .Set(x => x.PetType!,        petType)
                    .Set(x => x.Weight,          weight)
                    .Set(x => x.BirthDate,       birthDate)
                    .Set(x => x.OwnerId!,        owner.Id)
                    .Set(x => x.LastVaccineDate, vaccineDate)
                    .Set(x => x.ChipNumber,      chip)
                    .Update();
            }
            else
            {
                var pet = new Pet
                {
                    Name = name, PetType = petType, Weight = weight,
                    BirthDate = birthDate, OwnerId = owner.Id,
                    LastVaccineDate = vaccineDate, ChipNumber = chip
                };
                await SupabaseService.Client!.From<Pet>().Insert(pet);
            }

            ModalOverlay.IsVisible = false;
            await LoadDataAsync();
        }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    private void ShowError(string msg) { lblDialogError.Text = "⚠ " + msg; lblDialogError.IsVisible = true; }

    // ─── Delete with confirmation ─────────────────────────────────────────────

    private void btnDelete_Click(object? sender, RoutedEventArgs e)
    {
        if (lstPets.SelectedItem is not PetRow r) return;
        _pendingDeleteId = r.Id;
        lblConfirmPetName.Text = r.Name;
        ConfirmOverlay.IsVisible = true;
    }

    private void btnConfirmCancel_Click(object? sender, RoutedEventArgs e) => ConfirmOverlay.IsVisible = false;

    private async void btnConfirmDelete_Click(object? sender, RoutedEventArgs e)
    {
        ConfirmOverlay.IsVisible = false;
        await SupabaseService.Client!.From<Pet>().Where(x => x.Id == _pendingDeleteId).Delete();
        await LoadDataAsync();
    }

    // ─── Toolbar ─────────────────────────────────────────────────────────────

    private void btnAddPet_Click(object? sender, RoutedEventArgs e) => OpenDialog();

    private void btnEdit_Click(object? sender, RoutedEventArgs e)
    {
        if (lstPets.SelectedItem is not PetRow r) return;
        OpenDialog(new Pet
        {
            Id = r.Id, Name = r.Name, PetType = r.PetType, Weight = r.Weight,
            BirthDate = r.BirthDate, OwnerId = r.OwnerId,
            LastVaccineDate = r.LastVaccineDate, ChipNumber = r.ChipNumber
        });
    }

    // ─── Pet Types management ─────────────────────────────────────────────────

    private void btnManageTypes_Click(object? sender, RoutedEventArgs e)
    {
        lstPetTypes.ItemsSource = _allPetTypes;
        txtNewPetType.Text = "";
        lblPetTypesError.IsVisible = false;
        PetTypesOverlay.IsVisible = true;
    }

    private void btnClosePetTypes_Click(object? sender, RoutedEventArgs e) => PetTypesOverlay.IsVisible = false;

    private async void btnAddPetType_Click(object? sender, RoutedEventArgs e)
    {
        var typeName = txtNewPetType.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(typeName)) return;
        try
        {
            await SupabaseService.Client!.From<PetType>().Insert(new PetType { Name = typeName });
            txtNewPetType.Text = "";
            await LoadDataAsync();
            lstPetTypes.ItemsSource = _allPetTypes;
            lblPetTypesError.IsVisible = false;
        }
        catch (Exception ex) { lblPetTypesError.Text = ex.Message; lblPetTypesError.IsVisible = true; }
    }

    private async void btnDeletePetType_Click(object? sender, RoutedEventArgs e)
    {
        if (lstPetTypes.SelectedItem is not PetType pt) return;
        try
        {
            await SupabaseService.Client!.From<PetType>().Where(x => x.Id == pt.Id).Delete();
            await LoadDataAsync();
            lstPetTypes.ItemsSource = _allPetTypes;
            lblPetTypesError.IsVisible = false;
        }
        catch (Exception ex) { lblPetTypesError.Text = ex.Message; lblPetTypesError.IsVisible = true; }
    }
}

// ─── OwnerItem: displays name, carries ID ─────────────────────────────────────

public class OwnerItem
{
    public string Id   { get; }
    public string Name { get; }
    public OwnerItem(string id, string name) { Id = id; Name = name; }
    public override string ToString() => Name;
}

// ─── PetRow: display model ────────────────────────────────────────────────────

public class PetRow
{
    public int      Id             { get; set; }
    public string   OwnerId        { get; set; }
    public string   Name           { get; set; }
    public string   PetType        { get; set; }
    public decimal  Weight         { get; set; }
    public DateOnly BirthDate      { get; set; }
    public DateOnly? LastVaccineDate { get; set; }
    public string?  ChipNumber     { get; set; }
    public string   OwnerName      { get; set; }

    public string WeightFormatted  => $"{Weight:F1} kg";
    public string ChipDisplay      => ChipNumber ?? "—";

    public string AgeDisplay
    {
        get
        {
            var today  = DateOnly.FromDateTime(DateTime.Today);
            var years  = today.Year  - BirthDate.Year;
            var months = today.Month - BirthDate.Month;
            if (today.Day < BirthDate.Day) months--;
            if (months < 0) { years--; months += 12; }
            if (years  > 0) return $"{years} yr{(years  == 1 ? "" : "s")}";
            if (months > 0) return $"{months} mo";
            return "< 1 mo";
        }
    }

    public bool IsVaccineOverdue =>
        LastVaccineDate.HasValue &&
        LastVaccineDate.Value < DateOnly.FromDateTime(DateTime.Today.AddYears(-1));

    public IBrush VaccineForeground => IsVaccineOverdue
        ? new SolidColorBrush(Color.FromRgb(220, 38, 38))
        : new SolidColorBrush(Color.FromRgb(100, 116, 139));

    public string VaccineDisplay
    {
        get
        {
            if (!LastVaccineDate.HasValue) return "—";
            var s = LastVaccineDate.Value.ToString("MMM dd, yyyy");
            return IsVaccineOverdue ? "⚠️ " + s : s;
        }
    }

    public string PetTypeDisplay => PetType switch
    {
        "כלב"   => "כלב",
        "חתול"  => "חתול",
        "זוחל"  => "זוחל",
        "ציפור" => "ציפור",
        _       => PetType ?? ""
    };

    public Bitmap PetTypeImage => new Bitmap(AssetLoader.Open(new Uri(PetType switch
    {
        "כלב"   => "avares://ClinicVetSystem/Assets/dog.png",
        "חתול"  => "avares://ClinicVetSystem/Assets/cat.png",
        "ציפור" => "avares://ClinicVetSystem/Assets/bird.png",
        "זוחל"  => "avares://ClinicVetSystem/Assets/reptile.png",
        _       => "avares://ClinicVetSystem/Assets/dog.png"
    })));

    public PetRow(Pet p, string ownerName)
    {
        Id = p.Id; OwnerId = p.OwnerId; Name = p.Name; PetType = p.PetType;
        Weight = p.Weight; BirthDate = p.BirthDate; LastVaccineDate = p.LastVaccineDate;
        ChipNumber = p.ChipNumber; OwnerName = ownerName;
    }
}
