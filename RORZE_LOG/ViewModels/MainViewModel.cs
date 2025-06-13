using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RORZE_LOG.Views;

namespace RORZE_LOG.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private string title = "SFEM Log Analysis ver0.1.0";

        [RelayCommand]
        private void OpenMcLogView()
        {
            var window = new MCLogView();
            window.Show();
        }
    }
}
