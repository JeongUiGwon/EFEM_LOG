using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RORZE_LOG.Models
{
    public partial class CheckboxOptionItem : ObservableObject
    {
        public string Name { get; set; }

        [ObservableProperty]
        private bool isChecked = true;
    }
}
