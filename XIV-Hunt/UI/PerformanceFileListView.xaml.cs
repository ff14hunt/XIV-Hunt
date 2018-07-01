using FFXIV_GameSense.MML;
using FFXIV_GameSense.Properties;
using Splat;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace FFXIV_GameSense.UI
{
    public partial class PerformanceFileListView : UserControl
    {
        private GridViewColumnHeader _lastHeaderClicked = null;
        private ListSortDirection _lastDirection = ListSortDirection.Ascending;
        private bool filterApplied = false;
        private ObservableCollection<PerformanceListViewItem> collection = new ObservableCollection<PerformanceListViewItem>();
        private FileSystemWatcher performDirWatcher;
        private static readonly string[] fileTypes = new string[] { ".mml", ".txt" };//mmm ignore case?

        public PerformanceFileListView()
        {
            InitializeComponent();
            IndexPerformances();
            Settings.Default.SettingChanging += SettingChanging;
            ListView.ItemsSource = collection;
        }

        private void IndexPerformances(string nd = null)
        {
            collection.Clear();
            if (nd == null)
                nd = Settings.Default.PerformDirectory;
            if (Directory.Exists(nd))
            {
                DisposePerformDirWatcher();
                new Thread(() =>
                {
                    foreach (string s in Directory.EnumerateFiles(nd, "*.*", SearchOption.AllDirectories).Where(fn => fileTypes.Contains(Path.GetExtension(fn))))
                    {
                        if (HasNotes(s))
                        {
                            Dispatcher.Invoke(() =>
                            {
                                collection.Add(
                                    new PerformanceListViewItem
                                    {
                                        RelativePath = s.Substring(nd.Length + 1),
                                        LastModified = File.GetLastWriteTime(s)
                                    });
                            });
                        }
                    }
                }).Start();
                MakePerformDirWatcher(nd);
            }
        }

        private void MakePerformDirWatcher(string nd)
        {
            performDirWatcher = new FileSystemWatcher(nd)
            {
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };
            performDirWatcher.Created += PerformDirWatcher_Created;
            performDirWatcher.Changed += PerformDirWatcher_Changed;
            performDirWatcher.Deleted += PerformDirWatcher_Deleted;
        }

        private void PerformDirWatcher_Deleted(object sender, FileSystemEventArgs e)
        {
            if (fileTypes.Contains(Path.GetExtension(e.FullPath)))
            {
                //Debug.WriteLine(nameof(PerformDirWatcher_Deleted) + " " + e.FullPath);
                string rp = e.FullPath.Substring(Settings.Default.PerformDirectory.Length + 1);
                if(collection.Any(x=>x.RelativePath==rp))
                {
                    Dispatcher.Invoke(() =>
                    {
                        collection.Remove(collection.Single(x=>x.RelativePath==rp));
                    });
                }
            }
        }

        private void PerformDirWatcher_Created(object sender, FileSystemEventArgs e)
        {
            if (fileTypes.Contains(Path.GetExtension(e.FullPath)))
            {
                //Debug.WriteLine(nameof(PerformDirWatcher_Created) + " " + e.FullPath);
                if (HasNotes(e.FullPath))
                {
                    Dispatcher.Invoke(() =>
                    {
                        collection.Add(
                            new PerformanceListViewItem
                            {
                                RelativePath = e.FullPath.Substring(Settings.Default.PerformDirectory.Length + 1),
                                LastModified = File.GetLastWriteTime(e.FullPath)
                            });
                    });
                }
            }
        }

        private void PerformDirWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            if (fileTypes.Contains(Path.GetExtension(e.FullPath)))
            {
                //Debug.WriteLine(nameof(PerformDirWatcher_Changed) + " " + e.FullPath);
                PerformDirWatcher_Deleted(sender, e);
                PerformDirWatcher_Created(sender, e);
            }
        }

        private void DisposePerformDirWatcher()
        {
            if (performDirWatcher != null)
            {
                performDirWatcher.Created -= PerformDirWatcher_Created;
                performDirWatcher.Changed -= PerformDirWatcher_Changed;
                performDirWatcher.Deleted -= PerformDirWatcher_Deleted;
                performDirWatcher.Dispose();
                performDirWatcher = null;
            }
        }

        private bool HasNotes(string s)
        {
            try
            {
                var f = File.ReadAllLines(s);
                if (Path.GetExtension(s) == fileTypes[1])
                {
                    if (new Performance(string.Join(",", f)).Sheet.Count > 0)
                        return true;
                }
                var mml = new ImplementedPlayer();
                for (int i = 0; i < f.Length; i++)
                    f[i] = f[i].RemoveLineComments();
                var fmml = string.Join(string.Empty, f).RemoveBlockComments();
                mml.Load(fmml);
                return mml.Tracks.Any(x => x.notes.Any());
            }catch(Exception e)
            {
                LogHost.Default.WarnException($"{nameof(HasNotes)} Could not read file: {s}", e);
                return false;
            }
        }

        private void SettingChanging(object sender, SettingChangingEventArgs e)
        {
            if (e.SettingName == nameof(Settings.PerformDirectory))
            {
                Debug.WriteLine(nameof(SettingChanging) + " HIT");
                IndexPerformances((string)e.NewValue);
            }
        }

        private bool Filter(object obj)
        {
            if (string.IsNullOrWhiteSpace(FilterTextBox.Text))
                return true;
            else
            {
                PerformanceListViewItem item = (PerformanceListViewItem)obj;
                return item.RelativePath.IndexOf(FilterTextBox.Text, StringComparison.OrdinalIgnoreCase) >= 0
                    /*|| item.FileName.IndexOf(FilterTextBox.Text, StringComparison.OrdinalIgnoreCase) >= 0*/;
            }
        }

        private void FATEsListView_GridViewColumnHeaderClick(object sender, RoutedEventArgs e)
        {
            ListSortDirection direction;
            if (e.OriginalSource is GridViewColumnHeader headerClicked)
            {
                if (headerClicked.Role != GridViewColumnHeaderRole.Padding)
                {
                    if (headerClicked != _lastHeaderClicked)
                    {
                        direction = ListSortDirection.Ascending;
                    }
                    else
                    {
                        if (_lastDirection == ListSortDirection.Ascending)
                        {
                            direction = ListSortDirection.Descending;
                        }
                        else
                        {
                            direction = ListSortDirection.Ascending;
                        }
                    }

                    string header = (string)((GridViewColumnHeader)headerClicked.Column.Header).Tag;
                    Sort(header, direction);

                    if (direction == ListSortDirection.Ascending)
                    {
                        headerClicked.Column.HeaderTemplate = Resources["HeaderTemplateArrowUp"] as DataTemplate;
                    }
                    else
                    {
                        headerClicked.Column.HeaderTemplate = Resources["HeaderTemplateArrowDown"] as DataTemplate;
                    }

                    // Remove arrow from previously sorted header  
                    if (_lastHeaderClicked != null && _lastHeaderClicked != headerClicked)
                    {
                        _lastHeaderClicked.Column.HeaderTemplate = null;
                    }

                    _lastHeaderClicked = headerClicked;
                    _lastDirection = direction;
                }
            }
        }

        private void Sort(string sortBy, ListSortDirection direction)
        {
            ICollectionView dataView = CollectionViewSource.GetDefaultView(ListView.ItemsSource);
            if (dataView != null)
            {
                dataView.SortDescriptions.Clear();
                SortDescription sd = new SortDescription(sortBy, direction);
                dataView.SortDescriptions.Add(sd);
                dataView.Refresh();
            }
        }

        private void FilterTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            FilterCoverTextBlock.Visibility = Visibility.Hidden;
            if (!filterApplied)
            {
                CollectionView cv = (CollectionView)CollectionViewSource.GetDefaultView(ListView.ItemsSource);
                if (cv != null)
                {
                    cv.Filter = Filter;
                    filterApplied = true;
                }
            }
        }

        private void FilterTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(((TextBox)sender).Text))
                FilterCoverTextBlock.Visibility = Visibility.Visible;
        }

        private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            CollectionViewSource.GetDefaultView(ListView.ItemsSource).Refresh();
        }

        private void ListViewItem_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Window1 mv = (Window1)Window.GetWindow(this);
            mv.ProcessChatCommand(this, new CommandEventArgs(Command.Perform, ((PerformanceListViewItem)((ListViewItem)sender).Content).RelativePath));
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            Window1 mv = (Window1)Window.GetWindow(this);
            mv.ProcessChatCommand(this, new CommandEventArgs(Command.PerformStop, string.Empty));
        }
    }
}
