﻿using System;

namespace TetriNET.Server
{
    class Program
    {
        static void Main(string[] args)
        {
            DummyTetriNETCallback localCallback = new DummyTetriNETCallback();

            PlayerManager playerManager = new PlayerManager(6);
            
            WCFHost host = new WCFHost(playerManager);
            host.LocalPlayerCallback = localCallback;

            Server server = new Server(host);

            server.StartServer();

            Console.WriteLine("Commands:");
            Console.WriteLine("x: Stop server");
            Console.WriteLine("s: Start game");
            Console.WriteLine("t: Stop game");
            Console.WriteLine("m: Send message broadcast");
            Console.WriteLine("c: Connect local player");
            Console.WriteLine("d: Disconnect local player");

            bool stopped = false;
            while (!stopped)
            {
                if (Console.KeyAvailable)
                {
                    ConsoleKeyInfo cki = Console.ReadKey(true);
                    switch (cki.Key)
                    {
                        case ConsoleKey.X:
                            stopped = true;
                            break;
                        case ConsoleKey.S:
                            server.StartGame();
                            break;
                        case ConsoleKey.T:
                            server.StopGame();
                            break;
                        case ConsoleKey.M:
                            server.BroadcastRandomMessage();
                            break;
                        case ConsoleKey.C:
                            host.RegisterPlayer("Local-Joel");
                            break;
                        case ConsoleKey.D:
                            host.UnregisterPlayer();
                            break;
                    }
                }
                else
                    System.Threading.Thread.Sleep(1000);
            }
        }
    }
}
