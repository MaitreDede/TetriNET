﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using TetriNET.Common.GameDatas;
using TetriNET.Common.Interfaces;
using TetriNET.WPF_WCF_Client.Annotations;
using TetriNET.WPF_WCF_Client.Helpers;

namespace TetriNET.WPF_WCF_Client.Views
{
    /// <summary>
    /// Interaction logic for WinListView.xaml
    /// </summary>
    public partial class WinListView : UserControl, INotifyPropertyChanged
    {
        public static readonly DependencyProperty ClientProperty = DependencyProperty.Register("WinListViewClientProperty", typeof(IClient), typeof(WinListView), new PropertyMetadata(Client_Changed));
        public IClient Client
        {
            get { return (IClient)GetValue(ClientProperty); }
            set { SetValue(ClientProperty, value); }
        }

        private bool _isResetEnabled;
        public bool IsResetEnabled
        {
            get { return _isResetEnabled; }
            set
            {
                if (_isResetEnabled != value)
                {
                    _isResetEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        private readonly ObservableCollection<WinEntry> _winList = new ObservableCollection<WinEntry>();
        public ObservableCollection<WinEntry> WinList
        {
            get { return _winList; }
        }

        public WinListView()
        {
            InitializeComponent();
        }

        public void UpdateWinList(List<WinEntry> winList)
        {
            _winList.Clear();
            foreach(WinEntry entry in winList.OrderByDescending(x => x.Score))
                _winList.Add(entry);
        }

        private static void Client_Changed(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            WinListView _this = sender as WinListView;

            if (_this != null)
            {
                // Remove old handlers
                IClient oldClient = args.OldValue as IClient;
                if (oldClient != null)
                {
                    oldClient.OnServerMasterModified -= _this.OnServerMasterModified;
                    oldClient.OnWinListModified -= _this.OnWinListModified;
                }
                // Set new client
                IClient newClient = args.NewValue as IClient;
                _this.Client = newClient;
                // Add new handlers
                if (newClient != null)
                {
                    newClient.OnServerMasterModified += _this.OnServerMasterModified;
                    newClient.OnWinListModified += _this.OnWinListModified;
                }
            }
        }

        private void OnWinListModified(List<WinEntry> winList)
        {
            ExecuteOnUIThread.Invoke(() => UpdateWinList(winList));
        }

        private void OnServerMasterModified(int serverMasterId)
        {
            IsResetEnabled = Client.IsServerMaster;
        }

        private void ResetWinList_OnClick(object sender, RoutedEventArgs e)
        {
            Client.ResetWinList();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}