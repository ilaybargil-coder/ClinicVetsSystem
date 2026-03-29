using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ClinicVetsSystem.Models;
using System.Globalization;

namespace ClinicVetsSystem
{
    public partial class InventoryView : UserControl
    {
        public InventoryView()
        {
            InitializeComponent();
            LoadInventory();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private async void LoadInventory()
        {
            if (SupabaseService.Client == null) return;
            try
            {
                var result = await SupabaseService.Client.From<Medication>().Get();
                this.FindControl<DataGrid>("dgInventory")!.ItemsSource = result.Models;
            }
            catch { }
        }

        private async void AddMedication_Click(object sender, RoutedEventArgs e)
        {
            if (SupabaseService.Client == null) return;
            var name = this.FindControl<TextBox>("newName")!.Text?.Trim();
            var priceText = this.FindControl<TextBox>("newPrice")!.Text?.Trim();

            if (string.IsNullOrEmpty(name) || !decimal.TryParse(priceText, NumberStyles.Number, CultureInfo.InvariantCulture, out var price))
                return;

            try
            {
                var med = new Medication { Name = name, Price = price };
                await SupabaseService.Client.From<Medication>().Insert(med);
                LoadInventory();
            }
            catch { }
        }

        private async void DeleteMedication_Click(object sender, RoutedEventArgs e)
        {
            if (SupabaseService.Client == null) return;
            var grid = this.FindControl<DataGrid>("dgInventory");
            if (grid?.SelectedItem is Medication selected)
            {
                try
                {
                    await SupabaseService.Client.From<Medication>().Where(m => m.Id == selected.Id).Delete();
                    LoadInventory();
                }
                catch { }
            }
        }
    }
}