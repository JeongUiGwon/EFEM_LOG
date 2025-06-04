using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace RORZE_LOG.Models
{
    public partial class HandShakeModel : ObservableObject
    {
        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        private string type = string.Empty;

        [ObservableProperty]
        private DateTime startTime = DateTime.MinValue;

        [ObservableProperty]
        private DateTime startAckTime = DateTime.MinValue;

        [ObservableProperty]
        private DateTime infTime = DateTime.MinValue;

        [ObservableProperty]
        private DateTime infAckTime = DateTime.MinValue;

        [ObservableProperty]
        private bool isComplete = false;

        public void Clear()
        {
            Name = string.Empty;
            Type = string.Empty;
            StartTime = DateTime.MinValue;
            StartAckTime = DateTime.MinValue;
            InfTime = DateTime.MinValue;
            InfAckTime = DateTime.MinValue;
            IsComplete = false;
        }
    }
}
