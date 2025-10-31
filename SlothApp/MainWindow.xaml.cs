using System;
using System.ComponentModel;                 // CancelEventArgs
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;                       // OpenFileDialog / SaveFileDialog
using SlothApp.Services;                     // UserSettings
using Sloth.Core.Models;                     // SlothConfig, Customer, MoveResult, SourceDoc
using Sloth.Core.Services;                   // ConfigService, ExcelService, BatchMoveService

namespace SlothApp
{
    public partial class MainWindow : Window
    {
        private readonly UserSettings _settings = UserSettings.Load();
        private SlothConfig? _cfg;
        private List<Customer> _customers = new();
        private readonly ObservableCollection<MoveResult> _results = new();

        public MainWindow()
        {
            InitializeComponent();

            // hydrate saved paths
            if (!string.IsNullOrWhiteSpace(_settings.ConfigPath)) TxtConfig.Text = _settings.ConfigPath;
            if (!string.IsNullOrWhiteSpace(_settings.CustomersPath)) TxtExcel.Text = _settings.CustomersPath;
            if (!string.IsNullOrWhiteSpace(_settings.DestRoot)) TxtDestRoot.Text = _settings.DestRoot;
            if (!string.IsNullOrWhiteSpace(_settings.SourceDir)) TxtSrcDir.Text = _settings.SourceDir;
            if (!string.IsNullOrWhiteSpace(_settings.LogPath)) TxtLogPath.Text = _settings.LogPath;

            // default log path if empty
            if (string.IsNullOrWhiteSpace(TxtLogPath.Text))
            {
                var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                TxtLogPath.Text = Path.Combine(desktop, $"SlothLog_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
            }

            // bind grid
            GridResults.ItemsSource = _results;

            // persist on close
            this.Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            _settings.ConfigPath = TxtConfig.Text;
            _settings.CustomersPath = TxtExcel.Text;
            _settings.DestRoot = TxtDestRoot.Text;
            _settings.SourceDir = TxtSrcDir.Text;
            _settings.LogPath = TxtLogPath.Text;
            _settings.Save();
        }

        // -------- Browsers ----------
        private void BrowseConfig_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*" };
            if (dlg.ShowDialog() == true)
            {
                TxtConfig.Text = dlg.FileName;
                _settings.ConfigPath = dlg.FileName;
                _settings.Save();
            }
        }

        private void BrowseExcel_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Excel/CSV (*.xlsx;*.xlsm;*.xls;*.csv)|*.xlsx;*.xlsm;*.xls;*.csv|All files (*.*)|*.*"
            };
            if (dlg.ShowDialog() == true)
            {
                TxtExcel.Text = dlg.FileName;
                _settings.CustomersPath = dlg.FileName;
                _settings.Save();
            }
        }

        private void BrowseDestRoot_Click(object sender, RoutedEventArgs e)
        {
            // fully qualify to avoid namespace ambiguity
            var dlg = new System.Windows.Forms.FolderBrowserDialog();
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                TxtDestRoot.Text = dlg.SelectedPath;
                _settings.DestRoot = dlg.SelectedPath;
                _settings.Save();
            }
        }

        private void BrowseSrcDir_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog();
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                TxtSrcDir.Text = dlg.SelectedPath;
                _settings.SourceDir = dlg.SelectedPath;
                _settings.Save();
            }
        }

        private void BrowseLog_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "Excel Workbook (*.xlsx)|*.xlsx|All files (*.*)|*.*",
                FileName = TxtLogPath.Text
            };
            if (dlg.ShowDialog() == true)
            {
                TxtLogPath.Text = dlg.FileName;
                _settings.LogPath = dlg.FileName;
                _settings.Save();
            }
        }

        // -------- Loaders ----------
        private void LoadConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!File.Exists(TxtConfig.Text))
                    throw new FileNotFoundException("Config not found.", TxtConfig.Text);

                _cfg = ConfigService.Load(TxtConfig.Text);
                LblSets.Text = _cfg.DocumentSets.Count.ToString();

                PopulateDocCodesForSelectedCustomer();
                System.Windows.MessageBox.Show("Config loaded.", "Sloth", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to load config:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadCustomers_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!File.Exists(TxtExcel.Text))
                    throw new FileNotFoundException("Excel not found.", TxtExcel.Text);

                _customers = ExcelService.ReadCustomers(TxtExcel.Text);
                LblCustomers.Text = _customers.Count.ToString();

                CmbCustomer.ItemsSource = _customers;
                if (_customers.Count > 0)
                    CmbCustomer.SelectedIndex = 0;

                PopulateDocCodesForSelectedCustomer();
                System.Windows.MessageBox.Show("Customers loaded.", "Sloth", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to load customers:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PopulateDocCodesForSelectedCustomer()
        {
            CmbDocCode.ItemsSource = null;

            if (_cfg is null) return;
            if (CmbCustomer.SelectedItem is not Customer cust) return;

            if (_cfg.DocumentSets.TryGetValue(cust.Category ?? "", out var set) && set != null)
            {
                var codes = set.Select(d => d.Code).Distinct().ToList();
                CmbDocCode.ItemsSource = codes;
                if (codes.Count > 0) CmbDocCode.SelectedIndex = 0;
            }
        }

        // refresh DocCodes when customer changes
        private void CmbCustomer_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PopulateDocCodesForSelectedCustomer();
        }

        // -------- Actions ----------
        private void Preview_Click(object sender, RoutedEventArgs e) => RunCore(execute: false);
        private void Run_Click(object sender, RoutedEventArgs e) => RunCore(execute: true);

        private void RunCore(bool execute)
        {
            try
            {
                if (_cfg is null) throw new InvalidOperationException("Load Config first.");
                if (_customers.Count == 0) throw new InvalidOperationException("Load Customers first.");
                if (CmbCustomer.SelectedItem is not Customer cust)
                    throw new InvalidOperationException("Select a customer.");

                var docCode = (string?)CmbDocCode.SelectedItem;
                if (string.IsNullOrWhiteSpace(docCode))
                    throw new InvalidOperationException("Select a Doc Code.");

                var destRoot = TxtDestRoot.Text;
                var srcDir = TxtSrcDir.Text;

                if (!Directory.Exists(destRoot))
                    throw new DirectoryNotFoundException($"Dest root not found:\n{destRoot}");
                if (!Directory.Exists(srcDir))
                    throw new DirectoryNotFoundException($"Source folder not found:\n{srcDir}");

                var files = Directory.EnumerateFiles(srcDir).ToList();
                if (files.Count == 0)
                    throw new InvalidOperationException("No files in Source folder.");

                var sources = files.Select(f => new SourceDoc
                {
                    SourcePath = f,
                    CustomerId = cust.CustomerId,
                    DocCode = docCode!
                }).ToList();

                _results.Clear();
                var results = BatchMoveService.PreviewAndExecute(sources, _customers, _cfg, destRoot, execute);
                foreach (var r in results) _results.Add(r);

                // log on Run
                if (execute && _results.Any() && !string.IsNullOrWhiteSpace(TxtLogPath.Text))
                {
                    try { ExcelServiceExtensions.WriteMoveLog(TxtLogPath.Text, _results); }
                    catch (Exception exLog)
                    {
                        System.Windows.MessageBox.Show($"Moved, but failed to write log:\n{exLog.Message}", "Log",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }

                var ok = results.Count(x => x.Success);
                var fail = results.Count - ok;
                var mode = execute ? "Run" : "Preview";
                System.Windows.MessageBox.Show($"{mode} complete.\nOK: {ok}  Fail: {fail}", "Sloth",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}