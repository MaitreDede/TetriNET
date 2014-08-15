﻿using System;
using System.Configuration;
using System.Diagnostics;
using System.Reflection;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using TetriNET.Common.Logger;
using TetriNET.Common.Randomizer;
using TetriNET.Server.BanManager;
using TetriNET.Server.PieceProvider;
using TetriNET.Server.PlayerManager;
using TetriNET.Server.SpectatorManager;

namespace TetriNET.WCF.Service
{
    public partial class Service : ServiceBase
    {
        private const string EventLogSource = "TetriNET";
        private const string EventLogName = "TetriNET";

        private Task _mainLoopTask;
        private CancellationTokenSource _cancellationTokenSource;

        public Service()
        {
            InitializeComponent();

            //An event log source should not be created and immediately used. There is a latency time to enable the source.
            if (!EventLog.SourceExists(EventLogSource))
            {
                EventLog.CreateEventSource(EventLogSource, EventLogName);
                while(true)
                {
                    if (EventLog.SourceExists(EventLogSource))
                        break;

                    Thread.Sleep(250);
                }
            }
            //
            eventLog = new EventLog
                {
                    Source = EventLogSource,
                };
            //
            Log.Initialize(ConfigurationManager.AppSettings["logpath"], "serverservice.log");
        }

        protected override void OnStart(string[] args)
        {
            Version version = Assembly.GetEntryAssembly().GetName().Version;
            string company = ((AssemblyCompanyAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyCompanyAttribute), false)).Company;
            string product = ((AssemblyProductAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyProductAttribute), false)).Product;
            Log.WriteLine(Log.LogLevels.Info, "{0} {1}.{2} by {3}", product, version.Major, version.Minor, company);
            Log.WriteLine(Log.LogLevels.Info, "Starting");

            _cancellationTokenSource = new CancellationTokenSource();
            _mainLoopTask = Task.Factory.StartNew(ServiceMainLoop, _cancellationTokenSource.Token);
        }

        protected override void OnStop()
        {
            Log.WriteLine(Log.LogLevels.Info, "Stopping");

            _cancellationTokenSource.Cancel();

            _mainLoopTask.Wait(1000); // Wait max 1 second
        }

        private void ServiceMainLoop()
        {
            //
            BanManager banManager = new BanManager();
            //
            PlayerManager playerManager = new PlayerManager(6);
            SpectatorManager spectatorManager = new SpectatorManager(10);

            //
            Server.WCFHost.WCFHost wcfHost = new Server.WCFHost.WCFHost(
                playerManager,
                spectatorManager,
                banManager,
                (playerName, callback) => new Player(playerName, callback),
                (spectatorName, callback) => new Spectator(spectatorName, callback))
            {
                Port = ConfigurationManager.AppSettings["port"]
            };

            //
            PieceBag pieceProvider = new PieceBag(RangeRandom.Random, 4);

            //
            Server.Server server = new Server.Server(playerManager, spectatorManager, pieceProvider, wcfHost);

            //
            server.StartServer();

            while(!_cancellationTokenSource.IsCancellationRequested)
                Thread.Sleep(250);

            server.StopServer();
        }
    }
}