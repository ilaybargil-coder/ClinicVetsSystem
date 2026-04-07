using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using ClinicVetsSystem.Models;

namespace ClinicVetsSystem;

public partial class VisitsView : UserControl
{
    // ── State ──────────────────────────────────────────────────────────────
    private readonly Staff          _loggedInStaff;
    private readonly bool           _isSecretary;
    private const    decimal        BaseCost = 150m;

    private List<VisitDisplayItem>  _todayVisits   = new();
    private List<VisitDisplayItem>  _historyVisits = new();
    private List<Medication>        _allMeds       = new();
    private List<Pet>               _allPets       = new();
    private List<Customer>          _allCustomers  = new();

    private VisitDisplayItem?       _activeRecord;
    private HashSet<int>            _selectedMedIds = new();
    private bool                    _showHistory    = false;

    // ── Constructor ────────────────────────────────────────────────────────
    public VisitsView(Staff staff)
    {
        InitializeComponent();
        _loggedInStaff  = staff;
        _isSecretary    = staff.Role == "מזכיר/ה";
        lblVetBadge.Text = staff.Username;
        lblRoleLabel.Text = _isSecretary ? "מזכיר/ה" : "וטרינר/ית";

        _ = LoadDataAsync();
    }

    // ── DB Load ────────────────────────────────────────────────────────────
    private async Task LoadDataAsync()
    {
        try
        {
            lblSubtitle.Text = "טוען נתונים...";

            var visitsTask = _isSecretary
                ? SupabaseService.Client!.From<Visit>()
                      .Order(v => v.VisitDate, Postgrest.Constants.Ordering.Descending)
                      .Get()
                : SupabaseService.Client!.From<Visit>()
                      .Where(v => v.VetId == _loggedInStaff.Id)
                      .Order(v => v.VisitDate, Postgrest.Constants.Ordering.Descending)
                      .Get();
            var petsTask      = SupabaseService.Client!.From<Pet>().Get();
            var customersTask = SupabaseService.Client!.From<Customer>().Get();
            var medsTask      = SupabaseService.Client!.From<Medication>()
                                    .Order(m => m.Name, Postgrest.Constants.Ordering.Ascending)
                                    .Get();

            await Task.WhenAll(visitsTask, petsTask, customersTask, medsTask);

            var visits    = visitsTask.Result.Models;
            _allPets      = petsTask.Result.Models;
            _allCustomers = customersTask.Result.Models;
            _allMeds      = medsTask.Result.Models;

            // Build display items — join visit + pet + customer
            var displayItems = visits.Select(v =>
            {
                var pet      = _allPets.FirstOrDefault(p => p.Id == v.PetId);
                var customer = _allCustomers.FirstOrDefault(c => c.Id == pet?.OwnerId);
                return new VisitDisplayItem
                {
                    VisitId          = v.Id,
                    PetId            = v.PetId,
                    PetName          = pet?.Name      ?? "חיה לא ידועה",
                    PetType          = pet?.PetType   ?? "",
                    CustomerName     = customer?.FullName ?? "לקוח לא ידוע",
                    CustomerPhone    = customer?.Phone    ?? "",
                    Reason           = v.Reason,
                    Diagnosis        = v.Diagnosis,
                    VisitDate        = v.VisitDate,
                    BaseCost         = v.BaseCost > 0 ? v.BaseCost : BaseCost,
                    TotalCost        = v.TotalCost,
                    LastVaccineDate  = pet?.LastVaccineDate?.ToDateTime(TimeOnly.MinValue)
                };
            }).ToList();

            _todayVisits   = displayItems.Where(v => v.VisitDate.Date == DateTime.Today)
                                         .OrderBy(v => v.VisitDate).ToList();
            _historyVisits = displayItems.Where(v => v.VisitDate.Date != DateTime.Today)
                                         .ToList(); // already ordered desc by DB

            lblTodayCount.Text = _todayVisits.Count.ToString();

            int waiting = _todayVisits.Count(v => !v.IsCompleted);
            lblSubtitle.Text = waiting > 0
                ? $"{waiting} מטופלים ממתינים לטיפול"
                : $"{_todayVisits.Count} מטופלים היום";

            RenderPatientList();
        }
        catch (Exception)
        {
            lblSubtitle.Text = "⚠ שגיאה בטעינת נתונים";
        }
    }

    // ── Patient list ───────────────────────────────────────────────────────
    private void RenderPatientList()
    {
        PatientList.Children.Clear();

        var items = _showHistory ? _historyVisits : _todayVisits;

        if (items.Count == 0)
        {
            PatientList.Children.Add(BuildListEmptyState(_showHistory));
            return;
        }

        foreach (var item in items)
            PatientList.Children.Add(BuildPatientCard(item));
    }

    private static Control BuildListEmptyState(bool isHistory) =>
        new Border
        {
            Padding = new Thickness(24, 40),
            Child = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 10,
                Children =
                {
                    new TextBlock
                    {
                        Text = isHistory ? "📂" : "✅",
                        FontSize = 44,
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = isHistory ? "אין ביקורים קודמים" : "אין מטופלים להיום",
                        FontSize = 14, FontWeight = FontWeight.SemiBold,
                        Foreground = new SolidColorBrush(Color.Parse("#64748b")),
                        HorizontalAlignment = HorizontalAlignment.Center
                    }
                }
            }
        };

    private Border BuildPatientCard(VisitDisplayItem item)
    {
        bool isSelected = _activeRecord?.VisitId == item.VisitId;
        bool isDone     = item.IsCompleted;

        string emoji = item.PetType switch
        {
            "כלב"  => "🐕", "חתול" => "🐈",
            "ציפור"=> "🐦", "זוחל" => "🦎",
            _      => "🐾"
        };

        var (badgeBg, badgeFg, badgeText) = isDone
            ? ("#dcfce7", "#15803d", "✓ טופל")
            : ("#fef9c3", "#a16207", "ממתין");

        var bg = isSelected ? Color.Parse("#f0fdf4") : Colors.Transparent;

        // Top row: pet name + badge
        var topRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*, Auto") };
        topRow.Children.Add(new TextBlock
        {
            Text = $"{emoji}  {item.PetName}",
            FontSize = 14, FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Color.Parse("#1e293b")),
            VerticalAlignment = VerticalAlignment.Center
        });
        var badge = new Border
        {
            Background   = new SolidColorBrush(Color.Parse(badgeBg)),
            CornerRadius = new CornerRadius(10),
            Padding      = new Thickness(8, 3)
        };
        badge.Child = new TextBlock
        {
            Text = badgeText, FontSize = 11, FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse(badgeFg))
        };
        Grid.SetColumn(badge, 1);
        topRow.Children.Add(badge);

        // Bottom row: customer + time + cost (for history)
        var timeStr = item.VisitDate.Date == DateTime.Today
            ? item.VisitDate.ToString("HH:mm")
            : item.VisitDate.ToString("dd/MM/yy");
        var costStr = item.TotalCost > 0 ? $"  ·  ₪{item.TotalCost}" : "";
        var subText = new TextBlock
        {
            Text = $"👤 {item.CustomerName}  ·  🕐 {timeStr}{costStr}",
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#64748b"))
        };

        var content = new StackPanel { Spacing = 5 };
        content.Children.Add(topRow);
        content.Children.Add(subText);

        var card = new Border
        {
            Background   = new SolidColorBrush(bg),
            Padding      = new Thickness(18, 14),
            Cursor       = new Cursor(StandardCursorType.Hand),
            Child        = content
        };

        // Bottom separator line
        var wrapper = new StackPanel();
        wrapper.Children.Add(card);
        wrapper.Children.Add(new Border
        {
            Height = 1, Background = new SolidColorBrush(Color.Parse("#f1f5f9")),
            Margin = new Thickness(16, 0)
        });

        var outerCard = new Border { Child = wrapper };
        outerCard.PointerPressed += (_, _) => SelectVisit(item);
        return outerCard;
    }

    // ── Select a visit → populate medical record ───────────────────────────
    private void SelectVisit(VisitDisplayItem item)
    {
        _activeRecord   = item;
        _selectedMedIds = new HashSet<int>();

        // Show the record panel
        RecordEmptyState.IsVisible = false;
        RecordPanel.IsVisible      = true;

        // Patient header
        string emoji = item.PetType switch
        {
            "כלב"  => "🐕", "חתול" => "🐈",
            "ציפור"=> "🐦", "זוחל" => "🦎",
            _      => "🐾"
        };
        lblPatientEmoji.Text   = emoji;
        PatientAvatarBorder.Background = new SolidColorBrush(item.PetType switch
        {
            "כלב"  => Color.Parse("#f0fdf4"),
            "חתול" => Color.Parse("#eff6ff"),
            "ציפור"=> Color.Parse("#fefce8"),
            "זוחל" => Color.Parse("#fdf4ff"),
            _      => Color.Parse("#f8fafc")
        });
        lblPatientName.Text    = $"{item.PetName}  ({item.PetType})";
        lblPatientOwner.Text   = $"👤 {item.CustomerName}";
        lblVisitDateTime.Text  = $"📅 {item.VisitDate:dd/MM/yyyy HH:mm}";

        // Vaccine warning — show if last vaccine > 1 year ago (or never)
        if (item.LastVaccineDate == null)
        {
            lblVaccineWarning.Text     = "לא נרשם תאריך חיסון לחיה זו";
            VaccineWarningPanel.IsVisible = true;
        }
        else
        {
            var monthsSince = (DateTime.Today - item.LastVaccineDate.Value).TotalDays / 30.4;
            if (monthsSince >= 12)
            {
                lblVaccineWarning.Text     =
                    $"חיסון אחרון: {item.LastVaccineDate.Value:dd/MM/yyyy} — לפני {(int)monthsSince} חודשים";
                VaccineWarningPanel.IsVisible = true;
            }
            else
            {
                VaccineWarningPanel.IsVisible = false;
            }
        }

        // Step 1 – reason (vet fills in; pre-fill if already set)
        var reasonText = item.Reason == "טרם נקבע — ימולא על ידי הוטרינר" ? "" : item.Reason;
        txtReason.Text = reasonText;

        // Step 2 – diagnosis (pre-fill if already diagnosed)
        txtDiagnosis.Text = item.Diagnosis ?? "";

        // Step 3 – load existing medication selections from DB, then render
        lblSaveStatus.IsVisible = false;
        _ = LoadExistingMedsAndRender(item.VisitId);

        // Apply role restrictions for secretary
        if (_isSecretary)
        {
            txtReason.IsReadOnly      = true;
            txtDiagnosis.IsReadOnly   = true;
            btnSaveRecord.IsVisible   = false;
        }

        // Re-render list to highlight selected card
        RenderPatientList();
    }

    private async Task LoadExistingMedsAndRender(int visitId)
    {
        try
        {
            var resp = await SupabaseService.Client!.From<VisitMedication>()
                           .Where(vm => vm.VisitId == visitId)
                           .Get();
            _selectedMedIds = new HashSet<int>(resp.Models.Select(vm => vm.MedicationId));
        }
        catch (Exception)
        {
            _selectedMedIds = new HashSet<int>();
        }

        RenderMedicationsPanel();
        UpdateCostDisplay();
    }

    // ── Step 3: Medications panel ──────────────────────────────────────────
    private void RenderMedicationsPanel()
    {
        MedicationsList.Children.Clear();

        if (_allMeds.Count == 0)
        {
            MedicationsList.Children.Add(new TextBlock
            {
                Text = "אין תרופות בInventory",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.Parse("#94a3b8"))
            });
            return;
        }

        foreach (var med in _allMeds)
            MedicationsList.Children.Add(BuildMedRow(med));
    }

    private Border BuildMedRow(Medication med)
    {
        bool selected = _selectedMedIds.Contains(med.Id);

        var checkBox = new Border
        {
            Width = 22, Height = 22,
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(selected ? Color.Parse("#006c49") : Color.Parse("#e2e8f0")),
            VerticalAlignment = VerticalAlignment.Center,
            Child = selected ? new TextBlock
            {
                Text = "✓", FontSize = 12, FontWeight = FontWeight.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            } : null
        };

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto, *, Auto"), ColumnSpacing = 12 };
        Grid.SetColumn(checkBox, 0);
        row.Children.Add(checkBox);

        var nameBlock = new TextBlock
        {
            Text = med.Name,
            FontSize = 14,
            FontWeight = selected ? FontWeight.SemiBold : FontWeight.Normal,
            Foreground = new SolidColorBrush(selected ? Color.Parse("#006c49") : Color.Parse("#1e293b")),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(nameBlock, 1);
        row.Children.Add(nameBlock);

        var priceBlock = new TextBlock
        {
            Text = $"₪{med.Price}",
            FontSize = 13, FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(selected ? Color.Parse("#006c49") : Color.Parse("#64748b")),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(priceBlock, 2);
        row.Children.Add(priceBlock);

        var card = new Border
        {
            Background   = new SolidColorBrush(selected ? Color.Parse("#f0fdf4") : Color.Parse("#f8fafc")),
            CornerRadius = new CornerRadius(14),
            Padding      = new Thickness(16, 12),
            Cursor       = new Cursor(StandardCursorType.Hand),
            Child        = row
        };

        if (!_isSecretary)
        {
            var medId = med.Id;
            card.PointerPressed += (_, _) =>
            {
                if (_selectedMedIds.Contains(medId))
                    _selectedMedIds.Remove(medId);
                else
                    _selectedMedIds.Add(medId);

                RenderMedicationsPanel();
                UpdateCostDisplay();
            };
        }

        return card;
    }

    // ── Step 4: Cost display ───────────────────────────────────────────────
    private void UpdateCostDisplay()
    {
        if (_activeRecord == null) return;

        decimal medsTotal = _allMeds
            .Where(m => _selectedMedIds.Contains(m.Id))
            .Sum(m => m.Price);

        decimal base_ = _activeRecord.BaseCost > 0 ? _activeRecord.BaseCost : BaseCost;

        lblBaseCost.Text  = $"₪{base_}";
        lblMedsCost.Text  = $"₪{medsTotal}";
        lblTotalCost.Text = $"₪{base_ + medsTotal}";
    }

    // ── Save medical record ────────────────────────────────────────────────
    private async void BtnSaveRecord_Click(object? sender, RoutedEventArgs e)
    {
        if (_activeRecord == null) return;

        var reason    = txtReason.Text?.Trim()    ?? "";
        var diagnosis = txtDiagnosis.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(reason))
        {
            ShowSaveStatus("⚠ יש להזין סיבת ביקור לפני השמירה", false);
            return;
        }
        if (string.IsNullOrEmpty(diagnosis))
        {
            ShowSaveStatus("⚠ יש להזין אבחנה רפואית לפני השמירה", false);
            return;
        }

        decimal medsTotal = _allMeds
            .Where(m => _selectedMedIds.Contains(m.Id))
            .Sum(m => m.Price);
        decimal base_     = _activeRecord.BaseCost > 0 ? _activeRecord.BaseCost : BaseCost;
        decimal total     = base_ + medsTotal;

        try
        {
            btnSaveRecord.IsEnabled = false;
            ShowSaveStatus("שומר רשומה...", null);

            var client = SupabaseService.Client!;

            // 1. Update the visit row
            await client.From<Visit>()
                .Where(v => v.Id == _activeRecord.VisitId)
                .Set(v => v.Reason,     reason)
                .Set(v => v.Diagnosis!, diagnosis)
                .Set(v => v.VetId!,     _loggedInStaff.Id ?? "")
                .Set(v => v.TotalCost,  total)
                .Update();

            // 2. Replace visit medications
            await client.From<VisitMedication>()
                .Where(vm => vm.VisitId == _activeRecord.VisitId)
                .Delete();

            foreach (var medId in _selectedMedIds)
                await client.From<VisitMedication>().Insert(new VisitMedication
                {
                    VisitId      = _activeRecord.VisitId,
                    MedicationId = medId
                });

            ShowSaveStatus("✓ הרשומה נשמרה בהצלחה", true);
            await LoadDataAsync();

            // Re-select same patient so the panel stays open with updated data
            var updated = _todayVisits.Concat(_historyVisits)
                              .FirstOrDefault(v => v.VisitId == _activeRecord.VisitId);
            if (updated != null) SelectVisit(updated);
        }
        catch (Exception)
        {
            ShowSaveStatus("⚠ שגיאה בשמירה, נסה שוב", false);
        }
        finally
        {
            btnSaveRecord.IsEnabled = true;
        }
    }

    private void ShowSaveStatus(string msg, bool? isSuccess)
    {
        lblSaveStatus.Text = msg;
        lblSaveStatus.Foreground = isSuccess switch
        {
            true  => new SolidColorBrush(Color.Parse("#15803d")),
            false => new SolidColorBrush(Color.Parse("#dc2626")),
            _     => new SolidColorBrush(Color.Parse("#64748b"))
        };
        lblSaveStatus.IsVisible = true;
    }

    // ── Tab switching ──────────────────────────────────────────────────────
    private void BtnTabToday_Click(object? sender, RoutedEventArgs e)
    {
        if (_showHistory == false) return;
        _showHistory = false;
        btnTabToday.Classes.Set("active", true);
        btnTabHistory.Classes.Set("active", false);
        _activeRecord = null;
        RecordEmptyState.IsVisible = true;
        RecordPanel.IsVisible      = false;
        RenderPatientList();
    }

    private void BtnTabHistory_Click(object? sender, RoutedEventArgs e)
    {
        if (_showHistory) return;
        _showHistory = true;
        btnTabHistory.Classes.Set("active", true);
        btnTabToday.Classes.Set("active", false);
        _activeRecord = null;
        RecordEmptyState.IsVisible = true;
        RecordPanel.IsVisible      = false;
        RenderPatientList();
    }
}

// ── Display model: joins Visit + Pet + Customer ────────────────────────────
public class VisitDisplayItem
{
    public int       VisitId         { get; set; }
    public int       PetId           { get; set; }
    public string    PetName         { get; set; } = "";
    public string    PetType         { get; set; } = "";
    public string    CustomerName    { get; set; } = "";
    public string    CustomerPhone   { get; set; } = "";
    public string    Reason          { get; set; } = "";
    public string    Diagnosis       { get; set; } = "";
    public DateTime  VisitDate       { get; set; }
    public decimal   BaseCost        { get; set; }
    public decimal   TotalCost       { get; set; }
    public DateTime? LastVaccineDate { get; set; }

    public bool IsCompleted => !string.IsNullOrWhiteSpace(Diagnosis);
}
