using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FFXIV_GameSense.Overlay
{
    /// <summary>
    /// Interaction logic for EntityOverlayControl.xaml
    /// </summary>
    public partial class EntityOverlayControl : UserControl
    {
        private const string icondir = @"/Resources/Images/ui/icon/060000/";
        internal static readonly Dictionary<string, string> IconUris = new Dictionary<string, string>
        {
            { typeof(PC).Name, icondir+"060443.tex.png"},
            { typeof(NPC).Name, "/Resources/Images/NPC.png" },
            { typeof(Monster).Name, "/Resources/enemy.ico" },
            { typeof(Treasure).Name, icondir+"060356.tex.png" },
            { "Silver", icondir+"060355.tex.png" },
            { "Gold", icondir+"060354.tex.png" },
            { typeof(Aetheryte).Name, icondir+"060453.tex.png" },
            { "CairnOfReturn", icondir+"060905.tex.png" },
            { "CairnOfReturnUnlocked", icondir+"060906.tex.png" },
            { "CairnOfPassage", icondir+"060907.tex.png" },
            { "CairnOfPassageUnlocked", icondir+"060908.tex.png" },
            { "Banded", "/Resources/Images/Banded.png" }
        };
        private EntityOverlayControlViewModel Model { get; set; }

        public EntityOverlayControl()
        {
            InitializeComponent();
        }

        public EntityOverlayControl(Entity c, bool IsSelf = false)
        {
            InitializeComponent();
            Model = new EntityOverlayControlViewModel
            {
                NameColor = GetColor(c, IsSelf),
                Name = c.Name,
                Icon = GetIcon(c)
            };
            DataContext = Model;
        }

        private Brush GetColor(Entity c, bool IsSelf)
        {
            if(c is PC)
                return new SolidColorBrush(IsSelf ? Colors.LightGreen : Colors.LightBlue);
            if(c is Monster)
            {
                if(Hunt.TryGetHuntRank(((Monster)c).BNpcNameID, out HuntRank hr))
                {
                    return new SolidColorBrush(hr == HuntRank.B ? Color.FromArgb(255,0,0,0xE7) : Colors.Red);
                }
                return new SolidColorBrush(Colors.White);
            }
            if (string.IsNullOrWhiteSpace(c.Name))
                return new SolidColorBrush(Colors.MediumPurple);
            return new SolidColorBrush(Colors.LightGray);
        }

        public string GetName() => Model.Name;

        private void SetNameColor(Brush brush) => Model.NameColor = brush;

        private void RotateImage(float angle)
        {
            RotateTransform rotateTransform = new RotateTransform(angle);
            image.RenderTransform = rotateTransform;
        }

        public void Update(Entity c)
        {
            Model.Name = !string.IsNullOrWhiteSpace(c.Name) ? c.Name : c.GetType().Name + " No Name";
            if (c is PC)
            {
                RotateImage(-c.HeadingDegree);
            }
            else if(c is EObject)
            {
                Model.Icon = GetIcon(c);
                if (string.IsNullOrWhiteSpace(c.Name) && ((EObject)c).SubType == EObjType.Hoard)
                    Model.Name = "Hoard!";
            }
            else if (c is Combatant && ((Combatant)c).CurrentHP == 0)
                Visibility = Visibility.Hidden;
        }

        private string GetIcon(Entity c)
        {
            if (c is PC)
                return IconUris[typeof(PC).Name];
            if (c is Monster)
                return IconUris[typeof(Monster).Name];
            if (c is Aetheryte)
                return IconUris[typeof(Aetheryte).Name];
            if (c is Treasure || (c is EObject && ((EObject)c).SubType == EObjType.BronzeTrap))
                return IconUris[typeof(Treasure).Name];
            if(c is EObject)
            {
                if (((EObject)c).SubType == EObjType.CairnOfPassage || ((EObject)c).SubType==EObjType.BeaconOfPassage)
                    return IconUris[EObjType.CairnOfPassage.ToString() + (((EObject)c).CairnIsUnlocked ? "Unlocked" : string.Empty)];
                if (((EObject)c).SubType == EObjType.CairnOfReturn || ((EObject)c).SubType == EObjType.BeaconOfReturn)
                    return IconUris[EObjType.CairnOfReturn.ToString() + (((EObject)c).CairnIsUnlocked ? "Unlocked" : string.Empty)];
                if (((EObject)c).SubType == EObjType.Silver)
                    return IconUris[EObjType.Silver.ToString()];
                if (((EObject)c).SubType == EObjType.Gold)
                    return IconUris[EObjType.Gold.ToString()];
                if (((EObject)c).SubType == EObjType.Banded || ((EObject)c).SubType == EObjType.Hoard)
                    return IconUris[EObjType.Banded.ToString()];
            }
            if (c is NPC)
                return IconUris[typeof(NPC).Name];
            return @"/Resources/Images/ui/uld/image2.tex.png";
        }
    }

    public class EntityOverlayControlViewModel : INotifyPropertyChanged
    {
        private string icon;
        public string Icon
        {
            get
            {
                return icon;
            }
            set
            {
                if (icon != value)
                {
                    icon = value;
                    OnPropertyChanged(nameof(Icon));
                }
            }
        }
        private string name;
        public string Name
        {
            get
            {
                return name ?? string.Empty;
            }
            set
            {
                if (name != value)
                {
                    name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        private Brush nameColor = new SolidColorBrush(Colors.Black);
        public Brush NameColor
        {
            get
            {
                return nameColor;
            }
            set
            {
                if (nameColor != value)
                {
                    nameColor = value;
                    OnPropertyChanged(nameof(NameColor));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string prop) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
