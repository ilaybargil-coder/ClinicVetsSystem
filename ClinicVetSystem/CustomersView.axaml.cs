using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using ClinicVetsSystem.Models;

namespace ClinicVetsSystem;

public partial class CustomersView : UserControl
{
    // ── State ──────────────────────────────────────────────────────────────
    private List<CustomerRow>                   _allCustomers     = new();
    private readonly ObservableCollection<CustomerRow> _displayedCustomers = new();
    private CustomerRow?                        _selectedCustomer;
    private bool                                _isEditMode       = false;

    // Avatar colours
    private static readonly string[] AvatarBg  = { "#f0fdf4", "#eff6ff", "#fdf4ff", "#fffbeb", "#fef2f2" };
    private static readonly string[] AvatarFg  = { "#006c49", "#1d4ed8", "#7c3aed", "#a16207", "#dc2626" };

    // Pet type palette
    private static readonly (string Bg, string Fg, string Emoji)[] PetPalette =
    {
        ("#dcfce7", "#15803d", "🐕"),  // כלב
        ("#dbeafe", "#1d4ed8", "🐈"),  // חתול
        ("#fef9c3", "#a16207", "🐦"),  // ציפור
        ("#ede9fe", "#6d28d9", "🦎"),  // זוחל
        ("#f1f5f9", "#475569", "🐾"),  // other
    };

    // ── Constructor ────────────────────────────────────────────────────────
    public CustomersView()
    {
        InitializeComponent();
        lstCustomers.ItemsSource = _displayedCustomers;
        cbAddPetType.ItemsSource = new[] { "כלב", "חתול", "ציפור", "זוחל" };
    }

    // ── Load customers from DB ─────────────────────────────────────────────
    public async Task LoadDataAsync()
    {
        try
        {
            lblStatus.Text = "מסנכרן...";
            var result = await SupabaseService.Client!
                .From<Customer>()
                .Order(c => c.FullName, Postgrest.Constants.Ordering.Ascending)
                .Get();

            _allCustomers = result.Models.Select(c => new CustomerRow(c)).ToList();
            RefreshDisplay();
            lblStatus.Text = "מעודכן ✓";
        }
        catch (Exception)
        {
            lblStatus.Text = "⚠ שגיאה בטעינת נתונים";
        }
    }

    private void RefreshDisplay()
    {
        var q = txtSearch.Text?.Trim().ToLower() ?? "";
        var filtered = string.IsNullOrEmpty(q)
            ? _allCustomers
            : _allCustomers
                .Where(c => c.Id.Contains(q)
                         || c.Phone.Contains(q)
                         || c.FullName.ToLower().Contains(q))
                .ToList();

        _displayedCustomers.Clear();
        foreach (var row in filtered) _displayedCustomers.Add(row);
        lblCustomerCount.Text = $"{filtered.Count} לקוחות רשומים";
    }

    // ── Search ─────────────────────────────────────────────────────────────
    private void txtSearch_TextChanged(object? sender, TextChangedEventArgs e)
        => RefreshDisplay();

    // ── List selection → load profile ─────────────────────────────────────
    private void lstCustomers_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (lstCustomers.SelectedItem is CustomerRow row)
        {
            _selectedCustomer = row;
            _ = LoadCustomerProfileAsync(row);
        }
    }

    // ── Profile panel ──────────────────────────────────────────────────────
    private async Task LoadCustomerProfileAsync(CustomerRow row)
    {
        EmptyStatePanel.IsVisible = false;
        ProfilePanel.IsVisible    = true;

        int colorIdx = Math.Abs(row.FullName.GetHashCode()) % AvatarBg.Length;
        AvatarBorder.Background    = new SolidColorBrush(Color.Parse(AvatarBg[colorIdx]));
        lblInitials.Foreground     = new SolidColorBrush(Color.Parse(AvatarFg[colorIdx]));
        lblInitials.Text           = BuildInitials(row.FullName);

        lblProfileName.Text  = row.FullName;
        lblProfileId.Text    = row.Id;
        lblProfilePhone.Text = row.Phone;
        lblProfileEmail.Text = string.IsNullOrEmpty(row.Email) ? "—" : row.Email;
        lblProfileSince.Text = $"לקוח מאז {row.CreatedAtFormatted}";

        await LoadCustomerPetsAsync(row.Id);
    }

    private async Task LoadCustomerPetsAsync(string customerId)
    {
        PetsList.Children.Clear();
        lblPetCount.Text = "טוען...";

        try
        {
            var resp = await SupabaseService.Client!
                .From<Pet>()
                .Where(p => p.OwnerId == customerId)
                .Get();

            var pets = resp.Models;
            lblPetCount.Text = pets.Count > 0
                ? $"{pets.Count} חיות רשומות"
                : "אין חיות רשומות עדיין";

            if (pets.Count == 0)
            {
                PetsList.Children.Add(BuildPetEmptyState());
                return;
            }

            foreach (var pet in pets)
                PetsList.Children.Add(BuildPetCard(pet));
        }
        catch (Exception)
        {
            lblPetCount.Text = "שגיאה בטעינת חיות";
        }
    }

    // ── Pet card builder ───────────────────────────────────────────────────
    private static Control BuildPetEmptyState() =>
        new Border
        {
            Background   = new SolidColorBrush(Color.Parse("#f8fafc")),
            CornerRadius = new CornerRadius(16),
            Padding      = new Thickness(0, 20),
            Child = new TextBlock
            {
                Text = "לחץ על '+ הוסף חיה' כדי לרשום את חיית המחמד הראשונה",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.Parse("#94a3b8")),
                HorizontalAlignment = HorizontalAlignment.Center
            }
        };

    private static Border BuildPetCard(Pet pet)
    {
        var (bg, fg, emoji) = pet.PetType switch
        {
            "כלב"  => PetPalette[0],
            "חתול" => PetPalette[1],
            "ציפור"=> PetPalette[2],
            "זוחל" => PetPalette[3],
            _      => PetPalette[4]
        };

        var bgColor = Color.Parse(bg);
        var fgColor = Color.Parse(fg);
        var muteFg  = Color.FromArgb(160, fgColor.R, fgColor.G, fgColor.B);

        bool vaccineOk = !pet.NeedsVaccine;
        var vaccineBg  = vaccineOk ? Color.Parse("#dcfce7") : Color.Parse("#fef9c3");
        var vaccineFg  = vaccineOk ? Color.Parse("#15803d") : Color.Parse("#a16207");
        var vaccineText = vaccineOk ? "✓ חיסון תקין" : "⚠ חיסון פג";

        var emojiCircle = new Border
        {
            Width = 48, Height = 48,
            CornerRadius = new CornerRadius(14),
            Background   = new SolidColorBrush(Color.FromArgb(60, fgColor.R, fgColor.G, fgColor.B)),
            Child = new TextBlock
            {
                Text = emoji, FontSize = 22,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center
            }
        };

        var nameRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        nameRow.Children.Add(new TextBlock
        {
            Text = pet.Name, FontSize = 15, FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(fgColor),
            VerticalAlignment = VerticalAlignment.Center
        });
        nameRow.Children.Add(new Border
        {
            Background   = new SolidColorBrush(vaccineBg),
            CornerRadius = new CornerRadius(8),
            Padding      = new Thickness(8, 2),
            Child = new TextBlock
            {
                Text = vaccineText, FontSize = 11, FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(vaccineFg)
            }
        });

        var subtitleParts = new List<string>();
        if (!string.IsNullOrEmpty(pet.PetType)) subtitleParts.Add(pet.PetType);
        if (pet.Weight > 0) subtitleParts.Add($"{pet.Weight} ק\"ג");
        if (pet.LastVaccineDate.HasValue)
            subtitleParts.Add($"חיסון: {pet.LastVaccineDate.Value:dd/MM/yyyy}");

        var details = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        details.Children.Add(nameRow);
        if (subtitleParts.Count > 0)
            details.Children.Add(new TextBlock
            {
                Text = string.Join(" · ", subtitleParts),
                FontSize = 12,
                Foreground = new SolidColorBrush(muteFg)
            });

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto, *"), ColumnSpacing = 14 };
        Grid.SetColumn(emojiCircle, 0);
        Grid.SetColumn(details, 1);
        row.Children.Add(emojiCircle);
        row.Children.Add(details);

        return new Border
        {
            Background   = new SolidColorBrush(bgColor),
            CornerRadius = new CornerRadius(18),
            Padding      = new Thickness(16, 12),
            Child        = row
        };
    }

    // ── Profile action buttons ─────────────────────────────────────────────
    private void BtnEditProfile_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedCustomer == null) return;
        OpenDialog(new Customer
        {
            Id       = _selectedCustomer.Id,
            FullName = _selectedCustomer.FullName,
            Phone    = _selectedCustomer.Phone,
            Email    = _selectedCustomer.Email
        });
    }

    private async void BtnDeleteProfile_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedCustomer == null) return;
        try
        {
            await SupabaseService.Client!
                .From<Customer>()
                .Where(c => c.Id == _selectedCustomer.Id)
                .Delete();

            _selectedCustomer = null;
            ProfilePanel.IsVisible    = false;
            EmptyStatePanel.IsVisible = true;
            lstCustomers.SelectedItem = null;

            await LoadDataAsync();
        }
        catch (Exception)
        {
            lblStatus.Text = "⚠ לא ניתן למחוק לקוח שיש לו חיות רשומות";
        }
    }

    // ── Add Pet overlay ────────────────────────────────────────────────────
    private void BtnAddPet_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedCustomer == null) return;
        txtAddPetName.Text       = string.Empty;
        txtAddPetWeight.Text     = string.Empty;
        txtAddPetChip.Text       = string.Empty;
        cbAddPetType.SelectedIndex  = -1;
        dpAddPetBirth.SelectedDate  = null;
        dpAddPetVaccine.SelectedDate = null;
        lblAddPetError.IsVisible    = false;
        lblAddPetSubtitle.Text      = $"ללקוח: {_selectedCustomer.FullName}";
        AddPetOverlay.IsVisible     = true;
    }

    private void CloseAddPet_Click(object? sender, RoutedEventArgs e)
        => AddPetOverlay.IsVisible = false;

    private async void BtnSaveAddPet_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedCustomer == null) return;

        var name    = txtAddPetName.Text?.Trim() ?? "";
        var typeStr = cbAddPetType.SelectedItem?.ToString() ?? "";

        // שם — אותיות בלבד (כמו במסך ניהול חיות)
        if (string.IsNullOrEmpty(name) || !Regex.IsMatch(name, @"^[\u0590-\u05FFa-zA-Z\s]+$"))
        {
            ShowAddPetError("שם החיה חייב להכיל אותיות בלבד.");
            return;
        }
        if (string.IsNullOrEmpty(typeStr))
        {
            ShowAddPetError("יש לבחור סוג חיה.");
            return;
        }

        // משקל — מספר עשרוני בין 0.1 ל-100
        if (!decimal.TryParse(txtAddPetWeight.Text?.Replace(',', '.'),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var weight)
            || weight < 0.1m || weight > 100m)
        {
            ShowAddPetError("המשקל חייב להיות מספר עשרוני בין 0.1 ל-100 ק\"ג.");
            return;
        }

        // תאריך לידה — חובה, לא עתידי, לא לפני 2000
        if (!dpAddPetBirth.SelectedDate.HasValue)
        {
            ShowAddPetError("יש לבחור תאריך לידה.");
            return;
        }
        var birthDate = DateOnly.FromDateTime(dpAddPetBirth.SelectedDate.Value.DateTime);
        if (birthDate > DateOnly.FromDateTime(DateTime.Today))
        {
            ShowAddPetError("תאריך לידה לא יכול להיות תאריך עתידי.");
            return;
        }
        if (birthDate.Year < 2000)
        {
            ShowAddPetError("תאריך לידה לא יכול להיות לפני שנת 2000.");
            return;
        }

        // תאריך חיסון — אופציונלי, אך לא עתידי
        DateOnly? vaccineDate = null;
        if (dpAddPetVaccine.SelectedDate.HasValue)
        {
            var vd = DateOnly.FromDateTime(dpAddPetVaccine.SelectedDate.Value.DateTime);
            if (vd > DateOnly.FromDateTime(DateTime.Today))
            {
                ShowAddPetError("תאריך חיסון לא יכול להיות תאריך עתידי.");
                return;
            }
            vaccineDate = vd;
        }

        // שבב — אופציונלי, אך בדיוק 15 ספרות אם הוזן
        string? chip = null;
        if (!string.IsNullOrWhiteSpace(txtAddPetChip.Text))
        {
            chip = txtAddPetChip.Text.Trim();
            if (!Regex.IsMatch(chip, @"^\d{15}$"))
            {
                ShowAddPetError("מספר שבב חייב להכיל בדיוק 15 ספרות.");
                return;
            }
        }

        var pet = new Pet
        {
            OwnerId       = _selectedCustomer.Id,
            Name          = name,
            PetType       = typeStr,
            Weight        = weight,
            BirthDate     = birthDate,
            LastVaccineDate = vaccineDate,
            ChipNumber    = chip
        };

        try
        {
            btnSaveAddPet.IsEnabled = false;
            await SupabaseService.Client!.From<Pet>().Insert(pet);
            AddPetOverlay.IsVisible = false;
            await LoadCustomerPetsAsync(_selectedCustomer.Id);
        }
        catch (Exception)
        {
            ShowAddPetError("שגיאה בשמירה, נסה שוב.");
        }
        finally
        {
            btnSaveAddPet.IsEnabled = true;
        }
    }

    private void ShowAddPetError(string msg)
    {
        lblAddPetError.Text      = "⚠ " + msg;
        lblAddPetError.IsVisible = true;
    }

    // ── Add / Edit customer dialog ─────────────────────────────────────────
    private void OpenDialog(Customer? c = null)
    {
        _isEditMode = c != null;

        lblDialogTitle.Text    = _isEditMode ? "עריכת פרטי לקוח" : "רישום לקוח חדש";
        lblSaveBtn.Text        = _isEditMode ? "שמור שינויים"     : "שמור לקוח";
        txtDialogId.Text       = c?.Id       ?? "";
        txtDialogId.IsReadOnly = _isEditMode;
        txtDialogId.Opacity    = _isEditMode ? 0.5 : 1.0;
        txtDialogName.Text     = c?.FullName ?? "";
        txtDialogPhone.Text    = c?.Phone    ?? "";
        txtDialogEmail.Text    = c?.Email    ?? "";
        lblDialogError.IsVisible = false;
        ModalOverlay.IsVisible   = true;
    }

    private void btnCancelDialog_Click(object? sender, RoutedEventArgs e)
        => ModalOverlay.IsVisible = false;

    private async void btnSaveDialog_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var name  = txtDialogName.Text?.Trim()  ?? "";
            var id    = txtDialogId.Text?.Trim()    ?? "";
            var phone = txtDialogPhone.Text?.Trim() ?? "";
            var email = txtDialogEmail.Text?.Trim() ?? "";

            if (string.IsNullOrEmpty(name) ||
                !Regex.IsMatch(name, @"^[\u0590-\u05FFa-zA-Z\s]+$"))
            {
                ShowDialogError("שם הלקוח חייב להכיל אותיות בלבד.");
                return;
            }
            if (string.IsNullOrEmpty(id) || !Regex.IsMatch(id, @"^\d{9}$"))
            {
                ShowDialogError("תעודת הזהות חייבת להכיל בדיוק 9 ספרות.");
                return;
            }
            if (string.IsNullOrEmpty(phone) || !Regex.IsMatch(phone, @"^0\d{8,9}$"))
            {
                ShowDialogError("מספר הטלפון לא חוקי (חייב להתחיל ב-0 ולהכיל 9-10 ספרות).");
                return;
            }
            if (!string.IsNullOrEmpty(email) &&
                !Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.(com|co\.il|org|net|ac\.il|gov\.il|edu)$",
                               RegexOptions.IgnoreCase))
            {
                ShowDialogError("כתובת האימייל אינה תקינה.");
                return;
            }

            var customer = new Customer
            {
                Id       = id,
                FullName = name,
                Phone    = phone,
                Email    = string.IsNullOrEmpty(email) ? null : email
            };

            var client = SupabaseService.Client;
            if (client == null) return;

            if (_isEditMode)
            {
                await client.From<Customer>()
                    .Where(x => x.Id == customer.Id)
                    .Set(x => x.FullName!, customer.FullName)
                    .Set(x => x.Phone!,    customer.Phone)
                    .Set(x => x.Email!,    customer.Email ?? "")
                    .Update();
            }
            else
            {
                await client.From<Customer>().Insert(customer);
            }

            ModalOverlay.IsVisible = false;
            await LoadDataAsync();

            if (_isEditMode && _selectedCustomer?.Id == id)
            {
                var updated = _allCustomers.FirstOrDefault(c => c.Id == id);
                if (updated != null)
                {
                    _selectedCustomer = updated;
                    _ = LoadCustomerProfileAsync(updated);
                }
            }
        }
        catch (Exception ex)
        {
            ShowDialogError(ex.Message.Contains("duplicate") || ex.Message.Contains("23505")
                ? "שגיאה: תעודת הזהות או האימייל כבר קיימים במערכת."
                : "שגיאה בשמירה, ודא שכל השדות הוזנו כראוי.");
        }
    }

    private void ShowDialogError(string msg)
    {
        lblDialogError.Text      = "⚠ " + msg;
        lblDialogError.IsVisible = true;
    }

    private void btnAddCustomer_Click(object? sender, RoutedEventArgs e)
        => OpenDialog();

    // ── Appointment Management ─────────────────────────────────────────────
    private List<Pet> _customerPetsCache = new();

    private async void btnManageAppointments_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedCustomer == null) return;

        lblApptStatus.IsVisible        = false;
        lblApptCustomerName.Text       = $"לקוח: {_selectedCustomer.FullName}";
        AppointmentManagerModal.IsVisible = true;
        await RefreshAppointmentLists();
    }

    private async Task RefreshAppointmentLists()
    {
        if (_selectedCustomer == null) return;
        var client = SupabaseService.Client;
        if (client == null) return;

        try
        {
            var petsResponse = await client.From<Pet>()
                .Where(p => p.OwnerId == _selectedCustomer.Id)
                .Get();
            _customerPetsCache = petsResponse.Models;
            var petIds = _customerPetsCache.Select(p => p.Id).ToHashSet();

            if (petIds.Count == 0)
            {
                lstUpcoming.ItemsSource = null;
                lstHistory.ItemsSource  = null;
                return;
            }

            var visitsResponse = await client.From<Visit>().Get();
            var allVisits = visitsResponse.Models
                .Where(v => petIds.Contains(v.PetId))
                .ToList();

            var now = DateTime.Now;

            lstUpcoming.ItemsSource = allVisits
                .Where(v => v.VisitDate >= now)
                .OrderBy(v => v.VisitDate)
                .Select(v =>
                {
                    var pet = _customerPetsCache.FirstOrDefault(p => p.Id == v.PetId);
                    return new AppointmentDisplayRow
                    {
                        Id = v.Id,
                        DisplayDate = v.VisitDate.ToString("dd/MM/yyyy HH:mm"),
                        PetName = pet?.Name ?? "",
                        Reason = v.Reason
                    };
                })
                .ToList();

            lstHistory.ItemsSource = allVisits
                .Where(v => v.VisitDate < now)
                .OrderByDescending(v => v.VisitDate)
                .Select(v =>
                {
                    var pet = _customerPetsCache.FirstOrDefault(p => p.Id == v.PetId);
                    return new AppointmentDisplayRow
                    {
                        Id = v.Id,
                        DisplayDate = v.VisitDate.ToString("dd/MM/yyyy HH:mm"),
                        PetName = pet?.Name ?? "",
                        Reason = v.Reason
                    };
                })
                .ToList();
        }
        catch (Exception) { }
    }

    private async void btnCancelAppointment_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int visitId)
        {
            btn.IsEnabled = false;
            try
            {
                await SupabaseService.Client!.From<Visit>().Where(v => v.Id == visitId).Delete();
                lblApptStatus.Text       = "✓ התור בוטל בהצלחה";
                lblApptStatus.Foreground = new SolidColorBrush(Color.Parse("#15803d"));
                lblApptStatus.IsVisible  = true;
                await RefreshAppointmentLists();
            }
            catch (Exception)
            {
                lblApptStatus.Text       = "⚠ שגיאה בביטול התור";
                lblApptStatus.Foreground = new SolidColorBrush(Color.Parse("#dc2626"));
                lblApptStatus.IsVisible  = true;
            }
            finally { btn.IsEnabled = true; }
        }
    }

    private void btnGoToNewAppointment_Click(object? sender, RoutedEventArgs e)
    {
        AppointmentManagerModal.IsVisible = false;
        var mainWindow = this.VisualRoot as MainMenuWindow;
        if (mainWindow != null && _selectedCustomer != null)
            mainWindow.GoToCalendarForCustomer(_selectedCustomer.Id);
    }

    private void btnCloseAppointments_Click(object? sender, RoutedEventArgs e)
        => AppointmentManagerModal.IsVisible = false;

    // ── Helpers ────────────────────────────────────────────────────────────
    private static string BuildInitials(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[1][0]}"
            : fullName.Length > 0 ? fullName[0].ToString() : "?";
    }
}

// ── CustomerRow display model ──────────────────────────────────────────────
public class CustomerRow
{
    public string  Id                 { get; set; }
    public string  FullName           { get; set; }
    public string  Phone              { get; set; }
    public string? Email              { get; set; }
    public string  CreatedAtFormatted { get; set; }
    public string  FirstLetter        => FullName.Length > 0 ? FullName[0].ToString() : "?";

    public CustomerRow(Customer c)
    {
        Id                 = c.Id;
        FullName           = c.FullName;
        Phone              = c.Phone;
        Email              = c.Email;
        CreatedAtFormatted = c.CreatedAt?.ToString("MMM dd, yyyy") ?? "—";
    }
}

// ── Appointment display model ─────────────────────────────────────────────
public class AppointmentDisplayRow
{
    public int    Id          { get; set; }
    public string DisplayDate { get; set; } = "";
    public string PetName     { get; set; } = "";
    public string Reason      { get; set; } = "";
}