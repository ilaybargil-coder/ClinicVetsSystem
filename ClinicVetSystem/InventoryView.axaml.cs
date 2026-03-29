using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ClinicVetSystem
{
    public partial class InventoryView : UserControl
    {
        public InventoryView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}