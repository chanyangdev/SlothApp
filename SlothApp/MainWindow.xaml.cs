using System;
using System.ComponentModel; // CancelEventArgs
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32; // OpenFileDialog / SaveFileDialog
using SlothApp.Services; // UserSettings
using Sloth.Core.Models; // SlothConfig, Customer, MoveResult, SourceDoc
using Sloth.Core.Services; // ConfigService, ExcelService, BatchMoveService

namespace SlothApp
{
    public partial class MainWindow : Window
    {
        private readonly UserSettings _settings = UserSettings.Load();
        private SlothConfig? _cfg;
        private List<Customer> _customers = new();
        private readonly ObservableCollection<MoveResult> _results = new();
        private readonly ObservableCollection<QueueItem> _queue = new();
        private Dictionary<string, Customer> _customersById = new(StringComparer.OrdinalIgnoreCase);
        private CancellationTokenSource? _cts;

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Bind queue grid and customers list
            GridQueue.ItemsSource = _queue;
            LbCustomers.ItemsSource = _customers; // after LoadCustomers_Click this will refresh; optional
        }
        private void AddToQueue_Click(object sender, RoutedEventArgs e)
        {
            if (_cfg is null) { System.Windows.MessageBox.Show("Load Config first."); return; }
            if (_customers.Count ==0) { System.Windows.MessageBox.Show("Load Customers first."); return; }
            if (string.IsNullOrWhiteSpace(TxtSrcDir.Text) || !Directory.Exists(TxtSrcDir.Text))
            {
                System.Windows.MessageBox.Show("Set a valid Source Files Folder."); return;
            }

            var selectedCustomers = LbCustomers.SelectedItems.Cast<Customer>().ToList();
            if (selectedCustomers.Count ==0)
            {
                // fallback: add currently selected single customer
                if (CmbCustomer.SelectedItem is Customer c0) selectedCustomers.Add(c0);
            }
            if (selectedCustomers.Count ==0)
            {
                System.Windows.MessageBox.Show("Select at least one customer."); return;
            }

            var selectedDocs = LbDocCodes.SelectedItems.Cast<string>().ToList();
            var useAuto = ChkAutoDoc.IsChecked == true;
            if (!useAuto && selectedDocs.Count ==0)
            {
                System.Windows.MessageBox.Show("Select at least one Doc Code or enable Auto."); return;
            }

            foreach (var cust in selectedCustomers)
            {
                if (useAuto)
                {
                    _queue.Add(new QueueItem
                    {
                        CustomerId = cust.CustomerId ?? "",
                        CustomerName = cust.Name ?? "",
                        DocCode = "AUTO",
                        SourceDir = TxtSrcDir.Text
                    });
                }
                else
                {
                    foreach (var dc in selectedDocs)
                    {
                        _queue.Add(new QueueItem
                        {
                            CustomerId = cust.CustomerId ?? "",
                            CustomerName = cust.Name ?? "",
                            DocCode = dc,
                            SourceDir = TxtSrcDir.Text
                        });
                    }
                }
            }
        }

        private void RemoveSelectedQueue_Click(object sender, RoutedEventArgs e)
        {
            if (GridQueue.SelectedItems.Count ==0) return;
            var items = GridQueue.SelectedItems.Cast<QueueItem>().ToList();
            foreach (var it in items) _queue.Remove(it);
        }

        private void ClearQueue_Click(object sender, RoutedEventArgs e)
        {
            _queue.Clear();
        }

        private async void Preview_Click(object sender, RoutedEventArgs e) => await RunCoreAsync(execute: false);
        private async void Run_Click(object sender, RoutedEventArgs e) => await RunCoreAsync(execute: true);

        /// <summary>
        /// Main processing loop. Runs off the UI thread where possible and updates the UI via Dispatcher.
        /// </summary>
        private async Task RunCoreAsync(bool execute)
        {
            // cancel any previous run
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            try
            {
                // Basic validation on UI thread
                if (_cfg is null) throw new InvalidOperationException("Load Config first.");
                if (_customers.Count ==0) throw new InvalidOperationException("Load Customers first.");

                // Snapshot some UI values so background work doesn't need to touch UI controls
                string destRoot = string.Empty;
                string logPath = string.Empty;
                bool hasQueue = false;

                Dispatcher.Invoke(() =>
                {
                    destRoot = TxtDestRoot.Text;
                    logPath = TxtLogPath.Text;
                    hasQueue = _queue.Any();
                });

                // Validate dest root on UI thread
                if (string.IsNullOrWhiteSpace(destRoot) || !Directory.Exists(destRoot))
                    throw new DirectoryNotFoundException($"Dest root not found:\n{destRoot}");

                // Clear previous results on UI thread
                Dispatcher.Invoke(() => _results.Clear());

                // Ensure quick lookup for customers
                if (_customersById == null || _customersById.Count != _customers.Count)
                {
                    _customersById = _customers.ToDictionary(c => c.CustomerId ?? string.Empty, StringComparer.OrdinalIgnoreCase);
                }

                if (hasQueue)
                {
                    // Process each queued item sequentially, but avoid blocking UI
                    var queueSnapshot = _queue.ToList();
                    foreach (var qi in queueSnapshot)
                    {
                        ct.ThrowIfCancellationRequested();

                        if (!_customersById.TryGetValue(qi.CustomerId ?? string.Empty, out var cust))
                        {
                            var mr = new MoveResult { Success = false, Message = $"Customer not found: {qi.CustomerId}", CustomerId = qi.CustomerId, CustomerName = qi.CustomerName };
                            Dispatcher.Invoke(() => _results.Add(mr));
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(qi.SourceDir) || !Directory.Exists(qi.SourceDir))
                        {
                            var mr = new MoveResult { Success = false, Message = $"Source missing: {qi.SourceDir}", CustomerId = qi.CustomerId, CustomerName = qi.CustomerName };
                            Dispatcher.Invoke(() => _results.Add(mr));
                            continue;
                        }

                        var sources = new List<SourceDoc>();
                        foreach (var f in Directory.EnumerateFiles(qi.SourceDir))
                        {
                            ct.ThrowIfCancellationRequested();
                            var dc = qi.DocCode;
                            if (string.Equals(dc, "AUTO", StringComparison.OrdinalIgnoreCase))
                                dc = DocumentClassifierService.GuessDocCode(Path.GetFileName(f), _cfg!) ?? dc;

                            if (string.Equals(dc, "AUTO", StringComparison.OrdinalIgnoreCase))
                            {
                                var mr = new MoveResult { Success = false, Message = $"AUTO could not classify: {Path.GetFileName(f)}", CustomerId = qi.CustomerId, CustomerName = qi.CustomerName, SourcePath = f };
                                Dispatcher.Invoke(() => _results.Add(mr));
                                continue;
                            }

                            sources.Add(new SourceDoc { SourcePath = f, CustomerId = qi.CustomerId, DocCode = dc! });
                        }

                        if (sources.Count ==0) continue;

                        // Execute the batch on a background thread
                        List<MoveResult> batchResults = await Task.Run(() => BatchMoveService.PreviewAndExecute(sources, _customers, _cfg, destRoot, execute), ct);

                        // Marshal results to UI
                        Dispatcher.Invoke(() => { foreach (var r in batchResults) _results.Add(r); });
                    }
                }
                else
                {
                    // Single-customer path (existing behavior)
                    Customer? cust = null;
                    string docCode = string.Empty;
                    string srcDir = string.Empty;

                    Dispatcher.Invoke(() =>
                    {
                        if (CmbCustomer.SelectedItem is Customer c) cust = c;
                        docCode = (string?)CmbDocCode.SelectedItem ?? string.Empty;
                        srcDir = TxtSrcDir.Text;
                    });

                    if (cust is null) throw new InvalidOperationException("Select a customer.");
                    if (string.IsNullOrWhiteSpace(docCode)) throw new InvalidOperationException("Select a Doc Code.");
                    if (string.IsNullOrWhiteSpace(srcDir) || !Directory.Exists(srcDir)) throw new DirectoryNotFoundException($"Source folder not found:\n{srcDir}");

                    var files = Directory.EnumerateFiles(srcDir).ToList();
                    if (files.Count ==0) throw new InvalidOperationException("No files in Source folder.");

                    var sources = files.Select(f => new SourceDoc { SourcePath = f, CustomerId = cust.CustomerId, DocCode = docCode }).ToList();

                    var results = await Task.Run(() => BatchMoveService.PreviewAndExecute(sources, _customers, _cfg, destRoot, execute), ct);
                    Dispatcher.Invoke(() => { foreach (var r in results) _results.Add(r); });
                }

                // Ensure grid is refreshed (already bound to _results, but refresh if needed)
                Dispatcher.Invoke(() =>
                {
                    GridResults.ItemsSource = null;
                    GridResults.ItemsSource = _results;
                });

                // Log on run (append)
                if (execute && _results.Any() && !string.IsNullOrWhiteSpace(logPath))
                {
                    try
                    {
                        await Task.Run(() => ExcelServiceExtensions.WriteMoveLog(logPath, _results), ct);
                    }
                    catch (Exception exLog)
                    {
                        Dispatcher.Invoke(() => System.Windows.MessageBox.Show($"Moved, but failed to write log:\n{exLog.Message}", "Log", MessageBoxButton.OK, MessageBoxImage.Warning));
                    }
                }

                var ok = _results.Count(x => x.Success);
                var fail = _results.Count - ok;
                Dispatcher.Invoke(() => System.Windows.MessageBox.Show($"{(execute ? "Run" : "Preview")} complete.\nOK: {ok} Fail: {fail}", "Sloth", MessageBoxButton.OK, MessageBoxImage.Information));
            }
            catch (OperationCanceledException)
            {
                Dispatcher.Invoke(() => System.Windows.MessageBox.Show("Operation cancelled.", "Sloth", MessageBoxButton.OK, MessageBoxImage.Information));
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => System.Windows.MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error));
            }
            finally
            {
                // dispose token source
                _cts?.Dispose();
                _cts = null;
            }
        }

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

            // cancel any running operation
            _cts?.Cancel();
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
                if (_customers.Count >0)
                    CmbCustomer.SelectedIndex =0;

                // rebuild lookup
                _customersById = _customers.ToDictionary(c => c.CustomerId ?? string.Empty, StringComparer.OrdinalIgnoreCase);

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
                if (codes.Count >0) CmbDocCode.SelectedIndex =0;
            }
        }

        // refresh DocCodes when customer changes
        private void CmbCustomer_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PopulateDocCodesForSelectedCustomer();
        }
    }
}