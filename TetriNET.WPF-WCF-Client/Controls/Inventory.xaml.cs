﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TetriNET.Common.GameDatas;
using TetriNET.Common.Interfaces;
using TetriNET.WPF_WCF_Client.Helpers;

namespace TetriNET.WPF_WCF_Client.Controls
{
    /// <summary>
    /// Interaction logic for Inventory.xaml
    /// </summary>
    public partial class Inventory : UserControl, INotifyPropertyChanged
    {
        private const int MaxInventorySize = 15;

        // BEWARE OF DPI ... WPF only accept 96 DPI image !!!!!!!  almost 4 hours lost to figure this out
        private readonly Uri _graphicsUri = new Uri(@"D:\Oldies\TetriNET\DATA\TNETBLKS.BMP", UriKind.Absolute); // TODO: dynamic or relative

        private static readonly SolidColorBrush TransparentColor = new SolidColorBrush(Colors.Transparent);

        public static readonly DependencyProperty ClientProperty = DependencyProperty.Register("InventoryClientProperty", typeof(IClient), typeof(Inventory), new PropertyMetadata(Client_Changed));
        public IClient Client
        {
            get { return (IClient)GetValue(ClientProperty); }
            set { SetValue(ClientProperty, value); }
        }

        private readonly Dictionary<Specials, ImageBrush> _specialsBrushes = new Dictionary<Specials, ImageBrush>();
        private readonly List<Rectangle> _inventory = new List<Rectangle>();

        private string _firstSpecial;
        public string FirstSpecial
        {
            get { return _firstSpecial; }
            set
            {
                if (_firstSpecial != value)
                {
                    _firstSpecial = value;
                    OnPropertyChanged();
                }
            }
        }

        public Inventory()
        {
            InitializeComponent();

            BuildTextures(_graphicsUri, _specialsBrushes);

            for (int i = 0; i < MaxInventorySize; i++)
            {
                Rectangle rect = new Rectangle
                {
                    Width = 16,
                    Height = 16,
                    Fill = TransparentColor
                };
                _inventory.Add(rect);
                Canvas.Children.Add(rect);
                Canvas.SetLeft(rect, i*16);
                Canvas.SetTop(rect, 0);
            }

            FirstSpecial = "No Special Blocks";
        }

        private void DrawInventory()
        {
            if (Client == null)
                return;
            List<Specials> specials = Client.Inventory;
            if (specials != null && specials.Any())
            {
                for (int i = 0; i < MaxInventorySize; i++)
                    _inventory[i].Fill = TransparentColor;
                for (int i = 0; i < specials.Count; i++)
                    _inventory[i].Fill = _specialsBrushes[specials[i]];
                FirstSpecial = Mapper.MapSpecialToString(specials[0]);
            }
            else
            {
                FirstSpecial = "No Special Blocks";
            }
        }

        private static void Client_Changed(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            Inventory _this = sender as Inventory;

            if (_this != null)
            {
                // Remove old handlers
                IClient oldClient = args.OldValue as IClient;
                if (oldClient != null)
                {
                    oldClient.OnGameStarted -= _this.OnGameStarted;
                    oldClient.OnInventoryChanged -= _this.OnInventoryChanged;
                }
                // Set new client
                IClient newClient = args.NewValue as IClient;
                _this.Client = newClient;
                // Add new handlers
                if (newClient != null)
                {
                    newClient.OnGameStarted += _this.OnGameStarted;
                    newClient.OnInventoryChanged += _this.OnInventoryChanged;
                }
            }
        }

        private void OnGameStarted()
        {
            ExecuteOnUIThread.Invoke(DrawInventory);
        }

        private void OnInventoryChanged()
        {
            ExecuteOnUIThread.Invoke(DrawInventory);
        }

        private void BuildTextures(Uri graphicsUri, IDictionary<Specials, ImageBrush> specialsBrushes)
        {
            BitmapImage image = new BitmapImage(graphicsUri);
            // Specials
            //ACNRSBGQO
            specialsBrushes.Add(Specials.AddLines, new ImageBrush(image)
            {
                ViewboxUnits = BrushMappingMode.Absolute,
                Viewbox = new Rect(80, 0, 16, 16),
                Stretch = Stretch.None
            });
            specialsBrushes.Add(Specials.ClearLines, new ImageBrush(image)
            {
                ViewboxUnits = BrushMappingMode.Absolute,
                Viewbox = new Rect(96, 0, 16, 16),
                Stretch = Stretch.None
            });
            specialsBrushes.Add(Specials.NukeField, new ImageBrush(image)
            {
                ViewboxUnits = BrushMappingMode.Absolute,
                Viewbox = new Rect(112, 0, 16, 16),
                Stretch = Stretch.None
            });
            specialsBrushes.Add(Specials.RandomBlocksClear, new ImageBrush(image)
            {
                ViewboxUnits = BrushMappingMode.Absolute,
                Viewbox = new Rect(128, 0, 16, 16),
                Stretch = Stretch.None
            });
            specialsBrushes.Add(Specials.SwitchFields, new ImageBrush(image)
            {
                ViewboxUnits = BrushMappingMode.Absolute,
                Viewbox = new Rect(144, 0, 16, 16),
                Stretch = Stretch.None
            });
            specialsBrushes.Add(Specials.ClearSpecialBlocks, new ImageBrush(image)
            {
                ViewboxUnits = BrushMappingMode.Absolute,
                Viewbox = new Rect(160, 0, 16, 16),
                Stretch = Stretch.None
            });
            specialsBrushes.Add(Specials.BlockGravity, new ImageBrush(image)
            {
                ViewboxUnits = BrushMappingMode.Absolute,
                Viewbox = new Rect(176, 0, 16, 16),
                Stretch = Stretch.None
            });
            specialsBrushes.Add(Specials.BlockQuake, new ImageBrush(image)
            {
                ViewboxUnits = BrushMappingMode.Absolute,
                Viewbox = new Rect(192, 0, 16, 16),
                Stretch = Stretch.None
            });
            specialsBrushes.Add(Specials.BlockBomb, new ImageBrush(image)
            {
                ViewboxUnits = BrushMappingMode.Absolute,
                Viewbox = new Rect(208, 0, 16, 16),
                Stretch = Stretch.None
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}