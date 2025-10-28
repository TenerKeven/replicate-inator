using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;
using ReplicateInator.addons.replicate_inator.scripts;
using ReplicateInator.addons.replicate_inator.scripts.replication_fields;

public class ServerProcess : IProcess
{
    private int _tickOffset = 0;
    private readonly Queue<UpdateClientValues> _valuesToUpdateClient = new();
    
    void IProcess.Process(Replicator replicator)
    {
        while (_valuesToUpdateClient.Count > 0)
        {
            UpdateClientValues values = _valuesToUpdateClient.Dequeue();
            Array<Array<byte[]>> updateArray = [];

            for (int i = 0; i < replicator.ReplicationComponents.Count; i++)
            {
                updateArray.Add(replicator.ReplicationComponents[i].ConvertStateToBytes(values.ServerTick));
            }
            
            replicator.RpcId(replicator.NetworkId, nameof(replicator.UpdateLocalClientReconciliationStates), updateArray, values.ClientTick);
        }
    }
    
    void IProcess.PhysicsProcess(Replicator replicator, float delta, int processTick, ulong timeNow)
    {
        int newTickOffset = 0;

        if (replicator.PingPong.GetClientStat(replicator.NetworkId, out PingPong.PingStats pingStats))
        {
            newTickOffset = pingStats.GetTickOffsetAuto(replicator.TickDeltaMs);
        }

        newTickOffset = Math.Clamp(newTickOffset, replicator.MinimunTickOffset, replicator.maxTickOffset);
        _tickOffset = newTickOffset;

        ClearSomeInputsToProcess(ref replicator);
        bool simulatedClientInput = TrySimulatedClientInput(ref replicator, delta, processTick, timeNow);
        
        if (simulatedClientInput) return;
        
        IInputReplication emptyInput = new EmptyInput(processTick, timeNow);
        
        foreach (var replicationComponent in replicator.ReplicationComponents)
        {
            replicationComponent.Simulate(delta, processTick, timeNow, ref emptyInput);
        }
            
        replicator.SaveTick(processTick, timeNow, emptyInput);
    }

    private bool TrySimulatedClientInput(ref Replicator replicator, float delta, int processTick, ulong timeNow)
    {
        if (replicator.currentClientInputToProcess > -1 && replicator.ClientInputsToProcess.TryGetValue(replicator.currentClientInputToProcess, out IInputReplication input))
        {
            int clientTick = replicator.currentClientInputToProcess;
            int serverTick = clientTick + _tickOffset;
            
            if (clientTick < replicator.lastClientTickProcessed || processTick > serverTick)
            {
                replicator.ClientInputsToProcess.Remove(replicator.currentClientInputToProcess);
                replicator.currentClientInputToProcess += 1;
                
                TrySimulatedClientInput(ref replicator, delta, processTick, timeNow);
                return false;
            }

            if (serverTick < processTick)
            {
                return false;
            }

            IInputReplication serverVersion = input;
            
            serverVersion.Tick = processTick;
            serverVersion.Timestamp = timeNow;
            
            foreach (var replicationComponent in replicator.ReplicationComponents)
            {
                replicationComponent.Simulate(delta, processTick, timeNow, ref serverVersion);
            }
            
            replicator.SaveTick(processTick, timeNow, serverVersion);
            
            _valuesToUpdateClient.Enqueue(new UpdateClientValues(clientTick, processTick));
            
            replicator.ClientInputsToProcess.Remove(replicator.currentClientInputToProcess);
            replicator.lastClientTickProcessed = replicator.currentClientInputToProcess;
            replicator.currentClientInputToProcess += 1;
            
            return true;
        }
        
        if (replicator.currentClientInputToProcess > -1)
        {
            replicator.currentClientInputToProcess += 1;
        }

        return false;
    }
    
    private void ClearSomeInputsToProcess(ref Replicator replicator)
    {
        int count = replicator.ClientInputsToProcess.Count;
        int difference = count - replicator.maxInputsToServerStore;
        
        if (difference > 0)
        {
            for (int i = 0; i < difference; i++)
            {
                replicator.ClientInputsToProcess.Remove(replicator.currentClientInputToProcess);
                replicator.currentClientInputToProcess += 1;
            }
            
            replicator.lastClientTickProcessed = replicator.currentClientInputToProcess - 1;
        }
    }
}

public struct UpdateClientValues(int clientTick, int serverTick)
{
    public int ClientTick { get; set; } = clientTick;
    public int ServerTick { get; set; } = serverTick;
}
