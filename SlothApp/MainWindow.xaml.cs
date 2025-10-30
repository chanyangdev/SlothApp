using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32; // OpenFileDialog / SaveFileDialog
using Sloth.Core.Models;
using Sloth.Core.Services;

namespace SlothApp
{
    public partial class MainWindow : Window
    {
        private SlothConfig? _cfg;
        private List<Customer> _customers = new();
        private ObservableCollection<MoveResult> _results = new();

        public MainWindow()
        {
            InitializeComponent();
            GridResults.DataContext = _results;
        }

        // -------- Browsers ----------
        private void BrowseConfig_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = ".json",
                CheckFileExists = true
            };


            if (dlg.ShowDialog() == true)
                TxtConfig.Text = dlg.FileName;
        }

        private void BrowseExcel_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Excel files (*.xlsx;*.xlsm;*.xls)|*.xlsx;*.xlsm;*.xls|All files (*.*)|*.*"
            };
            if (dlg.ShowDialog() == true)
                TxtExcel.Text = dlg.FileName;
        }

        private void BrowseDestRoot_Click(object sender, RoutedEventArgs e)
        {
            // Use WinForms FolderBrowserDialog from WPF (fully qualified to avoid ambiguity)
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog())
            {
                dlg.Description = "Select destination ROOT folder (contains 주택/건물 or customer folders)";
                dlg.ShowNewFolderButton = true;
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    TxtDestRoot.Text = dlg.SelectedPath;
            }
        }

        private void BrowseSrcDir_Click(object sender, RoutedEventArgs e)
        {
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog())
            {
                dlg.Description = "Select folder containing the created files to move/rename";
                dlg.ShowNewFolderButton = false;
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    TxtSrcDir.Text = dlg.SelectedPath;
            }
        }

        private void BrowseLog_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = "SlothLog.xlsx"
            };
            if (dlg.ShowDialog() == true)
                TxtLogPath.Text = dlg.FileName;
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

                // If we already have a selected customer, populate doc codes now
                PopulateDocCodesForSelectedCustomer();
                System.Windows.MessageBox.Show("Config loaded.", "Sloth", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to load config:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                System.Windows.MessageBox.Show($"Failed to load customers:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

        // Whenever user changes selected customer, refresh available DocCodes
        private void CmbCustomer_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            PopulateDocCodesForSelectedCustomer();
        }

        // -------- Actions ----------
        private void Preview_Click(object sender, RoutedEventArgs e)
        {
            RunCore(execute: false);
        }

        private void Run_Click(object sender, RoutedEventArgs e)
        {
            RunCore(execute: true);
        }

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

                // Optional log on run
                if (execute && _results.Any() && !string.IsNullOrWhiteSpace(TxtLogPath.Text))
                {
                    try
                    {
                        ExcelServiceExtensions.WriteMoveLog(TxtLogPath.Text, _results);
                    }
                    catch (Exception exLog)
                    {
                        System.Windows.MessageBox.Show($"Moved, but failed to write log:\n{exLog.Message}", "Log", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }

                var ok = results.Count(x => x.Success);
                var fail = results.Count - ok;
                var mode = execute ? "Run" : "Preview";
                System.Windows.MessageBox.Show($"{mode} complete.\nOK: {ok}  Fail: {fail}", "Sloth", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
