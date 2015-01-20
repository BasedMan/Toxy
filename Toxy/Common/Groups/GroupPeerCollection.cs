using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using SharpTox.Core;

namespace Toxy.Common
{
    public class GroupPeerCollection : ObservableCollection<GroupPeer>
    {
        public GroupPeerCollection(List<GroupPeer> peers)
            : base(peers) { }

        public GroupPeerCollection() { }

        public bool ContainsPeer(int peerNumber)
        {
            return GetPeerByPeerNumber(peerNumber) != null;
        }

        public void RemovePeer(int peerNumber)
        {
            var peer = GetPeerByPeerNumber(peerNumber);

            if (peer != null)
                this.Remove(peer);
        }

        /*public GroupPeer GetPeerByPublicKey(ToxKey publicKey)
        {
            var peers = this.Where(p => p.PublicKey == publicKey).ToArray();

            if (peers.Length == 1)
                return peers[0];
            else
                return null;
        }*/

        public GroupPeer GetPeerByPeerNumber(int peerNumber)
        {
            var peers = this.Where(p => p.PeerNumber == peerNumber).ToArray();

            if (peers.Length == 1)
                return peers[0];
            else
                return null;
        }
    }
}
