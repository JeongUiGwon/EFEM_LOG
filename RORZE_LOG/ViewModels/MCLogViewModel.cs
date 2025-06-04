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

namespace RORZE_LOG.ViewModels
{
    public partial class MCLogViewModel : ObservableObject
    {
        public ObservableCollection<MCLogModel> MCLogRepository { get; } = new();
        public ICollectionView FilteredMCLogRepository { get; }

        public List<string> TypeOptions { get; } = new() { "All", "SED", "REC" };
        public ObservableCollection<CommandOptionItem> CommandOptionItems { get; } = new()
        {
            new CommandOptionItem { Name = "MOV" },
            new CommandOptionItem { Name = "SET" },
            new CommandOptionItem { Name = "GET" }
        };

        [ObservableProperty]
        private string typeFilter = "All";

        [ObservableProperty]
        private string searchText = string.Empty;

        public MCLogViewModel()
        {
            FilteredMCLogRepository = CollectionViewSource.GetDefaultView(MCLogRepository);
            FilteredMCLogRepository.Filter = ApplyFilter;

            foreach (var item in CommandOptionItems)
            {
                item.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(CommandOptionItem.IsChecked))
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

                var selectedCommands = CommandOptionItems
                                   .Where(x => x.IsChecked)
                                   .Select(x => x.Name)
                                   .ToList();

                if (selectedCommands.Count == 0) return false;

                bool commandMatches = selectedCommands.Contains(log.CommandGroup);

                bool searchMatches = string.IsNullOrEmpty(SearchText) || log.Data.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0;

                return typeMatches && commandMatches && searchMatches;
            }
            return false;
        }

        private DateTime convertToDateTime(string time)
        {
            if (string.IsNullOrEmpty(time)) return DateTime.MinValue;
            try
            {
                // Assuming the format is "MM-dd-yy HH:mm:ss:fff"
                return DateTime.ParseExact(time, "yy-MM-dd HH:mm:ss:fff", null);
            }
            catch
            {
                return DateTime.MinValue; // Return a default value if parsing fails
            }
        }

        Dictionary<string, HandShakeModel> handshakes = new Dictionary<string, HandShakeModel>();

        private double calcuateMCLogElapsedTime(string time, string command, string data)
        {
            double elapsedTime = 0.0;

            switch (command)
            {
                case "MOV":
                    if (handshakes.ContainsKey(data).Equals(true))
                    {
                        handshakes.Remove(data);
                    }

                    handshakes.Add(data, new HandShakeModel() { Name = data, Type = command, StartTime = convertToDateTime(time) });
                    return elapsedTime;
                case "SET":
                    if (handshakes.ContainsKey("ALIGN").Equals(true))
                    {
                        handshakes.Remove("ALIGN");
                    }

                    handshakes.Add("ALIGN", new HandShakeModel() { Name = "ALIGN", Type = command, StartTime = convertToDateTime(time) });
                    return elapsedTime;
                case "INF":
                    if (handshakes.ContainsKey(data).Equals(true))
                    {
                        handshakes[data].InfTime = convertToDateTime(time);
                        elapsedTime = (handshakes[data].InfTime - handshakes[data].StartTime).TotalMilliseconds;
                        elapsedTime /= 1000.0; // Convert to seconds
                        return elapsedTime;
                    }
                    break;
                case "ACK":
                    if(data.Contains("STATE"))
                    {
                        return elapsedTime; // Ignore STATE ACKs for elapsed time calculation
                    }

                    if(data.Contains("ALIGN") && !data.Contains("ALIGN2"))
                    {
                        if (handshakes["ALIGN"].InfTime != DateTime.MinValue)
                        {
                            handshakes["ALIGN"].StartAckTime = convertToDateTime(time);
                            elapsedTime = (handshakes["ALIGN"].StartAckTime - handshakes["ALIGN"].StartTime).TotalMilliseconds;
                        }
                        else
                        {
                            handshakes["ALIGN"].InfAckTime = convertToDateTime(time);
                            elapsedTime = (handshakes["ALIGN"].InfAckTime - handshakes["ALIGN"].StartTime).TotalMilliseconds;
                        }
                        elapsedTime /= 1000.0; // Convert to seconds
                        return elapsedTime;
                    }

                    if (handshakes.ContainsKey(data).Equals(true))
                    {
                        if (handshakes[data].InfTime != DateTime.MinValue)
                        {
                            handshakes[data].StartAckTime = convertToDateTime(time);
                            elapsedTime = (handshakes[data].StartAckTime - handshakes[data].StartTime).TotalMilliseconds;
                        }
                        else
                        {
                            handshakes[data].InfAckTime = convertToDateTime(time);
                            elapsedTime = (handshakes[data].InfAckTime - handshakes[data].StartTime).TotalMilliseconds;
                        }
                        elapsedTime /= 1000.0; // Convert to seconds
                        return elapsedTime;
                    }
                    break;
                default:
                    break;
            }
            return elapsedTime;
        }

        private string GetCommandGroup(string command, string data)
        {
            string group = string.Empty;

            switch (command)
            {
                case "MOV":
                case "SET":
                case "GET":
                    group = command;
                    break;
                case "INF":
                case "ACK":
                    if(data.Contains("STATE"))
                    {
                        group = "GET";
                    }
                    else if (data.Contains("ALIGN/") || data.Contains("ALIGN;"))
                    {
                        group = "SET";
                    }
                    else
                    {
                        group = "MOV";
                    }
                    break;
                default:
                    group = "Unknown";
                    break;
            }

            return group;
        }

        [RelayCommand]
        private void OpenLogFiles()
        {
            var dialog = new OpenFileDialog()
            {
                Multiselect = true,
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                MCLogRepository.Clear();
                var timePattern = new Regex(@"^\d{2}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}:\d{3}");

                foreach (var file in dialog.FileNames)
                {
                    foreach (var line in File.ReadAllLines(file))
                    {
                        string time = "";
                        string type = "";
                        string command = "";
                        string commandGroup = "";
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
                        if (parts.Length >= 1) type = parts[0].Trim();
                        if (parts.Length >= 2) command = parts[1].Trim();
                        if (parts.Length == 3) remainder = parts[2].Trim();

                        elapsedTime = calcuateMCLogElapsedTime(time, command, remainder);
                        commandGroup = GetCommandGroup(command, remainder);

                        MCLogRepository.Add(new MCLogModel
                        {
                            Time = time,
                            Type = type,
                            Command = command,
                            CommandGroup = commandGroup,
                            ElapsedTime = elapsedTime,
                            Data = remainder
                        });
                    }
                }
            }
        }
    }
}
