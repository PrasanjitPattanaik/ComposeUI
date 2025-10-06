using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace XmlEditorChatApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        // ----------------- Folder Tree -----------------
        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog();
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                LoadFolderStructure(dlg.SelectedPath);
            }
        }

        private void LoadFolderStructure(string path)
        {
            FolderTree.Items.Clear();
            var rootItem = CreateTreeItem(path);
            FolderTree.Items.Add(rootItem);
        }

        private TreeViewItem CreateTreeItem(string path)
        {
            // Header with icon + text
            var stack = new StackPanel { Orientation = Orientation.Horizontal };

            var img = new Image { Width = 16, Height = 16, Margin = new Thickness(0, 0, 5, 0) };
            string iconPath = Directory.Exists(path)
                ? "pack://application:,,,/Icons/open-folder.png"
                : "pack://application:,,,/Icons/new-file.png";
            img.Source = new BitmapImage(new Uri(iconPath));
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
                Text = Path.GetFileName(filePath),
                Margin = new Thickness(0, 0, 5, 0)
            };
            var closeButton = new Button
            {
                Content = "✖",
                Width = 16,
                Height = 16,
                Padding = new Thickness(0),
                Background = null,
                BorderBrush = null
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

                string newFilePath = Path.Combine(folderPath, "NewFile.xml");
                int i = 1;
                while (File.Exists(newFilePath))
                    newFilePath = Path.Combine(folderPath, $"NewFile{i++}.xml");

                File.WriteAllText(newFilePath, "<root></root>");
                LoadFolderStructure(folderPath);
            }
        }

        private void ChatInput_GotFocus(object sender, RoutedEventArgs e)
        {
            if (ChatInput.Text == "Type a message...")
            {
                ChatInput.Text = "";
                ChatInput.Foreground = System.Windows.Media.Brushes.Black;
            }
        }

        private void ChatInput_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ChatInput.Text))
            {
                ChatInput.Text = "Type a message...";
                ChatInput.Foreground = System.Windows.Media.Brushes.Gray;
            }
        }

        // ----------------- Chat -----------------
        private void ChatInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter
                && !string.IsNullOrWhiteSpace(ChatInput.Text)
                && ChatInput.Text != "Type a message...") // ignore placeholder
            {
                string msg = ChatInput.Text;
                ChatList.Items.Add("You: " + msg);
                ChatList.Items.Add("Bot: Hi");
                ChatInput.Clear();
            }
        }

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
