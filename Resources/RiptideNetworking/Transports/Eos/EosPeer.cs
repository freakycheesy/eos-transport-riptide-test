// This file is provided under The MIT License as part of RiptideSteamTransport.
// Copyright (c) Tom Weiland

using Epic.OnlineServices;
using Epic.OnlineServices.P2P;
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Riptide.Transports.Eos {
    public abstract class EosPeer {
        /// <summary>The name to use when logging messages via <see cref="Utils.RiptideLogger"/>.</summary>
        public const string LogName = "EOS";

        protected const int MaxMessages = 256;

        private readonly byte[] receiveBuffer;

        protected EosPeer() {
            receiveBuffer = new byte[Message.MaxSize + sizeof(ushort)];
            SetPortRangeOptions port = new();
            port.Port = 7777;
            EOSSDK.GetP2PInterface().SetPortRange(ref port);

            SetRelayControlOptions options = new();
            options.RelayControl = RelayControl.AllowRelays;
            EOSSDK.GetP2PInterface().SetRelayControl(ref options);
        }

        protected void Receive(EosConnection fromConnection) {
            IntPtr[] ptrs = new IntPtr[MaxMessages]; // TODO: remove allocation?

            // TODO: consider using poll groups -> https://partner.steamgames.com/doc/api/ISteamNetworkingSockets#functions_poll_groups
            ReceivePacketOptions packetOptions = new ReceivePacketOptions();
            packetOptions.LocalUserId = EOSSDK.LocalUserProductId;
            packetOptions.RequestedChannel = null;
            ProductUserId id = fromConnection.ProductUserId;
            SocketId socket = new();
            ArraySegment<byte> bytes = new ArraySegment<byte>();
            if (EOSSDK.GetP2PInterface().ReceivePacket(ref packetOptions, ref id, ref socket, out _, bytes, out uint amount) == Result.Success) {
                for (int i = 0; i < bytes.Array.Length; i++) {
                    OnDataReceived(receiveBuffer, (int)amount, fromConnection);
                }
            }
        }

        internal void Send(byte[] dataBuffer, int numBytes, ProductUserId toConnection) {
            GCHandle handle = GCHandle.Alloc(dataBuffer, GCHandleType.Pinned);
            IntPtr pDataBuffer = handle.AddrOfPinnedObject();

            SendPacketOptions options = new SendPacketOptions();
            options.LocalUserId = EOSSDK.LocalUserProductId;
            options.RemoteUserId = toConnection;
            options.Reliability = PacketReliability.ReliableOrdered | PacketReliability.UnreliableUnordered;
            Result result = EOSSDK.GetP2PInterface().SendPacket(ref options);
            if (result != Result.Success)
                Debug.LogWarning($"{LogName}: Failed to send {numBytes} bytes - {result}");

            handle.Free();
        }

        protected abstract void OnDataReceived(byte[] dataBuffer, int amount, EosConnection fromConnection);
    }
}
