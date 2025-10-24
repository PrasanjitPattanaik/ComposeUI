using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace XmlEditorChatApp
{
    public partial class MainWindow : Window
    {
        // ----------------- Terminal fields -----------------
        private bool _terminalVisible = false;
        private Process _shellProc;
        private bool _terminalStarted = false;
        private string _currentRootPath;       // current folder opened in the IDE
        private int _inputStartIndex = 0;      // index in TerminalTextBox where user input starts
        private bool _capturingPwd = false;    // CD marker capture
        private bool _capturingPwdArmed = false;

        // ----------------- ctor -----------------
        public MainWindow()
        {
            InitializeComponent();

            // Wire terminal editor behavior even if panel is hidden initially
            if (TerminalTextBox != null)
            {
                TerminalTextBox.IsReadOnly = false; // we manage "read-only regions" in code
                TerminalTextBox.AcceptsReturn = true;
                TerminalTextBox.AcceptsTab = true;

                TerminalTextBox.PreviewKeyDown += TerminalTextBox_PreviewKeyDown;
                TerminalTextBox.TextChanged += TerminalTextBox_TextChanged;
            }
        }

        // ----------------- Folder Tree -----------------
        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog();
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                LoadFolderStructure(dlg.SelectedPath);
                _currentRootPath = dlg.SelectedPath;

                // Sync terminal working dir if it is running
                if (_terminalVisible && _shellProc != null && !_shellProc.HasExited)
                {
                    ShellWriteLine($@"cd /d ""{_currentRootPath}""");
                    RequestPwdRefresh();
                    ShowPrompt();
                }
            }
        }

        private void LoadFolderStructure(string path)
        {
            FolderTree.Items.Clear();
            var rootItem = CreateTreeItem(path);
            FolderTree.Items.Add(rootItem);

            if (string.IsNullOrEmpty(_currentRootPath))
                _currentRootPath = path;
        }

        private TreeViewItem CreateTreeItem(string path)
        {
            // Header with icon + text
            var stack = new StackPanel { Orientation = Orientation.Horizontal };

            var img = new Image { Width = 16, Height = 16, Margin = new Thickness(0, 0, 5, 0) };
            string iconPath = Directory.Exists(path)
                ? "pack://application:,,,/Icons/open-folder.png"
                : "pack://application:,,,/Icons/new-file.png";
            try { img.Source = new BitmapImage(new Uri(iconPath)); } catch { /* ignore */ }
            stack.Children.Add(img);

            var txt = new TextBlock { Text = Path.GetFileName(path) };
            stack.Children.Add(txt);

            var item = new TreeViewItem
            {
                Header = stack,
                Tag = path
            };

            if (Directory.Exists(path))
            {
                foreach (var dir in Directory.GetDirectories(path))
                    item.Items.Add(CreateTreeItem(dir));
                foreach (var file in Directory.GetFiles(path))
                    item.Items.Add(CreateTreeItem(file));
            }

            return item;
        }

        private void FolderTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (FolderTree.SelectedItem is TreeViewItem item && File.Exists(item.Tag as string))
            {
                OpenXmlFile(item.Tag as string);
            }
        }

        // ----------------- AvalonEdit Tabbed Editor -----------------
        private void OpenXmlFile(string filePath)
        {
            // Check if file already open
            foreach (TabItem tab in XmlTabControl.Items)
            {
                if (tab.Tag as string == filePath)
                {
                    XmlTabControl.SelectedItem = tab;
                    return;
                }
            }

            // Create AvalonEdit editor
            var editor = new TextEditor
            {
                ShowLineNumbers = true,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 14,
                SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("XML"),
                Text = FormatXml(File.ReadAllText(filePath))
            };

            // Create tab
            var tabItem = new TabItem { Content = editor, Tag = filePath };

            // Header with filename + close button
            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };
            var fileNameText = new TextBlock
            {
                Text = System.IO.Path.GetFileName(filePath),
                Margin = new Thickness(0, 0, 5, 0)
            };
            var closeButton = new Button
            {
                Content = "✖",
                Width = 18,
                Height = 18,
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                ToolTip = "Close tab"
            };
            closeButton.Click += (s, e) => XmlTabControl.Items.Remove(tabItem);

            headerPanel.Children.Add(fileNameText);
            headerPanel.Children.Add(closeButton);

            tabItem.Header = headerPanel;

            XmlTabControl.Items.Add(tabItem);
            XmlTabControl.SelectedItem = tabItem;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (XmlTabControl.SelectedItem is TabItem selectedTab && selectedTab.Content is TextEditor editor)
            {
                string filePath = selectedTab.Tag as string;
                if (!string.IsNullOrEmpty(filePath))
                {
                    File.WriteAllText(filePath, editor.Text);
                    MessageBox.Show($"File saved: {filePath}");
                }
            }
        }

        // ----------------- File Creation -----------------
        private void CreateFile_Click(object sender, RoutedEventArgs e)
        {
            if (FolderTree.SelectedItem is TreeViewItem item)
            {
                string folderPath = item.Tag.ToString();
                if (File.Exists(folderPath)) folderPath = Path.GetDirectoryName(folderPath)!;

                string newFilePath = System.IO.Path.Combine(folderPath, "NewFile.xml");
                int i = 1;
                while (File.Exists(newFilePath))
                    newFilePath = System.IO.Path.Combine(folderPath, $"NewFile{i++}.xml");

                File.WriteAllText(newFilePath, "<root></root>");
                LoadFolderStructure(folderPath);
            }
        }

        // ----------------- Chat -----------------
        private void ChatInput_GotFocus(object sender, RoutedEventArgs e)
        {
            if (ChatInput.Text == "Type a message...")
            {
                ChatInput.Text = "";
                ChatInput.Foreground = Brushes.Black;
            }
        }

        private void ChatInput_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ChatInput.Text))
            {
                ChatInput.Text = "Type a message...";
                ChatInput.Foreground = Brushes.Gray;
            }
        }

        private void ChatInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter
                && !string.IsNullOrWhiteSpace(ChatInput.Text)
                && ChatInput.Text != "Type a message...")
            {
                string msg = ChatInput.Text;
                ChatList.Items.Add("You: " + msg);
                ChatList.Items.Add("Bot: Hi");
                ChatInput.Clear();
            }
        }

        // ----------------- Terminal UI toggle -----------------
        private void ToggleTerminal_Click(object sender, RoutedEventArgs e)
        {
            _terminalVisible = !_terminalVisible;

            // Toggle visibility
            TerminalContainer.Visibility = _terminalVisible ? Visibility.Visible : Visibility.Collapsed;

            // Update button text
            ToggleTerminalLabel.Text = _terminalVisible ? "Hide Terminal" : "Show Terminal";

            if (_terminalVisible)
            {
                StartShellIfNeeded();
                // Show prompt if empty
                if (TerminalTextBox != null && TerminalTextBox.Text.Length == 0)
                {
                    TerminalWriteLine($"[cwd] {(_currentRootPath ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))}");
                    ShowPrompt();
                }
                TerminalTextBox.Focus();
                TerminalTextBox.CaretIndex = TerminalTextBox.Text.Length;
            }
            else
            {
                StopShellIfRunning();
            }

            // Optional: focus editor to avoid accidental typing when hidden
            if (!_terminalVisible)
                XmlTabControl.Focus();
        }

        // ----------------- Terminal: process lifecycle -----------------
        private void StartShellIfNeeded()
        {
            if (_terminalStarted) return;

            if (string.IsNullOrEmpty(_currentRootPath) || !Directory.Exists(_currentRootPath))
            {
                _currentRootPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                // /Q disables echo of commands; /K keeps the shell open; set empty prompt so we render our own
                Arguments = "/Q /K \"prompt =\"",
                WorkingDirectory = _currentRootPath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            _shellProc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _shellProc.OutputDataReceived += Shell_OutputDataReceived;
            _shellProc.ErrorDataReceived += Shell_ErrorDataReceived;
            _shellProc.Exited += (s, e) => Dispatcher.Invoke(() => TerminalWriteLine("[process exited]"));

            _shellProc.Start();
            _shellProc.BeginOutputReadLine();
            _shellProc.BeginErrorReadLine();

            _terminalStarted = true;

            // Make sure cmd uses our desired starting directory (also handles drive switch)
            ShellWriteLine($@"cd /d ""{_currentRootPath}""");
            RequestPwdRefresh();
        }

        private void StopShellIfRunning()
        {
            try
            {
                if (_shellProc != null && !_shellProc.HasExited)
                {
                    _shellProc.StandardInput.WriteLine("exit");
                    if (!_shellProc.WaitForExit(400))
                    {
                        _shellProc.Kill(entireProcessTree: true);
                    }
                }
            }
            catch { /* ignore */ }
            finally
            {
                _shellProc?.Dispose();
                _shellProc = null;
                _terminalStarted = false;
            }
        }

        // ----------------- Terminal: I/O helpers -----------------
        private void Shell_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null) return;

            // CD marker protocol to capture working dir
            if (e.Data == "__PWD__")
            {
                _capturingPwd = true;
                _capturingPwdArmed = true;
                return;
            }
            if (e.Data == "__ENDPWD__")
            {
                _capturingPwd = false;
                _capturingPwdArmed = false;
                return;
            }
            if (_capturingPwd && _capturingPwdArmed)
            {
                // The next non-empty line after __PWD__ is the current directory
                var line = e.Data.Trim();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    _currentRootPath = line;
                    _capturingPwdArmed = false;
                    // We don't print the captured path here; the prompt shows it.
                }
                return;
            }

            Dispatcher.Invoke(() =>
            {
                TerminalWriteLine(e.Data);
                // Keep caret at end but within input range
                TerminalTextBox.CaretIndex = TerminalTextBox.Text.Length;
                EnsureInputStartIsEnd();
            });
        }

        private void Shell_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null) return;
            Dispatcher.Invoke(() =>
            {
                TerminalWriteLine(e.Data);
                TerminalTextBox.CaretIndex = TerminalTextBox.Text.Length;
                EnsureInputStartIsEnd();
            });
        }

        private void ShellWriteLine(string command)
        {
            try
            {
                if (_shellProc != null && !_shellProc.HasExited)
                {
                    _shellProc.StandardInput.WriteLine(command);
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => TerminalWriteLine("[write failed] " + ex.Message));
            }
        }

        private void RequestPwdRefresh()
        {
            // Print markers around CD to reliably capture path
            // We use two echos to frame one 'cd' output line.
            ShellWriteLine("echo __PWD__");
            ShellWriteLine("cd");
            ShellWriteLine("echo __ENDPWD__");
        }

        // ----------------- Terminal: prompt & editing model -----------------
        private void ShowPrompt()
        {
            var path = string.IsNullOrEmpty(_currentRootPath)
                ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                : _currentRootPath;

            AppendRaw($"{path}> ");
            _inputStartIndex = TerminalTextBox.Text.Length;
        }

        private void AppendRaw(string text)
        {
            TerminalTextBox.AppendText(text);
            TerminalTextBox.CaretIndex = TerminalTextBox.Text.Length;
            TerminalTextBox.ScrollToEnd();
        }

        private void TerminalWriteLine(string text)
        {
            TerminalTextBox.AppendText(text + Environment.NewLine);
            TerminalTextBox.CaretIndex = TerminalTextBox.Text.Length;
            TerminalTextBox.ScrollToEnd();
        }

        private void EnsureInputStartIsEnd()
        {
            // Keep input start at end if we just printed something
            _inputStartIndex = TerminalTextBox.Text.Length;
        }

        // Prevent editing the read-only history part of the terminal
        private void TerminalTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (TerminalTextBox == null) return;

            int caret = TerminalTextBox.CaretIndex;

            // Block navigation into history
            if ((e.Key == Key.Left) && caret <= _inputStartIndex)
            {
                e.Handled = true;
                TerminalTextBox.CaretIndex = _inputStartIndex;
                return;
            }
            if ((e.Key == Key.Back) && caret <= _inputStartIndex)
            {
                e.Handled = true;
                return;
            }
            if ((e.Key == Key.Home))
            {
                e.Handled = true;
                TerminalTextBox.CaretIndex = _inputStartIndex;
                return;
            }

            // Execute on Enter
            if (e.Key == Key.Enter)
            {
                e.Handled = true;

                var fullText = TerminalTextBox.Text;
                var cmd = fullText.Substring(_inputStartIndex, fullText.Length - _inputStartIndex).TrimEnd('\r', '\n');

                // Print newline to finalize the command in UI
                AppendRaw(Environment.NewLine);

                if (string.IsNullOrWhiteSpace(cmd))
                {
                    ShowPrompt();
                    return;
                }

                // Echo command to the shell
                // Special handling for 'clear' convenience
                if (string.Equals(cmd, "clear", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(cmd, "cls", StringComparison.OrdinalIgnoreCase))
                {
                    TerminalTextBox.Clear();
                    // Send real 'cls' too so shell knows state
                    ShellWriteLine("cls");
                    ShowPrompt();
                    return;
                }

                if (_shellProc != null && !_shellProc.HasExited)
                {
                    ShellWriteLine(cmd);

                    // Keep our internal cwd in sync if user typed 'cd ...'
                    if (cmd.StartsWith("cd", StringComparison.OrdinalIgnoreCase))
                    {
                        RequestPwdRefresh();
                    }
                }
                else
                {
                    TerminalWriteLine("[error] shell is not running");
                }

                ShowPrompt();
            }
        }

        // Ensure we don't let user paste into history region
        private void TerminalTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TerminalTextBox == null) return;

            // If changes happened before _inputStartIndex (history area), revert them
            foreach (var change in e.Changes)
            {
                // If any removal/insertion touches history
                if (change.Offset < _inputStartIndex)
                {
                    // Simplest: move caret to input start and append prompt anew
                    TerminalTextBox.CaretIndex = TerminalTextBox.Text.Length;
                    return;
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            StopShellIfRunning();
            base.OnClosed(e);
        }

        // ----------------- XML formatting helper -----------------
        private string FormatXml(string xml)
        {
            try
            {
                var doc = new System.Xml.XmlDocument();
                doc.LoadXml(xml);

                using (var stringWriter = new StringWriter())
                using (var xmlTextWriter = new System.Xml.XmlTextWriter(stringWriter))
                {
                    xmlTextWriter.Formatting = System.Xml.Formatting.Indented;
                    xmlTextWriter.Indentation = 2; // spaces per indent
                    doc.Save(xmlTextWriter);
                    return stringWriter.ToString();
                }
            }
            catch
            {
                // If invalid XML, just return it unchanged
                return xml;
            }
        }
    }
}
