using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace FFXIV_GameSense.UI
{
    /// <summary>
    /// Interaction logic for FATEListView.xaml
    /// </summary>
    public partial class FATEsListView : UserControl
    {
        private GridViewColumnHeader _lastHeaderClicked = null;
        private ListSortDirection _lastDirection = ListSortDirection.Ascending;
        bool filterApplied = false;

        public FATEsListView()
        {
            InitializeComponent();
        }

        private bool Filter(object obj)
        {
            if (string.IsNullOrWhiteSpace(FilterTextBox.Text))
                return true;
            else
            {
                FATEListViewItem item = (FATEListViewItem)obj;
                return (item.Name.IndexOf(FilterTextBox.Text, StringComparison.CurrentCultureIgnoreCase) >= 0)
                    || (item.Zones.IndexOf(FilterTextBox.Text, StringComparison.CurrentCultureIgnoreCase) >= 0)
                    || (item.ID.ToString() == FilterTextBox.Text);
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
            dataView.SortDescriptions.Clear();
            SortDescription sd = new SortDescription(sortBy, direction);
            dataView.SortDescriptions.Add(sd);
            dataView.Refresh();
        }

        private void FilterTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            FilterCoverTextBlock.Visibility = Visibility.Hidden;
            if (!filterApplied)
            {
                CollectionView cv = (CollectionView)CollectionViewSource.GetDefaultView(ListView.ItemsSource);
                cv.Filter = Filter;
                filterApplied = true;
            }
        }

        private void FilterTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if(string.IsNullOrWhiteSpace(((TextBox)sender).Text))
                FilterCoverTextBlock.Visibility = Visibility.Visible;
        }

        private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            CollectionViewSource.GetDefaultView(ListView.ItemsSource).Refresh();
        }

        public void AutoSizeZonesColumn()
        {
            foreach (var c in ((GridView)ListView.View).Columns)
                c.Width = c.ActualWidth;
        }
    }
}
