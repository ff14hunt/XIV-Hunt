using FFXIV_GameSense.Properties;
using System.ComponentModel;
using XIVDB;

namespace FFXIV_GameSense.UI
{
    public class FATEListViewItem : INotifyPropertyChanged
    {
        public ushort ID { get; private set; }
        public byte ClassJobLevel => GameResources.GetFATEInfo(ID).ClassJobLevel;
        public string Icon => FFXIVHunts.baseUrl + "images/" + GameResources.GetFATEInfo(ID).IconMap;
        public string Name => GameResources.GetFATEInfo(ID).NoTags;
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
                    {
                        Settings.Default.FATEs.Remove(ID.ToString());
                        OnPropertyChanged(nameof(Announce));
                    }
                else if(!Settings.Default.FATEs.Contains(ID.ToString()))
                {
                    Settings.Default.FATEs.Add(ID.ToString());
                    OnPropertyChanged(nameof(Announce));
                }
            }
        }

        public FATEListViewItem(FATE f)
        {
            ID = f.ID;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string prop) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
