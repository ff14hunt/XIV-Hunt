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
            { "PC", icondir+"060443.tex.png"},
            { "NPC", "/Resources/Images/NPC.png" },
            { "Monster", "/Resources/enemy.ico" },
            { "Treasure", icondir+"060356.tex.png" },
            { "Silver", icondir+"060355.tex.png" },
            { "Gold", icondir+"060354.tex.png" },
            { "Aetheryte", icondir+"060453.tex.png" },
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

        public EntityOverlayControl(Combatant c)
        {
            InitializeComponent();
            Model = new EntityOverlayControlViewModel
            {
                Name = c.Name,
                Icon = GetIcon(c)
            };
            DataContext = Model;
        }

        public String GetName() => Model.Name;

        private void SetNameColor(Brush brush) => Model.NameColor = brush;

        private void RotateImage(float angle)
        {
            RotateTransform rotateTransform = new RotateTransform(angle);
            image.RenderTransform = rotateTransform;
        }

        public void Update(Combatant c)
        {
            Model.Name = !string.IsNullOrWhiteSpace(c.Name) ? c.Name : c.Type.ToString() + " No Name";
            if (c.Type == ObjectType.PC)
            {
                RotateImage(-c.HeadingDegree);
            }
            else if(c.Type==ObjectType.EventObject)
            {
                Model.Icon = GetIcon(c);
                if (string.IsNullOrWhiteSpace(c.Name) && c.EventType == EventType.Hoard)
                    Model.Name = "Hoard!";
            }
            else if (c.Type == ObjectType.Monster && c.CurrentHP == 0)
                Visibility = Visibility.Hidden;
        }

        private string GetIcon(Combatant c)
        {
            if (c.Type == ObjectType.PC)
                return IconUris[ObjectType.PC.ToString()];
            if (c.Type == ObjectType.Monster)
                return IconUris[ObjectType.Monster.ToString()];
            if (c.Type==ObjectType.Aetheryte)
                return IconUris[ObjectType.Aetheryte.ToString()];
            if (c.Type == ObjectType.Treasure || c.EventType==EventType.BronzeTrap)
                return IconUris[ObjectType.Treasure.ToString()];
            if(c.Type==ObjectType.EventObject)
            {
                if (c.EventType == EventType.CairnOfPassage)
                    return IconUris[EventType.CairnOfPassage.ToString() + (c.CairnIsUnlocked ? "Unlocked" : string.Empty)];
                if (c.EventType == EventType.CairnOfReturn)
                    return IconUris[EventType.CairnOfReturn.ToString() + (c.CairnIsUnlocked ? "Unlocked" : string.Empty)];
                if (c.EventType == EventType.Silver)
                    return IconUris[EventType.Silver.ToString()];
                if (c.EventType == EventType.Gold)
                    return IconUris[EventType.Gold.ToString()];
                if (c.EventType == EventType.Banded || c.EventType == EventType.Hoard)
                    return IconUris[EventType.Banded.ToString()];
            }
            if (c.Type == ObjectType.NPC)
                return IconUris[ObjectType.NPC.ToString()];
            return string.Empty;
        }
    }

    public class EntityOverlayControlViewModel : INotifyPropertyChanged//TODO: Fody.PropertyChanged instead?
    {
        private string icon = EntityOverlayControl.IconUris["PC"];
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
