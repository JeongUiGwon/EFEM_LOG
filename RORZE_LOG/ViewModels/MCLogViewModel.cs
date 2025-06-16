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

        public MCLogViewModel()
        {
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

                    if (handshakes.ContainsKey(data).Equals(false))
                    {
                        break;
                    }

                        if (data.Contains("ALIGN") && !data.Contains("ALIGN2"))
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

                            elapsedTime = calcuateMCLogElapsedTime(time, message, remainder);

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
    }
}
