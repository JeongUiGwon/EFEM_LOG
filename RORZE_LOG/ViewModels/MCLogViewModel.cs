using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using RORZE_LOG.Models;
using RORZE_LOG.Services;

namespace RORZE_LOG.ViewModels
{
    public partial class MCLogViewModel : ObservableObject
    {
        private readonly Services.TypeConverter _typeConverter;
        public ObservableCollection<MCLogModel> MCLogRepository { get; } = new();
        public ICollectionView FilteredMCLogRepository { get; }
        public List<string> TypeOptions { get; } = new() { "All", "SED", "REC" };
        private readonly string[] messageList = new string[]
        {
            "MOV", "GET", "SET", "INF", "ABS", "EVT", "ACK", "NAK", "CAN", "UNKNOWN"
        };
        private readonly string[] commandList = new string[]
        {
            "INIT", "ORGSH", "LOCK", "UNLOCK", "DOCK", "UNDOCK", "OPEN", "CLOSE",
            "MAPDT", "GOTO", "LOAD", "UNLOAD", "ALIGN", "ERROR", "CLAMP", "STATE",
            "RFIDR", "PIOUT", "LAMPO", "EMODE", "SIGLM", "N2RUN", "N2STS", "UNKNOWN"
        };

        public ObservableCollection<CheckboxOptionItem> MessageOptionItems { get; }
        public ObservableCollection<CheckboxOptionItem> CommandOptionItems { get; }

        [ObservableProperty]
        private string typeFilter = "All";

        [ObservableProperty]
        private string searchText = string.Empty;

        [ObservableProperty]
        private string fileName = string.Empty;

        [ObservableProperty]
        private bool isLoading = false;

        [ObservableProperty]
        private int filteredCount = 0;

        Dictionary<string, HandShakeModel> handshakes = new Dictionary<string, HandShakeModel>();
        HandShakeModel alignHandshake = new HandShakeModel();

        public MCLogViewModel()
        {
            _typeConverter = new Services.TypeConverter();
            MessageOptionItems = new ObservableCollection<CheckboxOptionItem>();
            foreach (string message in messageList)
            {
                MessageOptionItems.Add(new CheckboxOptionItem { Name = message });
            }

            CommandOptionItems = new ObservableCollection<CheckboxOptionItem>();
            foreach (string command in commandList)
            {
                CommandOptionItems.Add(new CheckboxOptionItem { Name = command });
            }

            FilteredMCLogRepository = CollectionViewSource.GetDefaultView(MCLogRepository);
            FilteredMCLogRepository.Filter = ApplyFilter;
            FilteredMCLogRepository.CollectionChanged += (s, e) => FilteredCount = FilteredMCLogRepository.Cast<object>().Count();

            foreach (CheckboxOptionItem item in MessageOptionItems)
            {
                item.PropertyChanged += (_, e) =>
                {
                    FilteredMCLogRepository.Refresh();
                };
            }

            foreach (var item in CommandOptionItems)
            {
                item.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(CheckboxOptionItem.IsChecked))
                    {
                        FilteredMCLogRepository.Refresh();
                    }
                };
            }
        }
        partial void OnTypeFilterChanged(string value) => FilteredMCLogRepository.Refresh();
        partial void OnSearchTextChanged(string value) => FilteredMCLogRepository.Refresh();

        private bool ApplyFilter(object obj)
        {
            if (obj is MCLogModel log)
            {
                bool typeMatches = TypeFilter == "All" || log.Type.Equals(TypeFilter, StringComparison.OrdinalIgnoreCase);

                var selectedMessages = MessageOptionItems
                                   .Where(x => x.IsChecked)
                                   .Select(x => x.Name)
                                   .ToList();

                bool MessageMatches = true;
                if (selectedMessages.Count != 0)
                {
                    MessageMatches = selectedMessages.Contains(log.Message);
                }

                var selectedCommands = CommandOptionItems
                                       .Where(x => x.IsChecked)
                                       .Select(x => x.Name)
                                       .ToList();

                bool CommandMatches = true;
                if (selectedCommands.Count != 0)
                {
                    CommandMatches = selectedCommands.Contains(log.Command);
                }

                bool searchMatches = string.IsNullOrEmpty(SearchText) || log.Data.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0;

                return typeMatches && MessageMatches && searchMatches && CommandMatches;
            }
            return false;
        }

        private double calLoadUnloadElapsedTime(string time, string message, string command, string data)
        {
            double elapsedTime = 0.0;

            if ( command != "LOAD" && command != "UNLOAD" ) return elapsedTime;

            if ( message == "MOV" )
            {
                if (handshakes.ContainsKey(data).Equals(true))
                {
                    handshakes.Remove(data);
                }

                handshakes.Add(data, new HandShakeModel() { Name = data, Type = command, StartTime = _typeConverter.stringToDateTime(time) });
            }
            else if ( message == "INF")
            {
                if (handshakes.ContainsKey(data).Equals(true))
                {
                    handshakes[data].InfTime = _typeConverter.stringToDateTime(time);
                    elapsedTime = (handshakes[data].InfTime - handshakes[data].StartTime).TotalMilliseconds;
                    elapsedTime /= 1000.0; // Convert to seconds
                }
            }
            else if ( message == "ACK" )
            {
                if (handshakes.ContainsKey(data).Equals(false)) return elapsedTime;

                if (handshakes[data].InfTime != DateTime.MinValue)
                {
                    handshakes[data].StartAckTime = _typeConverter.stringToDateTime(time);
                    elapsedTime = (handshakes[data].StartAckTime - handshakes[data].StartTime).TotalMilliseconds;
                }
                else
                {
                    handshakes[data].InfAckTime = _typeConverter.stringToDateTime(time);
                    elapsedTime = (handshakes[data].InfAckTime - handshakes[data].StartTime).TotalMilliseconds;
                }

                elapsedTime /= 1000.0; // Convert to seconds
            }

            return elapsedTime;
        }

        private double calAlignElapsedTime(string time, string message, string command, string data)
        {
            double elapsedTime = 0.0;

            if ( command != "ALIGN" ) return elapsedTime;

            if ( message == "MOV" )
            {
                alignHandshake.Clear();

                alignHandshake.Name = "MOV/ALIGN";
                alignHandshake.StartTime = _typeConverter.stringToDateTime(time);
            }
            else if ( message == "SET" )
            {
                alignHandshake.Clear();

                alignHandshake.Name = "SET/ALIGN";
                alignHandshake.StartTime = _typeConverter.stringToDateTime(time);
            }
            else if (message == "INF")
            {
                if (alignHandshake.StartTime == DateTime.MinValue) return elapsedTime;

                alignHandshake.InfTime = _typeConverter.stringToDateTime(time);
                elapsedTime = (alignHandshake.InfTime - alignHandshake.StartTime).TotalMilliseconds;
                elapsedTime /= 1000.0; // Convert to seconds
            }
            else if (message == "ACK")
            {
                if (alignHandshake.StartTime == DateTime.MinValue) return elapsedTime;

                if (alignHandshake.InfTime != DateTime.MinValue)
                {
                    alignHandshake.StartAckTime = _typeConverter.stringToDateTime(time);
                    elapsedTime = (alignHandshake.StartAckTime - alignHandshake.StartTime).TotalMilliseconds;
                }
                else
                {
                    alignHandshake.InfAckTime = _typeConverter.stringToDateTime(time);
                    elapsedTime = (alignHandshake.InfAckTime - alignHandshake.StartTime).TotalMilliseconds;
                }

                elapsedTime /= 1000.0; // Convert to seconds
            }

            return elapsedTime;
        }

        string GetMessage(string message)
        {
            string ret_message = "UNKNOWN";
            message = message.Trim();

            if (messageList.Contains(message))
            {
                ret_message = message;
            }

            return ret_message;
        }

        string GetCommand(string command)
        {
            string ret_command = "UNKNOWN";
            command = command.Trim().Replace(";", "");

            if (commandList.Contains(command))
            {
                ret_command = command;
            }

            return ret_command;
        }

        [RelayCommand]
        private void OpenLogFiles()
        {
            IsLoading = true;

            try
            {
                var dialog = new OpenFileDialog()
                {
                    Multiselect = true,
                    Filter = "All files (*.*)|*.*"
                };


                if (dialog.ShowDialog() == true)
                {
                    FileName = string.Empty;
                    var temp_MCLogRepository = new List<MCLogModel>();
                    var timePattern = new Regex(@"^\d{2}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}:\d{3}");

                    var names = dialog.FileNames.Select(path => Path.GetFileName(path));
                    FileName = string.Join(" ", names);

                    foreach (var file in dialog.FileNames)
                    {
                        foreach (var line in File.ReadAllLines(file))
                        {
                            string time = "";
                            string type = "";
                            string message = "";
                            string command = "";
                            string data = line;
                            string remainder = data;
                            double elapsedTime = 0.0;

                            var match = timePattern.Match(line);
                            if (match.Success)
                            {
                                time = match.Value;
                                data = line.Substring(match.Length).TrimStart(':', ' ');
                            }

                            var parts = data.Split(':', 3);
                            if (parts.Length > 0) type = parts[0].Trim();
                            if (parts.Length > 1) message = GetMessage(parts[1]);
                            if (parts.Length > 2) remainder = parts[2].Trim();

                            parts = remainder.Split('/');
                            if (parts.Length > 0) command = GetCommand(parts[0]);

                            if (command == "LOAD" || command == "UNLOAD")
                            {
                                elapsedTime = calLoadUnloadElapsedTime(time, message, command, remainder);
                            }
                            else if (command == "ALIGN")
                            {
                                elapsedTime = calAlignElapsedTime(time, message, command, remainder);
                            }

                            temp_MCLogRepository.Add(new MCLogModel
                            {
                                Time = time,
                                Type = type,
                                Message = message,
                                Command = command,
                                ElapsedTime = elapsedTime,
                                Data = remainder
                            });
                        }
                    }

                    MCLogRepository.Clear();
                    foreach (var item in temp_MCLogRepository)
                    {
                        MCLogRepository.Add(item);
                    }
                }
            }
            finally
            {
                IsLoading = false;
            }            
        }

        [RelayCommand]
        private void ExportToCSV()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                DefaultExt = "csv",
                FileName = $"MCLog_Export_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    using (var writer = new StreamWriter(dialog.FileName, false, Encoding.UTF8))
                    {
                        // Write header
                        writer.WriteLine("Time,Type,Message,Command,ElapsedTime,Data");

                        // Write data
                        foreach (MCLogModel log in FilteredMCLogRepository)
                        {
                            var line = $"{log.Time},{log.Type},{log.Message},{log.Command},{log.ElapsedTime:F3},\"{log.Data.Replace("\"", "\"\"")}\"";
                            writer.WriteLine(line);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // You might want to show an error message to the user here
                    System.Windows.MessageBox.Show($"Error exporting to CSV: {ex.Message}", "Export Error", 
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }
    }
}
