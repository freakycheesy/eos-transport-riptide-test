// This file is provided under The MIT License as part of RiptideSteamTransport.
// Copyright (c) Tom Weiland

using Epic.OnlineServices;
using System;
using System.Collections.Generic;

namespace Riptide.Transports.Eos {
    public class EosConnection : Connection, IEquatable<EosConnection> {
        public readonly ProductUserId ProductUserId;

        internal bool DidReceiveConnect;

        private readonly EosPeer peer;

        internal EosConnection(ProductUserId steamId, EosPeer peer) {
            ProductUserId = steamId;
            this.peer = peer;
        }

        protected internal override void Send(byte[] dataBuffer, int amount) {
            peer.Send(dataBuffer, amount, ProductUserId);
        }

        /// <inheritdoc/>
        public override string ToString() => ProductUserId.ToString();

        /// <inheritdoc/>
        public override bool Equals(object obj) => Equals(obj as EosConnection);
        /// <inheritdoc/>
        public bool Equals(EosConnection other) {
            if (other is null)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            return ProductUserId.Equals(other.ProductUserId);
        }

        /// <inheritdoc/>
        public override int GetHashCode() {
            return -721414014 + EqualityComparer<ProductUserId>.Default.GetHashCode(ProductUserId);
        }

        public static bool operator ==(EosConnection left, EosConnection right) {
            if (left is null) {
                if (right is null)
                    return true;

                return false; // Only the left side is null
            }

            // Equals handles case of null on right side
            return left.Equals(right);
        }

        public static bool operator !=(EosConnection left, EosConnection right) => !(left == right);
    }
}
