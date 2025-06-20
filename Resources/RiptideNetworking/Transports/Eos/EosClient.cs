// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using Epic.OnlineServices.P2P;
using Epic.OnlineServices.Sessions;
using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Riptide.Transports.Eos {
    /// <summary>A client which can connect to a <see cref="EosServer"/>.</summary>
    public class EosClient : EosPeer, IClient {
        /// <inheritdoc/>
        public event EventHandler Connected;
        public event EventHandler ConnectionFailed;
        public event EventHandler<DataReceivedEventArgs> DataReceived;
        public event EventHandler<DisconnectedEventArgs> Disconnected;

        private const string LocalHostName = "localhost";
        private const string LocalHostIP = "127.0.0.1";

        private EosConnection steamConnection;
        private EosServer localServer;

        public EosClient(EosServer localServer) {
            this.localServer = localServer;
        }

        public void ChangeLocalServer(EosServer newLocalServer) {
            localServer = newLocalServer;
        }

        public bool Connect(string hostAddress, out Connection connection, out string connectError) {
            connection = null;
            int port = 0;

            try {
                JoinLobbyByIdOptions options = new();
                options.LocalUserId = EOSSDK.LocalUserProductId;
                options.LobbyId = hostAddress;
                EOSSDK.GetLobbyInterface().JoinLobbyById(ref options, null, null);
            }
            catch (Exception ex) {
                connectError = $"Couldn't connect: {ex}";
                return false;
            }

            connectError = $"Invalid host address '{hostAddress}'! Expected '{LocalHostIP}' or '{LocalHostName}' for local connections, or a valid Steam ID.";
            if (hostAddress == LocalHostIP || hostAddress == LocalHostName) {
                if (localServer == null) {
                    connectError = $"No locally running server was specified to connect to! Either pass a {nameof(EosServer)} instance to your {nameof(EosClient)}'s constructor or call its {nameof(EosClient.ChangeLocalServer)} method before attempting to connect locally.";
                    connection = null;
                    return false;
                }

                connection = steamConnection = ConnectLocal();
                return true;
            }

            int portSeperatorIndex = hostAddress.IndexOf(':');
            if (portSeperatorIndex != -1) {
                if (!int.TryParse(hostAddress[(portSeperatorIndex + 1)..], out port)) {
                    connectError = $"Couldn't connect: Failed to parse port '{hostAddress[(portSeperatorIndex + 1)..]}'";
                    return false;
                }
                hostAddress = hostAddress[..portSeperatorIndex];
            }

            if (ulong.TryParse(hostAddress, out ulong hostId)) {
                connection = steamConnection = TryConnect(EOSSDK.LocalUserProductId, hostAddress);
                return connection != null;
            }

            return false;
        }

        private EosConnection ConnectLocal() {
            Debug.Log($"{LogName}: Connecting to locally running server...");

            ProductUserId playerSteamId = EOSSDK.LocalUserProductId;

            EosConnection connection = new EosConnection(playerSteamId, this);
            localServer.Add(connection);
            OnConnected();
            return connection;
        }

        private EosConnection TryConnect(ProductUserId hostId, string address) {
            try {
                JoinLobbyByIdOptions options = new();
                options.LocalUserId = hostId;
                options.LobbyId = address;
                EOSSDK.GetLobbyInterface().JoinLobbyById(ref options, null, null);

                ConnectTimeout();
                return new EosConnection(EOSSDK.LocalUserProductId, this);
            }
            catch (Exception ex) {
                Debug.LogException(ex);
                OnConnectionFailed();
                return null;
            }
        }

        private async void ConnectTimeout() // TODO: confirm if this is needed, Riptide *should* take care of timing out the connection
        {
            Task timeOutTask = Task.Delay(6000); // TODO: use Riptide Client's TimeoutTime
            await Task.WhenAny(timeOutTask);

            if (!steamConnection.IsConnected)
                OnConnectionFailed();
        }

        public void Poll() {
            if (steamConnection != null)
                Receive(steamConnection);
        }

        // TODO: disable nagle so this isn't needed
        //public void Flush()
        //{
        //    foreach (SteamConnection connection in connections.Values)
        //        SteamNetworkingSockets.FlushMessagesOnConnection(connection.SteamNetConnection);
        //}

        public void Disconnect() {
            LeaveLobbyOptions options = new();
            options.LocalUserId = EOSSDK.LocalUserProductId;
            EOSSDK.GetLobbyInterface().LeaveLobby(ref options, null, null);
            steamConnection = null;
        }

        protected virtual void OnConnected() {
            Connected?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnConnectionFailed() {
            ConnectionFailed?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnDataReceived(byte[] dataBuffer, int amount, EosConnection fromConnection) {
            DataReceived?.Invoke(this, new DataReceivedEventArgs(dataBuffer, amount, fromConnection));
        }

        protected virtual void OnDisconnected(DisconnectReason reason) {
            Disconnected?.Invoke(this, new DisconnectedEventArgs(steamConnection, reason));
        }
    }
}

public class RandomString {

    // Generates a random string with a given size.    
    public static string Generate(int size) {
        var builder = new StringBuilder(size);

        System.Random random = new();

        // Unicode/ASCII Letters are divided into two blocks
        // (Letters 65–90 / 97–122):
        // The first group containing the uppercase letters and
        // the second group containing the lowercase.  

        // char is a single Unicode character  
        char offsetLowerCase = 'a';
        char offsetUpperCase = 'A';
        const int lettersOffset = 26; // A...Z or a..z: length=26  

        for (var i = 0; i < size; i++) {
            char offset;
            if (random.Next(0, 2) == 0) {
                offset = offsetLowerCase;
            }
            else {
                offset = offsetUpperCase;
            }

            var @char = (char)random.Next(offset, offset + lettersOffset);
            builder.Append(@char);
        }

        return builder.ToString();
    }
}