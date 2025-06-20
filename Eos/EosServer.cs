// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using Epic.OnlineServices.P2P;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Riptide.Transports.Eos {
    /// <summary>A server which can accept connections from <see cref="EosClient"/>s.</summary>
    public class EosServer : EosPeer, IServer {
        public event EventHandler<ConnectedEventArgs> Connected;
        public event EventHandler<DataReceivedEventArgs> DataReceived;
        public event EventHandler<DisconnectedEventArgs> Disconnected;

        public ushort Port {
            get; private set;
        }

        private Dictionary<ProductUserId, EosConnection> connections;
        public string listenAddress;
        public void Start(ushort port) {
            Port = port;
            listenAddress = RandomString.Generate(16);
            connections = new();

            try {
                SetRelayControlOptions relay = new();
                EOSSDK.GetP2PInterface().SetRelayControl(ref relay);
            }
            catch (Exception ex) {
                Debug.LogException(ex);
            }
            CreateLobbyOptions options = new CreateLobbyOptions();
            options.LobbyId = listenAddress;
            options.DisableHostMigration = true;
            options.EnableJoinById = true;
            options.LocalUserId = EOSSDK.LocalUserProductId;
            EOSSDK.GetLobbyInterface().CreateLobby(ref options, null, null);
        }

        internal void Add(EosConnection connection) {
            if (!connections.ContainsKey(connection.ProductUserId)) {
                connections.Add(connection.ProductUserId, connection);
                OnConnected(connection);
            }
            else
                Debug.Log($"{LogName}: Connection from {connection.ProductUserId} could not be accepted: Already connected");
        }

        private void Accept(ProductUserId connection) {
            AcceptConnectionOptions options = new();
            options.RemoteUserId = connection;

            Result result = EOSSDK.GetP2PInterface().AcceptConnection(ref options);
            if (result != Result.Success)
                Debug.LogWarning($"{LogName}: Connection could not be accepted: {result}");
            }

        public void Close(Connection connection) {
            if (connection is EosConnection steamConnection) {
                CloseConnectionOptions options = new();
                options.RemoteUserId = steamConnection.ProductUserId;
                EOSSDK.GetP2PInterface().CloseConnection(ref options);
                connections.Remove(steamConnection.ProductUserId);
            }
        }

        public void Poll() {
            foreach (EosConnection connection in connections.Values)
                Receive(connection);
        }

        // TODO: disable nagle so this isn't needed
        //public void Flush()
        //{
        //    foreach (SteamConnection connection in connections.Values)
        //        SteamNetworkingSockets.FlushMessagesOnConnection(connection.SteamNetConnection);
        //}

        public void Shutdown() {
            CloseConnectionOptions connectionOptions = new CloseConnectionOptions();
            foreach (EosConnection connection in connections.Values) {
                connectionOptions.RemoteUserId = connection.ProductUserId;
                EOSSDK.GetP2PInterface().CloseConnection(ref connectionOptions);
            }

            connections.Clear();
            DestroyLobbyOptions options = new();
            options.LocalUserId = EOSSDK.LocalUserProductId;
            options.LobbyId = Port.ToString();
            EOSSDK.GetLobbyInterface().DestroyLobby(ref options, null, null);
        }

        protected internal virtual void OnConnected(Connection connection) {
            Connected?.Invoke(this, new ConnectedEventArgs(connection));
        }

        protected override void OnDataReceived(byte[] dataBuffer, int amount, EosConnection fromConnection) {
            if ((MessageHeader)dataBuffer[0] == MessageHeader.Connect) {
                if (fromConnection.DidReceiveConnect)
                    return;

                fromConnection.DidReceiveConnect = true;
            }

            DataReceived?.Invoke(this, new DataReceivedEventArgs(dataBuffer, amount, fromConnection));
        }

        protected virtual void OnDisconnected(ProductUserId steamId, DisconnectReason reason) {
            if (connections.TryGetValue(steamId, out EosConnection connection)) {
                Disconnected?.Invoke(this, new DisconnectedEventArgs(connection, reason));
                connections.Remove(steamId);
            }
        }
    }
}
