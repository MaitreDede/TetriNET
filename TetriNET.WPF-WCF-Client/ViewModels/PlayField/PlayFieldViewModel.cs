﻿using System;
using System.Timers;
using TetriNET.Common.DataContracts;
using TetriNET.Client.Interfaces;

namespace TetriNET.WPF_WCF_Client.ViewModels.PlayField
{
    public class PlayFieldViewModel : PlayFieldViewModelBase
    {
        private const int OpponentCount = 5;

        public GameInfoViewModel GameInfoViewModel { get; set; }
        public InGameChatViewModel InGameChatViewModel { get; set; }
        public PlayerViewModel PlayerViewModel { get; set; }
        public OpponentViewModel[] OpponentsViewModel { get; set; }

        private DateTime _lastAchievement;
        private readonly Timer _achievementTimer;

        private string _achievement;
        public string Achievement
        {
            get { return _achievement; }
            set { Set(() => Achievement, ref _achievement, value); }
        }

        private double _achievementOpacity;
        public double AchievementOpacity
        {
            get { return _achievementOpacity; }
            set { Set(() => AchievementOpacity, ref _achievementOpacity, value); }
        }

        public PlayFieldViewModel()
        {
            _achievementTimer = new Timer(100);
            _achievementTimer.Elapsed += (sender, args) =>
            {
                TimeSpan timeSpan = DateTime.Now - _lastAchievement;
                if (timeSpan.TotalSeconds > 5)
                {
                    AchievementOpacity -= 0.1;
                    if (AchievementOpacity <= 0)
                        _achievementTimer.Stop();
                }
            };

            GameInfoViewModel = new GameInfoViewModel();
            InGameChatViewModel = new InGameChatViewModel();
            PlayerViewModel = new PlayerViewModel();
            OpponentsViewModel = new OpponentViewModel[OpponentCount];
            for (int i = 0; i < OpponentCount; i++)
                OpponentsViewModel[i] = new OpponentViewModel();

            ClientChanged += OnClientChanged;
        }

        private OpponentViewModel GetOpponentViewModel(int playerId)
        {
            if (Client.IsSpectator)
                return null;
            if (playerId == Client.PlayerId)
                return null;
            // playerId -> id mapping rule
            // 0 1 [2] 3 4 5 -> 0 1 / 2 3 4
            // [0] 1 2 3 4 5 -> / 0 1 2 3 4 
            // 0 1 2 3 4 [5] -> 0 1 2 3 4 /
            int id;
            if (playerId < Client.PlayerId)
                id = playerId + 1;
            else
                id = playerId;
            return OpponentsViewModel[id - 1];
        }

        #region ViewModelBase

        private void OnClientChanged(IClient oldClient, IClient newClient)
        {
            GameInfoViewModel.Client = newClient;
            InGameChatViewModel.Client = newClient;
            PlayerViewModel.Client = newClient;
            foreach (OpponentViewModel opponent in OpponentsViewModel)
                opponent.Client = newClient;
        }

        public override void UnsubscribeFromClientEvents(IClient oldClient)
        {
            oldClient.PlayerJoined -= OnPlayerJoined;
            oldClient.PlayerLeft -= OnPlayerLeft;
            oldClient.AchievementEarned -= OnAchievementEarned;
        }

        public override void SubscribeToClientEvents(IClient newClient)
        {
            newClient.PlayerJoined += OnPlayerJoined;
            newClient.PlayerLeft += OnPlayerLeft;
            newClient.AchievementEarned += OnAchievementEarned;
        }

        #endregion

        #region IClient events handler
        
        private void OnAchievementEarned(IAchievement achievement, bool firstTime)
        {
            if (Client.IsSpectator)
                return;
            Achievement = achievement.Title;
            _achievementTimer.Stop();
            _lastAchievement = DateTime.Now;
            AchievementOpacity = 1;
            _achievementTimer.Start();
        }

        private void OnPlayerLeft(int playerId, string playerName, LeaveReasons reason)
        {
            if (Client.IsSpectator)
                return;
            OpponentViewModel opponent = GetOpponentViewModel(playerId);
            if (opponent != null)
            {
                opponent.PlayerId = -1;
                opponent.PlayerName = "Not playing";
                opponent.Team = "";
            }
        }

        private void OnPlayerJoined(int playerId, string playerName, string team)
        {
            if (Client.IsSpectator)
                return;

            OpponentViewModel opponent = GetOpponentViewModel(playerId);
            if (opponent != null)
            {
                opponent.PlayerId = playerId;
                opponent.PlayerName = playerName;
                opponent.Team = team;
            }
        }

        #endregion
    }

    public class PlayFieldViewModelDesignData : PlayFieldViewModel
    {
        public PlayFieldViewModelDesignData()
        {
            GameInfoViewModel = new GameInfoViewModelDesignData();
            InGameChatViewModel = new InGameChatViewModelDesignData();
        }
    }
}