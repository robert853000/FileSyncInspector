using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using System.Security.Cryptography;
namespace FileSyncInspector
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private CancellationTokenSource _cts;
        public MainWindow()
        {
            InitializeComponent();
        }

        private void ButtonBrowseSource_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Vyberte zdrojovou složku";
                dialog.ShowNewFolderButton = false;

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    TextBoxSourceFolder.Text = dialog.SelectedPath;
                    UpdateStartButtonState();
                }
            }
        }

        private void ButtonBrowseTarget_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Vyberte cílovou složku";
                dialog.ShowNewFolderButton = false;

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    TextBoxTargetFolder.Text = dialog.SelectedPath;
                    UpdateStartButtonState();
                }
            }
        }

        private void UpdateStartButtonState()
        {
            // Povolit tlačítko Start, pokud jsou obě složky vybrané a nejsou stejné
            ButtonStart.IsEnabled = !string.IsNullOrWhiteSpace(TextBoxSourceFolder.Text)
                                  && !string.IsNullOrWhiteSpace(TextBoxTargetFolder.Text)
                                  && TextBoxSourceFolder.Text != TextBoxTargetFolder.Text;
        }

        private async void ButtonStart_Click(object sender, RoutedEventArgs e)
        {
            ButtonStart.IsEnabled = false;
            ButtonCancel.IsEnabled = true;
            ProgressBarComparison.Value = 0;

            _cts = new CancellationTokenSource();
   
            try
            {
                await Task.Run(() =>
                {
                    Thread.Sleep(1000); // jen simulace práce
                });

               // System.Windows.MessageBox.Show("Hotovo");
                if (string.IsNullOrWhiteSpace(TextBoxSourceFolder.Text))
                {
                    System.Windows.MessageBox.Show("Zdrojová cesta není zadána!");
                    return;
                }
                if (string.IsNullOrWhiteSpace(TextBoxTargetFolder.Text))
                {
                    System.Windows.MessageBox.Show("Cílová cesta není zadána!");
                    return;
                }

                // System.Windows.MessageBox.Show("Zdrojová cesta: " + TextBoxSourceFolder.Text);
                //System.Windows.MessageBox.Show("Cílová cesta: " + TextBoxTargetFolder.Text);           // Zde zavoláš asynchronní metodu porovnávání
                // await Task.Run(() => CompareFolders(TextBoxSourceFolder.Text, TextBoxTargetFolder.Text, _cts.Token));
                await CompareFoldersAsync(TextBoxSourceFolder.Text, TextBoxTargetFolder.Text, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                System.Windows.MessageBox.Show("Porovnávání bylo zrušeno.");
            }
            finally
            {
                ButtonStart.IsEnabled = true;
                ButtonCancel.IsEnabled = false;
                _cts = null;
            }
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
            }   
        }
        private TreeViewItem CreateTreeViewItem(FileSystemItem item)
        {
            var treeViewItem = new TreeViewItem
            {
                Header = item.Name,
                Tag = item.FullPath
            };

            // Nastav barvu podle statusu
            switch (item.Status)
            {
                case "missing":
                    treeViewItem.Foreground = Brushes.Red;
                    break;
                case "added":
                    treeViewItem.Foreground = Brushes.Green;
                    break;
                case "modified":
                    treeViewItem.Foreground = Brushes.Orange;
                    break;
                default:
                    treeViewItem.Foreground = Brushes.Black;
                    break;
            }

            // Rekurzivně přidej děti
            foreach (var child in item.Children)
            {
                treeViewItem.Items.Add(CreateTreeViewItem(child));
            }

            return treeViewItem;
        }
        private FileSystemItem BuildTree(IEnumerable<string> paths)
        {
             var root = new FileSystemItem { Name = "Root" };

            foreach (var path in paths)
            {
                var parts = path.Split(System.IO.Path.DirectorySeparatorChar);
                var current = root;

                foreach (var part in parts)
                {
                    var existing = current.Children.FirstOrDefault(c => c.Name == part);
                    if (existing == null)
                    {
                        var newItem = new FileSystemItem { Name = part };
                        current.Children.Add(newItem);
                        current = newItem;
                    }
                    else
                    {
                        current = existing;
                    }
                }
            }

            return root;
        }
        private void ToggleView_Click(object sender, RoutedEventArgs e)
        {
            if (TreeViewPanel.Visibility == Visibility.Visible)
            {
                TreeViewPanel.Visibility = Visibility.Collapsed;
                TextOutputPanel.Visibility = Visibility.Visible;
            }
            else
            {
                TreeViewPanel.Visibility = Visibility.Visible;
                TextOutputPanel.Visibility = Visibility.Collapsed;
            }
        }
        private void AddPathToTree(FileSystemItem root, string fullPath, string rootPath, string status)
        {
            var relative = System.IO.Path.GetRelativePath(rootPath, fullPath);
            var parts = relative.Split(System.IO.Path.DirectorySeparatorChar);
            var current = root;

            string currentPath = rootPath;

            foreach (var part in parts)
            {
                currentPath = System.IO.Path.Combine(currentPath, part);
                var child = current.Children.FirstOrDefault(c => c.Name == part);
                if (child == null)
                {
                    child = new FileSystemItem
                    {
                        Name = part,
                        FullPath = currentPath
                    };
                    current.Children.Add(child);
                }
                current = child;
            }

            // Nastav status až na poslední úrovni
            current.Status = status;
        }
        private async Task CompareFoldersAsync(string sourcePath, string targetPath, CancellationToken token)
        {
            // Připrav UI (na UI vlákně)
            await Dispatcher.InvokeAsync(() =>
            {
                TextBoxSourceOutput.Clear();
                TextBoxTargetOutput.Clear();
                ProgressBarComparison.Value = 0;
                TreeViewSourceOutput.Items.Clear();
                TreeViewTargetOutput.Items.Clear();
            });

            string MakeRelative(string fullPath, string root) =>
                System.IO.Path.GetRelativePath(root, fullPath).TrimEnd(System.IO.Path.DirectorySeparatorChar);

            // HashSety pro rychlé porovnání (case-insensitive)
            var sourceDirsRel = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var targetDirsRel = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var sourceFilesRel = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var targetFilesRel = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Seznamy rozdílů (pouze pro výstup)
            var missingInTargetDirs = new List<string>();
            var missingInSourceDirs = new List<string>();
            var missingInTargetFiles = new List<string>();
            var missingInSourceFiles = new List<string>();
            var differentFiles = new List<string>();

            var sourceRoot = new FileSystemItem { Name = System.IO.Path.GetFileName(sourcePath), FullPath = sourcePath };
            var targetRoot = new FileSystemItem { Name = System.IO.Path.GetFileName(targetPath), FullPath = targetPath };

            try
            {
                // 1) Spočítej počty (pro správné procento)
                int sourceDirCount = Directory.EnumerateDirectories(sourcePath, "*", SearchOption.AllDirectories).Count();
                int targetDirCount = Directory.EnumerateDirectories(targetPath, "*", SearchOption.AllDirectories).Count();
                int sourceFileCount = Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories).Count();
                int targetFileCount = Directory.EnumerateFiles(targetPath, "*", SearchOption.AllDirectories).Count();

                // 2) Naplň hashsety (streamovaně) - druhé průchod (rozumná paměť)
                foreach (var d in Directory.EnumerateDirectories(sourcePath, "*", SearchOption.AllDirectories))
                {
                    token.ThrowIfCancellationRequested();
                    sourceDirsRel.Add(MakeRelative(d, sourcePath));
                }

                foreach (var d in Directory.EnumerateDirectories(targetPath, "*", SearchOption.AllDirectories))
                {
                    token.ThrowIfCancellationRequested();
                    targetDirsRel.Add(MakeRelative(d, targetPath));
                }

                foreach (var f in Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories))
                {
                    token.ThrowIfCancellationRequested();
                    sourceFilesRel.Add(MakeRelative(f, sourcePath));
                }

                foreach (var f in Directory.EnumerateFiles(targetPath, "*", SearchOption.AllDirectories))
                {
                    token.ThrowIfCancellationRequested();
                    targetFilesRel.Add(MakeRelative(f, targetPath));
                }

                // 3) připrav totalSteps tak, aby zahrnoval i porovnání obsahu společných souborů
                var commonFiles = sourceFilesRel.Intersect(targetFilesRel).ToList();
                int totalSteps = sourceDirsRel.Count + targetDirsRel.Count + sourceFilesRel.Count + targetFilesRel.Count + commonFiles.Count;

                // Safety: pokud nic není, nastavíme totalSteps na 1, aby se nedělilo nulou
                if (totalSteps == 0) totalSteps = 1;

                int currentStep = 0;
                int lastProgress = -1;

                void UpdateProgress()
                {
                    int progress = (int)((double)currentStep / totalSteps * 100);
                    if (progress != lastProgress)
                    {
                        lastProgress = progress;
                        // jen jedna volání na Dispatcher při změně o 1 %
                        Dispatcher.Invoke(() => ProgressBarComparison.Value = progress);
                    }
                }

                // 4) Porovnej adresáře (v paměti)
                foreach (var dir in sourceDirsRel)
                {
                    token.ThrowIfCancellationRequested();
                    if (!targetDirsRel.Contains(dir))
                    {
                        missingInTargetDirs.Add(dir);
                        // přidej do stromu (pouze reálná cesta, bez přidávání textu)
                        AddPathToTree(sourceRoot, System.IO.Path.Combine(sourcePath, dir), sourcePath, "missing");
                    }
                    currentStep++; UpdateProgress();
                }

                foreach (var dir in targetDirsRel)
                {
                    token.ThrowIfCancellationRequested();
                    if (!sourceDirsRel.Contains(dir))
                    {
                        missingInSourceDirs.Add(dir);
                        AddPathToTree(targetRoot, System.IO.Path.Combine(targetPath, dir), targetPath, "added");
                    }
                    currentStep++; UpdateProgress();
                }

                // 5) Porovnej existenci souborů + obsah u společných (obsah porovnáváme až v další sekci)
                foreach (var file in sourceFilesRel)
                {
                    token.ThrowIfCancellationRequested();
                    if (!targetFilesRel.Contains(file))
                    {
                        missingInTargetFiles.Add(file);
                        AddPathToTree(sourceRoot, System.IO.Path.Combine(sourcePath, file), sourcePath, "missing");
                    }
                    currentStep++; UpdateProgress();
                }

                foreach (var file in targetFilesRel)
                {
                    token.ThrowIfCancellationRequested();
                    if (!sourceFilesRel.Contains(file))
                    {
                        missingInSourceFiles.Add(file);
                        AddPathToTree(targetRoot, System.IO.Path.Combine(targetPath, file), targetPath, "added");
                    }
                    currentStep++; UpdateProgress();
                }

                // 6) Porovnání obsahu u společných souborů (spouštíme na threadpoolu, po jednom, aby se nezamkla UI)
                foreach (var file in commonFiles)
                {
                    token.ThrowIfCancellationRequested();

                    var fullSourcePath = System.IO.Path.Combine(sourcePath, file);
                    var fullTargetPath = System.IO.Path.Combine(targetPath, file);

                    bool areEqual = await Task.Run(() => FilesAreEqual(fullSourcePath, fullTargetPath), token);

                    if (!areEqual)
                    {
                        differentFiles.Add(file);
                        AddPathToTree(sourceRoot, fullSourcePath, sourcePath, "modified");
                        AddPathToTree(targetRoot, fullTargetPath, targetPath, "modified");
                    }

                    currentStep++; UpdateProgress();
                }

                // 7) Výsledky: sestav textový výstup najednou a vykresli TreeView jednou (barvy se nastaví CreateTreeViewItem)
                await Dispatcher.InvokeAsync(() =>
                {
                    var sbSource = new StringBuilder();
                    var sbTarget = new StringBuilder();

                    if (missingInTargetDirs.Count > 0)
                    {
                        sbSource.AppendLine("Chybějící složky v cíli:");
                        foreach (var d in missingInTargetDirs) sbSource.AppendLine(d);
                        sbSource.AppendLine();
                    }

                    if (missingInTargetFiles.Count > 0)
                    {
                        sbSource.AppendLine("Chybějící soubory v cíli:");
                        foreach (var f in missingInTargetFiles) sbSource.AppendLine(f);
                        sbSource.AppendLine();
                    }

                    if (differentFiles.Count > 0)
                    {
                        sbSource.AppendLine("Rozdílné soubory (ve zdroji):");
                        foreach (var f in differentFiles) sbSource.AppendLine(f);
                        sbSource.AppendLine();
                    }

                    if (missingInSourceDirs.Count > 0)
                    {
                        sbTarget.AppendLine("Chybějící složky ve zdroji:");
                        foreach (var d in missingInSourceDirs) sbTarget.AppendLine(d);
                        sbTarget.AppendLine();
                    }

                    if (missingInSourceFiles.Count > 0)
                    {
                        sbTarget.AppendLine("Chybějící soubory ve zdroji:");
                        foreach (var f in missingInSourceFiles) sbTarget.AppendLine(f);
                        sbTarget.AppendLine();
                    }

                    if (differentFiles.Count > 0)
                    {
                        sbTarget.AppendLine("Rozdílné soubory (v cíli):");
                        foreach (var f in differentFiles) sbTarget.AppendLine(f);
                        sbTarget.AppendLine();
                    }

                    // Pokud nic nenašlo, napiš to
                    if (!missingInTargetDirs.Any() && !missingInSourceDirs.Any()
                        && !missingInTargetFiles.Any() && !missingInSourceFiles.Any() && !differentFiles.Any())
                    {
                        const string message = "Složky jsou zcela identické (100% shoda).";
                        TextBoxSourceOutput.Text = message + Environment.NewLine;
                        TextBoxTargetOutput.Text = message + Environment.NewLine;

                        TreeViewSourceOutput.Items.Clear();
                        TreeViewSourceOutput.Items.Add(new TreeViewItem { Header = message, Foreground = Brushes.Black });

                        TreeViewTargetOutput.Items.Clear();
                        TreeViewTargetOutput.Items.Add(new TreeViewItem { Header = message, Foreground = Brushes.Black });
                    }
                    else
                    {
                        // Naplníme textová pole najednou
                        TextBoxSourceOutput.Text = sbSource.ToString();
                        TextBoxTargetOutput.Text = sbTarget.ToString();

                        // Naplníme TreeView jednorázově (CreateTreeViewItem použije barvy podle statusu)
                        TreeViewSourceOutput.Items.Clear();
                        TreeViewSourceOutput.Items.Add(CreateTreeViewItem(sourceRoot));

                        TreeViewTargetOutput.Items.Clear();
                        TreeViewTargetOutput.Items.Add(CreateTreeViewItem(targetRoot));
                    }

                    // Ujisti se, že progress je 100% po dokončení
                    ProgressBarComparison.Value = 100;
                });
            }
            catch (OperationCanceledException)
            {
                // pokud uživatel zrušil, předáme dál
                await Dispatcher.InvokeAsync(() =>
                {
                    TextBoxSourceOutput.AppendText("Porovnávání zrušeno uživatelem." + Environment.NewLine);
                    TextBoxTargetOutput.AppendText("Porovnávání zrušeno uživatelem." + Environment.NewLine);
                    ProgressBarComparison.Value = 0;
                });
            }
            catch (Exception ex)
            {
                // zachytit neočekávané chyby (práva, cesta atd.)
                await Dispatcher.InvokeAsync(() =>
                {
                    //DialogResult dialogResult = System.Windows.Forms.MessageBox.Show($"Chyba při porovnávání: {ex.Message}", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }
        private async Task CompareFoldersAsync_deleted(string sourcePath, string targetPath, CancellationToken token)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                TextBoxSourceOutput.Clear();
                TextBoxTargetOutput.Clear();
                ProgressBarComparison.Value = 0;
            });

            string MakeRelative(string fullPath, string root) =>
                System.IO.Path.GetRelativePath(root, fullPath).TrimEnd(System.IO.Path.DirectorySeparatorChar);

            // HashSety pro rychlé porovnání
            var sourceDirsRel = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var targetDirsRel = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var sourceFilesRel = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var targetFilesRel = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Výstupní seznamy pro zobrazení na konci
            var missingInTargetDirs = new List<string>();
            var missingInSourceDirs = new List<string>();
            var missingInTargetFiles = new List<string>();
            var missingInSourceFiles = new List<string>();
            var differentFiles = new List<string>();

            // Spočítej celkový počet kroků
            int totalSteps = 0;
            totalSteps += Directory.EnumerateDirectories(sourcePath, "*", SearchOption.AllDirectories).Count();
            totalSteps += Directory.EnumerateDirectories(targetPath, "*", SearchOption.AllDirectories).Count();
            totalSteps += Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories).Count();
            totalSteps += Directory.EnumerateFiles(targetPath, "*", SearchOption.AllDirectories).Count();

            int currentStep = 0;
            int lastProgress = -1;

            void UpdateProgress()
            {
                int progress = (int)((double)currentStep / totalSteps * 100);
                if (progress != lastProgress)
                {
                    lastProgress = progress;
                    Dispatcher.Invoke(() => ProgressBarComparison.Value = progress);
                }
            }

            // 1) Načti relativní cesty
            foreach (var d in Directory.EnumerateDirectories(sourcePath, "*", SearchOption.AllDirectories))
            {
                token.ThrowIfCancellationRequested();
                sourceDirsRel.Add(MakeRelative(d, sourcePath));
                currentStep++; UpdateProgress();
            }

            foreach (var d in Directory.EnumerateDirectories(targetPath, "*", SearchOption.AllDirectories))
            {
                token.ThrowIfCancellationRequested();
                targetDirsRel.Add(MakeRelative(d, targetPath));
                currentStep++; UpdateProgress();
            }

            foreach (var f in Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories))
            {
                token.ThrowIfCancellationRequested();
                sourceFilesRel.Add(MakeRelative(f, sourcePath));
                currentStep++; UpdateProgress();
            }

            foreach (var f in Directory.EnumerateFiles(targetPath, "*", SearchOption.AllDirectories))
            {
                token.ThrowIfCancellationRequested();
                targetFilesRel.Add(MakeRelative(f, targetPath));
                currentStep++; UpdateProgress();
            }

            // 2) Porovnej adresáře
            foreach (var dir in sourceDirsRel)
            {
                if (!targetDirsRel.Contains(dir))
                    missingInTargetDirs.Add(dir);
            }
            foreach (var dir in targetDirsRel)
            {
                if (!sourceDirsRel.Contains(dir))
                    missingInSourceDirs.Add(dir);
            }

            // 3) Porovnej soubory (existence + obsah)
            foreach (var file in sourceFilesRel)
            {
                if (!targetFilesRel.Contains(file))
                {
                    missingInTargetFiles.Add(file);
                }
                else
                {
                    var sourceFilePath = System.IO.Path.Combine(sourcePath, file);
                    var targetFilePath = System.IO.Path.Combine(targetPath, file);

                    if (!FilesAreEqual(sourceFilePath, targetFilePath))
                        differentFiles.Add(file);
                }
            }
            foreach (var file in targetFilesRel)
            {
                if (!sourceFilesRel.Contains(file))
                    missingInSourceFiles.Add(file);
            }

            // 4) Výpis výsledků do UI najednou
            await Dispatcher.InvokeAsync(() =>
            {
                if (missingInTargetDirs.Count > 0)
                {
                    TextBoxSourceOutput.AppendText("Chybějící složky v cíli:\n");
                    foreach (var dir in missingInTargetDirs)
                        TextBoxSourceOutput.AppendText(dir + "\n");
                    TextBoxSourceOutput.AppendText("\n");
                }
                if (missingInSourceDirs.Count > 0)
                {
                    TextBoxTargetOutput.AppendText("Chybějící složky ve zdroji:\n");
                    foreach (var dir in missingInSourceDirs)
                        TextBoxTargetOutput.AppendText(dir + "\n");
                    TextBoxTargetOutput.AppendText("\n");
                }
                if (missingInTargetFiles.Count > 0)
                {
                    TextBoxSourceOutput.AppendText("Chybějící soubory v cíli:\n");
                    foreach (var file in missingInTargetFiles)
                        TextBoxSourceOutput.AppendText(file + "\n");
                    TextBoxSourceOutput.AppendText("\n");
                }
                if (missingInSourceFiles.Count > 0)
                {
                    TextBoxTargetOutput.AppendText("Chybějící soubory ve zdroji:\n");
                    foreach (var file in missingInSourceFiles)
                        TextBoxTargetOutput.AppendText(file + "\n");
                    TextBoxTargetOutput.AppendText("\n");
                }
                if (differentFiles.Count > 0)
                {
                    TextBoxSourceOutput.AppendText("Rozdílné soubory:\n");
                    foreach (var file in differentFiles)
                        TextBoxSourceOutput.AppendText(file + "\n");
                    TextBoxSourceOutput.AppendText("\n");
                }
            });
        }

        private async Task CompareFoldersAsync2_deleted3(string sourcePath, string targetPath, CancellationToken token)
        {
            //  TextBoxSourceOutput.AppendText("asdaszzzzzzzzzzzzzzzzzzzzd");
            // Vyčistí výstupy před začátkem
            await Dispatcher.InvokeAsync(() =>
            {
               TextBoxSourceOutput.Clear();
                TextBoxTargetOutput.Clear();
                ProgressBarComparison.Value = 0;



               

            });
            var sourceRoot = new FileSystemItem { Name = System.IO.Path.GetFileName(sourcePath), FullPath = sourcePath };
            var targetRoot = new FileSystemItem { Name = System.IO.Path.GetFileName(targetPath), FullPath = targetPath };
            var sourceDirs = Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories);
            var targetDirs = Directory.GetDirectories(targetPath, "*", SearchOption.AllDirectories);

            var sourceFiles = Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories);
            var targetFiles = Directory.GetFiles(targetPath, "*", SearchOption.AllDirectories);

            // Pomocné dictionary pro rychlé hledání relativních cest
            string MakeRelative(string fullPath, string root) =>
                System.IO.Path.GetRelativePath(root, fullPath).TrimEnd(System.IO.Path.DirectorySeparatorChar);

            var sourceDirsRel = new HashSet<string>(sourceDirs.Select(d => MakeRelative(d, sourcePath)));
            var targetDirsRel = new HashSet<string>(targetDirs.Select(d => MakeRelative(d, targetPath)));

            var sourceFilesRel = new HashSet<string>(sourceFiles.Select(f => MakeRelative(f, sourcePath)));
            var targetFilesRel = new HashSet<string>(targetFiles.Select(f => MakeRelative(f, targetPath)));

            int totalSteps = sourceDirsRel.Count + targetDirsRel.Count + sourceFilesRel.Count + targetFilesRel.Count;
            int currentStep = 0;
            int lastProgress = -1;
            // Funkce pro aktualizaci progress baru
            void UpdateProgress()
            {
                int progress = (int)((double)currentStep / totalSteps * 100);
                if (progress != lastProgress) // změna alespoň o 1 %
                {
                    lastProgress = progress;
                    Dispatcher.Invoke(() => ProgressBarComparison.Value = progress); Thread.Sleep(100);
                }
            }

            // 1) Najdi složky, které jsou v source, ale chybí v target (červeně)
            foreach (var dir in sourceDirsRel)
            {
                token.ThrowIfCancellationRequested();
                if (!targetDirsRel.Contains(dir))
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        TextBoxSourceOutput.AppendText($"{dir} (missing dir)\n");
                        TextBoxSourceOutput.Select(TextBoxSourceOutput.Text.Length - dir.Length - 1, dir.Length);
                        TextBoxSourceOutput.SelectionBrush = Brushes.Red;

                    }); Thread.Sleep(1000);  AddPathToTree(sourceRoot, System.IO.Path.Combine(sourcePath, dir) + " (missing dir)", sourcePath, "missing");
                }
                currentStep++;
                UpdateProgress();
            }

            // 2) Najdi složky, které jsou v target, ale chybí v source (zeleně)
            foreach (var dir in targetDirsRel)
            {
                token.ThrowIfCancellationRequested();
                if (!sourceDirsRel.Contains(dir))
                {
                  
                    await Dispatcher.InvokeAsync(() =>
                    {
                        TextBoxTargetOutput.AppendText($"{dir} (added dir)\n");
                        TextBoxTargetOutput.Select(TextBoxTargetOutput.Text.Length - dir.Length - 1, dir.Length);
                        TextBoxTargetOutput.SelectionBrush = Brushes.Green;
                    }); AddPathToTree(targetRoot, System.IO.Path.Combine(targetPath, dir) + " (added dir)", targetPath, "added");
                }
                currentStep++;
                UpdateProgress();
            }

            // 3) Porovnej soubory podle relativních cest - které jsou v source, ale chybí v target (červeně)
            foreach (var file in sourceFilesRel)
            {
                token.ThrowIfCancellationRequested();
                if (!targetFilesRel.Contains(file))
                {
                    var fullSourceFilePath = System.IO.Path.Combine(sourcePath, file);
                    await Dispatcher.InvokeAsync(() =>
                    {
                        TextBoxSourceOutput.AppendText($"{fullSourceFilePath} (missing)\n");
                        TextBoxSourceOutput.Select(TextBoxSourceOutput.Text.Length - file.Length - 11, file.Length + 10);
                        TextBoxSourceOutput.SelectionBrush = Brushes.Red;
                       


                    }); AddPathToTree(sourceRoot, fullSourceFilePath + " (missing)", sourcePath, "missing");
                }
                currentStep++;
                UpdateProgress();
            }

            // 4) Porovnej soubory podle relativních cest - které jsou v target, ale chybí v source (zeleně)
            foreach (var file in targetFilesRel)
            {
                token.ThrowIfCancellationRequested();
                if (!sourceFilesRel.Contains(file))
                {
                    var fullSourceFilePath = System.IO.Path.Combine(sourcePath, file);
                    await Dispatcher.InvokeAsync(() =>
                    {
                        TextBoxTargetOutput.AppendText($"{fullSourceFilePath} (added)\n");
                        TextBoxTargetOutput.Select(TextBoxTargetOutput.Text.Length - file.Length - 8, file.Length + 7);
                        TextBoxTargetOutput.SelectionBrush = Brushes.Green;
                    });

                    AddPathToTree(targetRoot, fullSourceFilePath + " (added)", targetPath, "added");
                }
                currentStep++;
                UpdateProgress();
            }

            // 5) Porovnej soubory co existují v obou a zjisti rozdíly bit po bitu
            var commonFiles = sourceFilesRel.Intersect(targetFilesRel);

            foreach (var file in commonFiles)
            {
                token.ThrowIfCancellationRequested();

                var fullSourcePath = System.IO.Path.Combine(sourcePath, file);
                var fullTargetPath = System.IO.Path.Combine(targetPath, file);

                bool areEqual = await Task.Run(() => FilesAreEqual(fullSourcePath, fullTargetPath));

                if (!areEqual)
                {
                    var fullSourceFilePath = System.IO.Path.Combine(sourcePath, file);
                    await Dispatcher.InvokeAsync(() =>
                    {
                        TextBoxSourceOutput.AppendText($"{fullSourceFilePath} (modified)\n");
                        TextBoxSourceOutput.Select(TextBoxSourceOutput.Text.Length - file.Length - 11, file.Length + 10);
                        TextBoxSourceOutput.SelectionBrush = Brushes.Orange;

                        TextBoxTargetOutput.AppendText($"{fullTargetPath} (modified)\n");
                        TextBoxTargetOutput.Select(TextBoxTargetOutput.Text.Length - file.Length - 11, file.Length + 10);
                        TextBoxTargetOutput.SelectionBrush = Brushes.Orange;
                    });

                       AddPathToTree(sourceRoot, fullSourcePath + " (modified)", sourcePath, "modified");
        AddPathToTree(targetRoot, fullTargetPath + " (modified)", targetPath, "modified");
                }

                currentStep++;
                UpdateProgress();
              
            }  bool hasDifferences =
    sourceDirsRel.Except(targetDirsRel).Any() ||
    targetDirsRel.Except(sourceDirsRel).Any() ||
    sourceFilesRel.Except(targetFilesRel).Any() ||
    targetFilesRel.Except(sourceFilesRel).Any() ||
    commonFiles.Any(file =>
    {
        var fullSourcePath = System.IO.Path.Combine(sourcePath, file);
        var fullTargetPath = System.IO.Path.Combine(targetPath, file);
        return !FilesAreEqual(fullSourcePath, fullTargetPath);
    }); if (!hasDifferences)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        string message = "Složky jsou zcela identické (100% shoda).";

                        TextBoxSourceOutput.AppendText(message + "\n");
                        TextBoxSourceOutput.SelectionBrush = Brushes.Black;

                        TextBoxTargetOutput.AppendText(message + "\n");
                        TextBoxTargetOutput.SelectionBrush = Brushes.Black;

                        // Do TreeView vložíme jednoduchý uzel s touto zprávou
                        TreeViewSourceOutput.Items.Clear();
                        TreeViewSourceOutput.Items.Add(new TreeViewItem { Header = message, Foreground = Brushes.Black });

                        TreeViewTargetOutput.Items.Clear();
                        TreeViewTargetOutput.Items.Add(new TreeViewItem { Header = message, Foreground = Brushes.Black });
                    });
                }
                else
                {
                    // Pokud jsou rozdíly, vykreslíme strom, jak máš nyní
                    // Po dokončení – jednorázově naplnit TreeView
                    Dispatcher.Invoke(() =>
                {
                    TreeViewSourceOutput.Items.Clear();
                    TreeViewSourceOutput.Items.Add(CreateTreeViewItem(sourceRoot));

                    TreeViewTargetOutput.Items.Clear();
                    TreeViewTargetOutput.Items.Add(CreateTreeViewItem(targetRoot));
                });
                }
        }

        // Pomocná metoda pro porovnání dvou souborů bit po bitu
        private bool FilesAreEqual(string file1, string file2)
        {
            const int bufferSize = 1024 * 1024; // 1 MB buffer

            using (var fs1 = new FileStream(file1, FileMode.Open, FileAccess.Read))
            using (var fs2 = new FileStream(file2, FileMode.Open, FileAccess.Read))
            {
                if (fs1.Length != fs2.Length)
                    return false;

                byte[] buffer1 = new byte[bufferSize];
                byte[] buffer2 = new byte[bufferSize];

                int bytesRead1;
                while ((bytesRead1 = fs1.Read(buffer1, 0, bufferSize)) > 0)
                {
                    fs2.Read(buffer2, 0, bytesRead1);

                    for (int i = 0; i < bytesRead1; i++)
                    {
                        if (buffer1[i] != buffer2[i])
                            return false;
                    }
                }
            }
            return true;
        }

        private void TextBoxTargetOutput_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
    }
}