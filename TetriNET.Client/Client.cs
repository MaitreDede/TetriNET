﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using TetriNET.Common;
using TetriNET.Common.Contracts;
using TetriNET.Common.Interfaces;

namespace TetriNET.Client
{
    public class Player
    {
        public string Name { get; set; }
        public byte[] Grid { get; set; }
    }

    public class TetriminoArray
    {
        private readonly object _lock = new object();
        private int _size;
        private Tetriminos[] _array;

        public TetriminoArray(int size)
        {
            _size = size;
            _array = new Tetriminos[_size];
        }

        public Tetriminos this[int index]
        {
            get
            {
                Tetriminos tetrimino;
                lock (_lock)
                {
                    tetrimino = _array[index];
                }
                return tetrimino;
            }
            set
            {
                lock (_lock)
                {
                    if (index >= _size)
                        Grow(64);
                    _array[index] = value;
                }
            }
        }

        private void Grow(int increment)
        {
            int newSize = _size + increment;
            Tetriminos[] newArray = new Tetriminos[newSize];
            if (_size > 0)
                Array.Copy(_array, newArray, _size);
            _array = newArray;
            _size = newSize;
        }

        public string Dump(int size)
        {
            return _array.Take(size).Select((t, i) => "[" + i.ToString(CultureInfo.InvariantCulture) + ":" + t.ToString() + "]").Aggregate((s, t) => s + "," + t);
        }
    }

    public sealed class Client : ITetriNETCallback, IClient
    {
        private const int HeartbeatDelay = 300; // in ms
        private const int TimeoutDelay = 500; // in ms
        private const int MaxTimeoutCountBeforeDisconnection = 3;
        private const int TetriminosCount = 7;

        public enum States
        {
            Created, // -> Registering
            Registering, // --> Registered
            Registered, // --> Playing
            Playing, // --> Paused | Registered
            Paused // --> Playing | Registered
        }

        private readonly IProxy _proxy;
        private readonly Random _random;
        private readonly Func<Tetriminos, int, int, ITetrimino>  _createTetriminoFunc;
        private readonly Player[] _players = new Player[6];
        private readonly ManualResetEvent _stopBackgroundTaskEvent = new ManualResetEvent(false);
        private readonly TetriminoArray _tetriminos;
        private readonly System.Timers.Timer _gameTimer;

        public States State { get; private set; }
        public ITetrimino CurrentTetrimino { get; private set; }
        public ITetrimino NextTetrimino { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }

        private List<WinEntry> _winList;
        private GameOptions _options;
        private int _playerId;
        private bool _isServerMaster;

        private DateTime _lastActionFromServer;
        private int _timeoutCount;

        private int _tetriminoIndex;
        private int _lineCount;
        private int _level;

        public byte[] Grid
        {
            get { return Player.Grid; }
        }

        private Player Player
        {
            get { return _players[_playerId]; }
        }

        public string Name { get; private set; }

        public Client(Func<ITetriNETCallback, IProxy> createProxyFunc, Func<Tetriminos, int, int, ITetrimino> createTetrimino)
        {
            if (createProxyFunc == null)
                throw new ArgumentNullException("createProxyFunc");
            if (createTetrimino == null)
                throw new ArgumentNullException("createTetrimino");

            _proxy = createProxyFunc(this);
            _proxy.OnConnectionLost += ConnectionLostHandler;

            _createTetriminoFunc = createTetrimino;

            _random = new Random();

            _tetriminos = new TetriminoArray(64);

            _gameTimer = new System.Timers.Timer();
            _gameTimer.Interval = 200; // TODO: use a function to compute interval in function of level
            _gameTimer.Elapsed += GameTimerOnElapsed;

            _lastActionFromServer = DateTime.Now;
            _timeoutCount = 0;

            _playerId = -1;
            _isServerMaster = false;

            Width = 12;
            Height = 22;

            State = States.Created;

            Task.Factory.StartNew(TimeoutTask);
        }

        public void Dump()
        {
            // Players
            for (int i = 0; i < 6; i++)
            {
                Player p = _players[i];
                Log.WriteLine("{0}{1}: {2}", i, i == _playerId ? "*" : String.Empty, p == null ? String.Empty : p.Name);
            }
            // Grid
            if (_playerId >= 0 && State == States.Playing)
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < Width*Height; i++)
                {
                    int x = i%Width;
                    int y = i/Width;
                    bool foundPart = false;
                    if (CurrentTetrimino != null)
                        if (x >= CurrentTetrimino.PosX && x < CurrentTetrimino.PosX + CurrentTetrimino.Width && y >= CurrentTetrimino.PosY && y < CurrentTetrimino.PosY + CurrentTetrimino.Height)
                        {
                            int partX = x - CurrentTetrimino.PosX;
                            int partY = y - CurrentTetrimino.PosY;
                            int linearPartPos = partY * CurrentTetrimino.Width + partX;
                            if (CurrentTetrimino.Parts[linearPartPos] > 0)
                            {
                                sb.Append(CurrentTetrimino.Parts[linearPartPos]);
                                foundPart = true;
                            }
                        }
                    if (!foundPart)
                        sb.Append(Grid[i]);
                    if ((i + 1)%Width == 0)
                    {
                        Log.WriteLine(sb.ToString());
                        sb.Clear();
                    }
                }
                Log.WriteLine("========================");
            }
        }

        #region IProxy event handler

        private void ConnectionLostHandler()
        {
            throw new NotImplementedException();
        }

        #endregion

        #region ITetriNETCallback

        public void OnHeartbeatReceived()
        {
            ResetTimeout();
        }

        public void OnServerStopped()
        {
            ResetTimeout();
            ConnectionLostHandler();
        }

        public void OnPlayerRegistered(bool succeeded, int playerId, bool gameStarted)
        {
            ResetTimeout();
            if (succeeded && State == States.Registering)
            {
                Log.WriteLine("Registered as player {0} game started {1}", playerId, gameStarted);

                // TODO: if gameStarted fill our screen with random blocks

                _playerId = playerId;
                _players[_playerId] = new Player
                    {
                        Name = Name,
                        Grid = new byte[Width*Height]
                    };
                State = States.Registered;
            }
            else
            {
                Log.WriteLine("Registration failed");
            }
        }

        public void OnPlayerJoined(int playerId, string name)
        {
            Log.WriteLine("Player {0}[{1}] joined", name, playerId);

            ResetTimeout();
            if (playerId != _playerId && playerId >= 0) // don't update ourself
                _players[playerId] = new Player
                    {
                        Name = name
                    };
            // TODO: update chat list + display msg in out-game chat + fill player screen with random blocks if game started
        }

        public void OnPlayerLeft(int playerId, string name, LeaveReasons reason)
        {
            Log.WriteLine("Player {0}[{1}] left ({2})", name, playerId, reason);

            ResetTimeout();
            if (playerId != _playerId && playerId >= 0)
                _players[playerId] = null;
            // TODO: update chat list + display msg in out-game chat + fill player screen with random blocks if game started
        }

        public void OnPublishPlayerMessage(string playerName, string msg)
        {
            Log.WriteLine("{0}:{1}", playerName, msg);

            ResetTimeout();
            // TODO: display msg in out-game chat
        }

        public void OnPublishServerMessage(string msg)
        {
            Log.WriteLine("{0}", msg);

            ResetTimeout();
            // TODO: display msg in out-game chat
        }

        public void OnPlayerLost(int playerId)
        {
            Log.WriteLine("Player [{0}] {1} has lost", playerId, _players[playerId].Name);

            ResetTimeout();
            // TODO: fill player screen with random blocks + display msg in in-game chat + update player status
        }

        public void OnPlayerWon(int playerId)
        {
            Log.WriteLine("Player [{0}] {1} has won", playerId, _players[playerId].Name);

            ResetTimeout();
            // TODO: display msg in in-game chat + out-game chat + update player status
        }

        public void OnGameStarted(Tetriminos firstTetrimino, Tetriminos secondTetrimino, Tetriminos thirdTetrimino, GameOptions options)
        {
            Log.WriteLine("Game started with {0} {1} {2}", firstTetrimino, secondTetrimino, thirdTetrimino);

            ResetTimeout();
            State = States.Playing;
            _options = options;
            //
            _tetriminos[0] = firstTetrimino;
            _tetriminos[1] = secondTetrimino;
            _tetriminos[2] = thirdTetrimino;
            _tetriminoIndex = 0;
            CurrentTetrimino = _createTetriminoFunc(firstTetrimino, Width, Height);
            NextTetrimino = _createTetriminoFunc(secondTetrimino, Width, Height);
            //
            _lineCount = 0;
            _level = 0;
            // Reset grid
            for (int i = 0; i < Width*Height; i++)
                Grid[i] = 0;
            // Restart timer
            _gameTimer.Start();

            if (ClientOnGameStarted != null)
                ClientOnGameStarted();

            //Log.WriteLine("TETRIMINOS:{0}", _tetriminos.Dump(8));
            // TODO: update current/next piece, clear every player screen
        }

        public void OnGameFinished()
        {
            Log.WriteLine("Game finished");

            ResetTimeout();
            State = States.Registered;
            _gameTimer.Stop();
            if (ClientOnGameFinished != null)
                ClientOnGameFinished();
            // TODO:
        }

        public void OnGamePaused()
        {
            Log.WriteLine("Game paused");

            ResetTimeout();
            State = States.Paused;
            // TODO
            if (ClientOnGamePaused != null)
                ClientOnGamePaused();
        }

        public void OnGameResumed()
        {
            Log.WriteLine("Game resumed");

            ResetTimeout();
            State = States.Playing;
            // TODO
            if (ClientOnGameResumed != null)
                ClientOnGameResumed();
        }

        public void OnServerAddLines(int lineCount)
        {
            Log.WriteLine("Server add {0} lines", lineCount);

            ResetTimeout();
            // TODO
        }

        public void OnPlayerAddLines(int specialId, int playerId, int lineCount)
        {
            Log.WriteLine("Player {0} add {1} lines (special [{2}])", playerId, lineCount, specialId);

            ResetTimeout();
            // TODO: perform attack
        }

        public void OnSpecialUsed(int specialId, int playerId, int targetId, Specials special)
        {
            Log.WriteLine("Special {0}[{1}] from {2} to {3}", special, specialId, playerId, targetId);

            ResetTimeout();
            // TODO: if targetId == own id, perform attack + display in-game msg
        }

        public void OnNextTetrimino(int index, Tetriminos tetrimino)
        {
            ResetTimeout();
            _tetriminos[index] = tetrimino;
        }

        public void OnGridModified(int playerId, byte[] grid)
        {
            Log.WriteLine("Player [{0}] {1} modified", playerId, _players[playerId].Name);

            ResetTimeout();
            if (_players[playerId] != null)
                _players[playerId].Grid = grid;
            // TODO: update player screen
        }

        public void OnServerMasterChanged(int playerId)
        {
            ResetTimeout();
            if (playerId == _playerId)
            {
                Log.WriteLine("Yeehaw ... power is ours");

                _isServerMaster = true;
                // TODO: enable server settings, start/stop/pause/resume buttons, ...
            }
            else
            {
                Log.WriteLine("The power is for another one");

                _isServerMaster = false;
                // TODO: disable server settings, start/stop/pause/resume buttons, ...
            }
        }

        public void OnWinListModified(List<WinEntry> winList)
        {
            Log.WriteLine("Win list: {0}", winList.Select(x => String.Format("{0}:{1}", x.PlayerName, x.Score)).Aggregate((n, i) => n + "|" + i));

            ResetTimeout();
            _winList = winList;
            // TODO: update win list
        }

        #endregion

        public void ResetTimeout()
        {
            _timeoutCount = 0;
            _lastActionFromServer = DateTime.Now;
        }

        public void SetTimeout()
        {
            _timeoutCount++;
            _lastActionFromServer = DateTime.Now;
        }

        private void PlaceCurrentTetrimino()
        {
            // Place tetrimino in grid
            for (int i = 0; i < CurrentTetrimino.Width * CurrentTetrimino.Height; i++)
                if (CurrentTetrimino.Parts[i] > 0)
                {
                    int linearGridCoordinate = CurrentTetrimino.LinearPosInGrid(i);
                    Grid[linearGridCoordinate] = CurrentTetrimino.Parts[i];
                }
        }

        private void EndGame()
        {
            Log.WriteLine("End game");
            _gameTimer.Stop();
            // Send player lost
            _proxy.GameLost(this);

            if (ClientOnGameOver != null)
                ClientOnGameOver();
        }

        private void FinishRound()
        {
            //
            //Log.WriteLine("Round finished with tetrimino {0} {1}  next {2}", CurrentTetrimino.TetriminoValue, _tetriminoIndex, NextTetrimino.TetriminoValue);
            // Stop game
            _gameTimer.Stop();
            //Log.WriteLine("TETRIMINOS:{0}", _tetriminos.Dump(8));
            // Delete rows
            int deletedRows = DeleteRows();
            if (deletedRows > 0)
                Log.WriteLine("{0} lines deleted", deletedRows);
            _lineCount += deletedRows;
            // Check level increase
            if (_level < _lineCount/10)
            {
                _level = _lineCount/10;
                Log.WriteLine("Level increased: {0}", _level);
                // TODO: change game timer interval
            }
            // Send tetrimino places to server
            _proxy.PlaceTetrimino(this, _tetriminoIndex, CurrentTetrimino.TetriminoValue, Orientations.Top/*TODO*/, null/*TODO*/, Grid);
            // Set new current tetrimino to next, increment tetrimino index and create next tetrimino
            CurrentTetrimino = NextTetrimino;
            _tetriminoIndex++;
            NextTetrimino = _createTetriminoFunc(_tetriminos[_tetriminoIndex + 1], Width, Height);
            //Log.WriteLine("New tetrimino {0} {1}  next {2}", CurrentTetrimino.TetriminoValue, _tetriminoIndex, NextTetrimino.TetriminoValue);
            // Check game over (if current tetrimino has conflict)
            if (CurrentTetrimino.CheckConflict(Grid))
                EndGame();
            else
            {
                // TODO: Add special blocks depending on deleted rows and options

                // Inform UI
                if (ClientOnRedraw != null)
                    ClientOnRedraw();

                // TODO: Send lines if classic style
                _gameTimer.Start();

                if (ClientOnTetriminoPlaced != null)
                    ClientOnTetriminoPlaced();
            }
        }

        private int DeleteRows()
        {
            int rows = 0;
            for (int y = 1; y < Height; y++)
            {
                // Count number of part in row
                int countPart = 0;
                for (int x = 0; x < Width; x++)
                {
                    int linearIndex = y * Width + x;
                    countPart += Grid[linearIndex] > 0 ? 1 : 0;
                }
                // Full row, delete it and move part above one step below
                if (countPart == Width)
                {
                    rows++;
                    for (int yMove = y; yMove > 0; yMove--)
                        for (int x = 0; x < Width; x++)
                        {
                            int linearIndexTo = yMove * Width + x;
                            int linearIndexFrom = (yMove - 1) * Width + x;
                            Grid[linearIndexTo] = Grid[linearIndexFrom];
                        }
                }
            }
            return rows;
        }

        private void GameTimerOnElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            if (State == States.Playing)
            {
                MoveDown();
            }
        }

        #region Specials

        // Add junk lines
        public void AddLines(int count)
        {
            if (count <= 0)
                return;
            // Move grid up
            for (int i = 0; i < count; i++)
            {
                for (int y = 0; y < Height - count; y++)
                    for (int x = 0; x < Width; x++)
                    {
                        int to = y*Width + x;
                        int from = to + (count*Width);
                        Grid[to] = Grid[from];
                    }
            }
            // Add junk lines at the bottom
            for (int i = 0; i < count; i++) 
            {
                int hole = _random.Next(Width);
                for (int x = 0; x < Width; x++)
                {
                    int linear = (Height-1)*Width + x;
                    //byte part = (byte)(_random.Next()%TetriminosCount);
                    byte part;
                    if (x == hole)
                        part = 0;
                    else
                        part = (byte)(1 + (_random.Next() % TetriminosCount));
                    Grid[linear] = part;
                }
            }
        }

        public void ClearLine()
        {
            throw new NotImplementedException();
        }

        public void NukeField()
        {
            throw new NotImplementedException();
        }

        public void RandomBlocksClear(int count)
        {
            throw new NotImplementedException();
        }

        public void ClearSpecialBlocks()
        {
            throw new NotImplementedException();
        }

        public void Gravity()
        {
            throw new NotImplementedException();
        }

        public void Quake()
        {
            throw new NotImplementedException();
        }

        public void BlockBomb()
        {
            throw new NotImplementedException();
        }

        // switch is handled by server
        #endregion

        #region IClient

        public bool IsGamePaused
        {
            get
            {
                return State == States.Paused;
            }
        }

        public bool IsGameStarted
        {
            get
            {
                return State == States.Playing;
            }
        }

        private event ClientTetriminoPlacedHandler ClientOnTetriminoPlaced;
        event ClientTetriminoPlacedHandler IClient.OnTetriminoPlaced
        {
            add { ClientOnTetriminoPlaced += value; }
            remove { ClientOnTetriminoPlaced -= value; }
        }


        private event ClientStartGameHandler ClientOnGameStarted;
        event ClientStartGameHandler IClient.OnGameStarted
        {
            add { ClientOnGameStarted += value; }
            remove { ClientOnGameStarted -= value; }
        }

        private event ClientFinishGameHandler ClientOnGameFinished;
        event ClientFinishGameHandler IClient.OnGameFinished
        {
            add { ClientOnGameFinished += value; }
            remove { ClientOnGameFinished -= value; }
        }

        private event ClientGameOverHandler ClientOnGameOver;
        event ClientGameOverHandler IClient.OnGameOver
        {
            add { ClientOnGameOver += value; }
            remove { ClientOnGameOver -= value; }
        }

        private event ClientPauseGameHandler ClientOnGamePaused;
        event ClientPauseGameHandler IClient.OnGamePaused
        {
            add { ClientOnGamePaused += value; }
            remove { ClientOnGamePaused -= value; }
        }

        private event ClientResumeGameHandler ClientOnGameResumed;
        event ClientResumeGameHandler IClient.OnGameResumed
        {
            add { ClientOnGameResumed += value; }
            remove { ClientOnGameResumed -= value; }
        }

        private event ClientRedrawHandler ClientOnRedraw;
        event ClientRedrawHandler IClient.OnRedraw
        {
            add { ClientOnRedraw += value; }
            remove { ClientOnRedraw -= value; }
        }

        public void Register(string name)
        {
            State = States.Registering;
            Name = name;
            _proxy.RegisterPlayer(this, Name);
        }

        public void Drop()
        {
            // TODO
            if (State != States.Playing)
                return;
            _gameTimer.Stop();
            // TODO: remove current tetrimino from UI
            // Perform move
            while (true)
            {
                bool movedDown = CurrentTetrimino.MoveDown(Grid);
                if (!movedDown)
                    break;
            }
            // TODO: add current tetrimino in UI
            //
            PlaceCurrentTetrimino();
            //
            FinishRound();
        }

        public void MoveDown()
        {
            if (State != States.Playing)
                return;
            // TODO: remove current tetrimino from UI
            bool movedDown = CurrentTetrimino.MoveDown(Grid);
            // TODO: add current tetrimino in UI

            if (!movedDown)
            {
                //
                PlaceCurrentTetrimino();
                //
                FinishRound();
            }

            // Inform UI
            if (ClientOnRedraw != null)
                ClientOnRedraw();
        }

        public void MoveLeft()
        {
            if (State != States.Playing)
                return;
            // TODO: remove current tetrimino from UI
            CurrentTetrimino.MoveLeft(Grid);
            // TODO: add current tetrimino in UI

            // Inform UI
            if (ClientOnRedraw != null)
                ClientOnRedraw();

        }

        public void MoveRight()
        {
            if (State != States.Playing)
                return;
            // TODO: remove current tetrimino from UI
            CurrentTetrimino.MoveRight(Grid);
            // TODO: add current tetrimino in UI

            // Inform UI
            if (ClientOnRedraw != null)
                ClientOnRedraw();

        }

        public void RotateClockwise()
        {
            if (State != States.Playing)
                return;
            // TODO: remove current tetrimino from UI
            CurrentTetrimino.RotateClockwise(Grid);
            // TODO: add current tetrimino in UI

            // Inform UI
            if (ClientOnRedraw != null)
                ClientOnRedraw();

        }

        public void RotateCounterClockwise()
        {
            if (State != States.Playing)
                return;
            // TODO: remove current tetrimino from UI
            CurrentTetrimino.RotateCounterClockwise(Grid);
            // TODO: add current tetrimino in UI

            // Inform UI
            if (ClientOnRedraw != null)
                ClientOnRedraw();

        }

        #endregion

        private void TimeoutTask()
        {
            while (true)
            {
                if (State == States.Registered || State == States.Playing)
                {
                    // Check server timeout
                    TimeSpan timespan = DateTime.Now - _lastActionFromServer;
                    if (timespan.TotalMilliseconds > TimeoutDelay)
                    {
                        Log.WriteLine("Timeout++");
                        // Update timeout count
                        SetTimeout();
                        if (_timeoutCount >= MaxTimeoutCountBeforeDisconnection)
                            ConnectionLostHandler(); // timeout
                    }

                    // Send heartbeat if needed
                    TimeSpan delayFromPreviousHeartbeat = DateTime.Now - _proxy.LastActionToServer;
                    if (delayFromPreviousHeartbeat.TotalMilliseconds > HeartbeatDelay)
                    {
                        _proxy.Heartbeat(this);
                    }

                    // Stop task if stop event is raised
                    if (_stopBackgroundTaskEvent.WaitOne(10))
                        break;
                }
            }
        }
    }
}
