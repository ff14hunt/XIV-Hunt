using FFXIV_GameSense.Properties;
using System.ComponentModel;

namespace FFXIV_GameSense.UI
{
    public class FATEListViewItem : INotifyPropertyChanged
    {
        public ushort ID { get; private set; }
        public byte ClassJobLevel { get; private set; }
        public string Icon { get; private set; }
        public string Name { get; private set; }
        private string zones;
        public string Zones
        {
            get { return zones ?? string.Empty; }
            set
            {
                if (zones != value)
                {
                    zones = value;
                    OnPropertyChanged(nameof(Zones));
                }
            }
        }
        public bool Announce
        {
            get
            {
                return Settings.Default.FATEs.Contains(ID.ToString());
            }
            set
            {
                if (!value)
                    while (Settings.Default.FATEs.Contains(ID.ToString()))
                        Settings.Default.FATEs.Remove(ID.ToString());
                else
                    if(!Settings.Default.FATEs.Contains(ID.ToString()))
                        Settings.Default.FATEs.Add(ID.ToString());
            }
        }

        public FATEListViewItem(FATE f)
        {
            ID = f.ID;
            Name = f.Name(true);
            ClassJobLevel = f.FATEInfo.ClassJobLevel;
            Icon = FFXIVHunts.baseUrl + "images/" + f.FATEInfo.IconMap;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string prop) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
