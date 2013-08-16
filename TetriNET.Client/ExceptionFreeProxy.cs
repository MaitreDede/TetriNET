﻿using System;
using System.ServiceModel;
using TetriNET.Common;

namespace TetriNET.Client
{
    public class ExceptionFreeProxy : IWCFTetriNET
    {
        private readonly IWCFTetriNET _proxy;
        private readonly IClient _client;

        public ExceptionFreeProxy(IWCFTetriNET proxy, IClient client)
        {
            _proxy = proxy;
            _client = client;
        }

        private void ExceptionFreeAction(Action action, string actionName)
        {
            try
            {
                action();
                _client.LastAction = DateTime.Now;
            }
            catch (CommunicationObjectAbortedException ex)
            {
                Log.WriteLine("CommunicationObjectAbortedException:{0}", actionName);
                _client.OnDisconnectedFromServer(this);
            }
            catch (CommunicationObjectFaultedException ex)
            {
                Log.WriteLine("CommunicationObjectFaultedException:{0}", actionName);
                _client.OnDisconnectedFromServer(this);
            }
            catch (EndpointNotFoundException ex)
            {
                Log.WriteLine("EndpointNotFoundException:{0}", actionName);
                _client.OnServerUnreachable(this);
            }
        }

        public void RegisterPlayer(string playerName)
        {
            ExceptionFreeAction(() => _proxy.RegisterPlayer(playerName), "RegisterPlayer");
        }

        public void UnregisterPlayer()
        {
            ExceptionFreeAction(_proxy.UnregisterPlayer, "UnregisterPlayer");
        }

        public void Ping()
        {
            ExceptionFreeAction(_proxy.Ping, "Ping");
        }

        public void PublishMessage(string msg)
        {
            ExceptionFreeAction(() => _proxy.PublishMessage(msg), "PublishMessage");
        }

        public void PlaceTetrimino(int index, Tetriminos tetrimino, Orientations orientation, Position position, PlayerGrid grid)
        {
            ExceptionFreeAction(() => _proxy.PlaceTetrimino(index, tetrimino, orientation, position, grid), "PlaceTetrimino");
        }

        public void SendAttack(int targetId, Attacks attack)
        {
            ExceptionFreeAction(() => _proxy.SendAttack(targetId, attack), "SendAttack");
        }
    }
}
