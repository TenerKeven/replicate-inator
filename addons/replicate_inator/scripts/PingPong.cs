using Godot;
using System;
using System.Collections.Generic;
using ReplicateInator.addons.replicate_inator.scripts;

[GlobalClass]
public partial class PingPong : Node
{
    [Export] private bool debug;
    private const float PingInterval = 1.0f; 
    private const int MaxSamples = 100;
    private const double OffsetEwmaAlpha = 0.2;

    // ---------------- PingStats ----------------
    public class PingStats
    {
        public Dictionary<int, ulong> Pending = new();
        public List<double> RttSamples = new();
        public List<double> OneWayClientToServer = new();
        public List<double> OneWayServerToClient = new();
        public int Sent = 0;
        public int Received = 0;
        public double AvgRtt = 0;
        public double Jitter = 0;
        public double Offset = 0;
        private const int MaxSamples = 100;

        public double PacketLoss => Sent == 0 ? 0 : 1.0 - (double)Received / Sent;
        public double AvgOWD_CS => OneWayClientToServer.Count == 0 ? 0 : Sum(OneWayClientToServer) / OneWayClientToServer.Count;
        public double AvgOWD_SC => OneWayServerToClient.Count == 0 ? 0 : Sum(OneWayServerToClient) / OneWayServerToClient.Count;

        private double Sum(List<double> list)
        {
            double s = 0;
            foreach (var v in list) s += v;
            return s;
        }

        public void AddSample(double rtt, double owd_cs, double owd_sc)
        {
            RttSamples.Add(rtt);
            OneWayClientToServer.Add(owd_cs);
            OneWayServerToClient.Add(owd_sc);

            if (RttSamples.Count > MaxSamples)
            {
                RttSamples.RemoveAt(0);
                OneWayClientToServer.RemoveAt(0);
                OneWayServerToClient.RemoveAt(0);
            }
            
            double sum = 0;
            foreach (var s in RttSamples) sum += s;
            AvgRtt = sum / RttSamples.Count;
            
            double jitterSum = 0;
            for (int i = 1; i < RttSamples.Count; i++)
                jitterSum += Math.Abs(RttSamples[i] - RttSamples[i - 1]);
            Jitter = jitterSum / Math.Max(1, RttSamples.Count - 1);
        }
        
        public int GetTickOffsetAuto(float tickDeltaMs)
        {
            if (tickDeltaMs <= 0.0)
                return 0;
            
            double kJitterDynamic = Math.Clamp(Jitter / 10.0, 0.5, 2.0); 
            double jitterMargin = Jitter * kJitterDynamic;
            
            double kLossDynamic = Math.Clamp(PacketLoss * 200.0, 20.0, 100.0); 
            double lossMargin = kLossDynamic;

            double totalOffsetMs = Offset + jitterMargin + lossMargin;
            
            return (int)Math.Round(totalOffsetMs / tickDeltaMs);
        }
    }

    // ---------------- SERVER ----------------
    private Dictionary<long, PingStats> serverClients = new();
    private Dictionary<long, int> serverNextPingId = new();

    // ---------------- CLIENT ----------------
    private PingStats clientStats = new();
    private double clientClockOffset = 0.0;
    private int clientNextPingId = 0;

    public bool GetClientStat(long peerId, out PingStats stats)
    {
        if (serverClients.TryGetValue(peerId, out PingStats foundStats))
        {
            stats = foundStats;
            return true;
        }

        stats = null;
        return false;
    }

    public override void _Ready()
    {
        Name = "PingManager";

        ReplicateGlobalObjects.pingPong = this;
        
        if (Multiplayer.IsServer())
        {
            Multiplayer.PeerConnected += OnPeerConnected;
            Multiplayer.PeerDisconnected += OnPeerDisconnected;
        }

        GetTree().CreateTimer(PingInterval).Timeout += StartPingLoop;
    }

    private void OnPeerConnected(long id)
    {
        serverClients[id] = new PingStats();
        serverNextPingId[id] = 0;
        GD.Print($"[PingManager] Peer conectado: {id}");
    }

    private void OnPeerDisconnected(long id)
    {
        serverClients.Remove(id);
        serverNextPingId.Remove(id);
        GD.Print($"[PingManager] Peer desconectado: {id}");
    }

    private void StartPingLoop()
    {
        ulong now = Time.GetTicksMsec();

        if (Multiplayer.IsServer())
        {
            foreach (var kv in serverClients)
            {
                long peerId = kv.Key;
                var stats = kv.Value;

                int pingId = serverNextPingId[peerId]++;
                stats.Pending[pingId] = now;
                stats.Sent++;

                RpcId(peerId, nameof(ClientReceivePing), pingId, now);
            }
        }
        else
        {
            int pingId = clientNextPingId++;
            clientStats.Pending[pingId] = now;
            clientStats.Sent++;

            RpcId(1, nameof(ServerReceivePing), pingId, now);
        }

        GetTree().CreateTimer(PingInterval).Timeout += StartPingLoop;
    }

    // ---------------- CLIENTE → SERVIDOR ----------------
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void ServerReceivePing(int pingId, ulong clientSendTime)
    {
        ulong serverReceiveTime = Time.GetTicksMsec();
        long senderId = Multiplayer.GetRemoteSenderId();

        RpcId(senderId, nameof(ClientReceivePong), pingId, clientSendTime, serverReceiveTime);
    }

    // ---------------- CLIENTE ----------------
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void ClientReceivePing(int pingId, ulong serverSendTime)
    {
        ulong clientReceiveTime = Time.GetTicksMsec();
        RpcId(1, nameof(ServerReceivePong), pingId, serverSendTime, clientReceiveTime, Time.GetTicksMsec());
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void ClientReceivePong(int pingId, ulong clientSendTime, ulong serverReceiveTime)
    {
        if (!clientStats.Pending.Remove(pingId, out ulong start)) return;
        clientStats.Received++;

        ulong now = Time.GetTicksMsec();
        double rtt = now - start;

        double owd_cs = serverReceiveTime - start;
        double owd_sc = now - clientSendTime;

        clientStats.AddSample(rtt, owd_cs, owd_sc);

        double measuredOffset = ((double)clientSendTime + (double)serverReceiveTime) / 2 - ((double)start + (double)now) / 2;
        clientClockOffset = OffsetEwmaAlpha * measuredOffset + (1 - OffsetEwmaAlpha) * clientClockOffset;
        
        if (!debug) return;
        GD.Print($"[Client] RTT={clientStats.AvgRtt:F1}ms Jitter={clientStats.Jitter:F1}ms " +
                 $"Loss={clientStats.PacketLoss*100:F1}% Offset={clientClockOffset:F1}ms " +
                 $"OWD_CS={clientStats.AvgOWD_CS:F1}ms OWD_SC={clientStats.AvgOWD_SC:F1}ms");
    }

    // ---------------- SERVER RECEIVE PONG ----------------
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void ServerReceivePong(int pingId, ulong serverSendTime, ulong clientReceiveTime, ulong clientSendTime)
    {
        long peerId = Multiplayer.GetRemoteSenderId();
        if (!serverClients.TryGetValue(peerId, out var stats)) return;
        if (!stats.Pending.Remove(pingId, out ulong sent)) return;

        stats.Received++;
        ulong now = Time.GetTicksMsec();
        double rtt = now - sent;

        double owd_cs = clientReceiveTime - sent;
        double owd_sc = now - serverSendTime;

        stats.AddSample(rtt, owd_cs, owd_sc);

        double offset = ((double)serverSendTime + (double)now) / 2 - ((double)clientSendTime + (double)clientReceiveTime) / 2;
        stats.Offset = offset;

        if (!debug) return;
        GD.Print($"[Server→{peerId}] RTT={stats.AvgRtt:F1}ms Jitter={stats.Jitter:F1}ms " +
                 $"Loss={stats.PacketLoss*100:F1}% Offset={stats.Offset:F1}ms " +
                 $"OWD_CS={stats.AvgOWD_CS:F1}ms OWD_SC={stats.AvgOWD_SC:F1}ms");
    }

    // ---------------- UTIL ----------------
    public double GetServerTimeClient() => Time.GetTicksMsec() + clientClockOffset;
}
