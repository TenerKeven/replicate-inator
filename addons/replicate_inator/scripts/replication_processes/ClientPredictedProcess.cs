using System.Collections.Generic;
using Godot;
using ReplicateInator.addons.replicate_inator.scripts;
using ReplicateInator.addons.replicate_inator.scripts.replication_fields;

public class ClientPredictedProcess : IProcess
{
    private readonly Queue<IInputReplication> _inputsToUpdateServer = new();
    private int _lastWaitingReconstituteInput = -1;
    
    void IProcess.Process(Replicator replicator)
    {
        ReconstituteInputs(ref replicator);
        
        while (_inputsToUpdateServer.Count > 0) 
        {
            IInputReplication input = _inputsToUpdateServer.Dequeue();
            
            replicator.RpcId(1, nameof(replicator.SentLocalInput), input.Serialize());
        }

        ClearInputsWaitingToReconstitute(ref replicator);
    }
    
    void IProcess.PhysicsProcess(Replicator replicator, float delta, int processTick, ulong timeNow)
    {
        replicator.Tick += 1;
        
        ClearSomeInputsToProcess(ref replicator);
        IInputReplication currentInput = replicator.LocalInputsToProcess.Count > 0 ? replicator.LocalInputsToProcess.Dequeue() : new EmptyInput(processTick, timeNow);
        
        currentInput.Tick = processTick;
        currentInput.Timestamp = timeNow;
        
        foreach (var replicationComponent in replicator.ReplicationComponents)
        {
            replicationComponent.Simulate(delta, processTick, timeNow, ref currentInput);
        } 
        
        _inputsToUpdateServer.Enqueue(currentInput);
        replicator.SaveTick(processTick, timeNow, currentInput);
        replicator.LocalInputsNotConfirmed.Enqueue(processTick);

        ClearReconciliations(ref replicator);
        DoReconciliation(ref replicator, processTick, delta);
    }
    
    private void ReconstituteInputs(ref Replicator replicator)
    {
        int lastConfirmed = -1;
        Queue<IInputReplication> notConfirmedInputs = new();

        int amountDequeue = replicator.LocalInputsNotConfirmed.Count;
        
        /*
        GD.Print("Not Confirmed Queue: [" + string.Join(", ", replicator.LocalInputsNotConfirmed) + "]");
        GD.Print("Dictionary: [" +
                 string.Join(", ", replicator.LocalInputsConfirmed.Select(kv => $"{kv.Key}:{kv.Value}")) + "]");
                 */
                 

        while (amountDequeue > 0)
        {
            int confirm = replicator.LocalInputsNotConfirmed.Peek();

            if (replicator.LocalInputsConfirmed.TryGetValue(confirm, out bool processed))
            {
                int difference = confirm - lastConfirmed;

                if (lastConfirmed == -1 || difference <= 1)
                {
                    lastConfirmed = confirm;
                }
                else
                {
                    for (int i = 0; i < difference; i++)
                    {
                        GD.Print("input not confirmed ", i);
                        int notConfirmedInput = confirm - i;

                        if (replicator.InputsBuffer.TryGetAt(notConfirmedInput, out IInputReplication input))
                        {
                            notConfirmedInputs.Enqueue(input);
                            replicator.InputsBuffer.TrySet(notConfirmedInput,new EmptyInput(notConfirmedInput, input.Timestamp));
                            replicator.LocalInputsWaitingToReconstitute.Add(notConfirmedInput, input);

                            if (notConfirmedInput < _lastWaitingReconstituteInput)
                            {
                                _lastWaitingReconstituteInput = notConfirmedInput;
                            }
                        }
                    }
                }
                
                replicator.LocalInputsNotConfirmed.Dequeue();
                replicator.LocalInputsConfirmed.Remove(confirm);

                if (!processed && replicator.InputsBuffer.TryGetAt(confirm, out IInputReplication oldInput))
                {
                    replicator.InputsBuffer.TrySet(confirm,new EmptyInput(confirm, oldInput.Timestamp));
                }
            }
            
            amountDequeue -= 1;
        }

        while (notConfirmedInputs.Count > 0)
        {
            IInputReplication input = notConfirmedInputs.Dequeue();
            
            replicator.RpcId(1, nameof(replicator.ReconstituteLocalInput), input.Serialize());
        }
    }
    
    private void ClearSomeInputsToProcess(ref Replicator replicator)
    {
        int count = replicator.LocalInputsToProcess.Count;
        int difference = count - replicator.maxLocalInputsToProcess;
        
        if (difference > 0)
        {
            for (int i = 0; i < difference; i++)
            {
                replicator.LocalInputsToProcess.Dequeue();
            }
        }
    }
    
    private void ClearInputsWaitingToReconstitute(ref Replicator replicator)
    {
        int count = replicator.LocalInputsWaitingToReconstitute.Count;
        int difference = count - replicator.maxLocalInputsWaitingToReconstitute;
        
        if (difference > 0)
        {
            for (int i = 0; i < difference; i++)
            {
                replicator.LocalInputsWaitingToReconstitute.Remove(_lastWaitingReconstituteInput);
                _lastWaitingReconstituteInput += 1;
            }
        }
    }

    private void DoReconciliation(ref Replicator replicator, int processTick, float delta)
    {
        int amount = processTick - replicator.nextLocalReconcilationTick;
        
        while (amount > 0)
        {
            if (replicator.LocalReconciliationDictionary.TryGetValue(replicator.nextLocalReconcilationTick, out var value))
            {
                if (value[0].Count == 0)
                {
                    GD.Print("0 bro from recon");
                }
                
                for (int i = 0; i < value.Count; i++)
                {
                    var component = replicator.ReplicationComponents[i];
                    bool reconciliation = value[i].Count == 0 ? false : component.NeedReconciliation(replicator.nextLocalReconcilationTick, value[i]);
                    
                    if (reconciliation)
                    {
                        if (replicator.rollback && component.rollback)
                        {
                            for (int a = processTick; a >= replicator.nextLocalReconcilationTick; a--)
                            {
                                if (replicator.InputsBuffer.TryGetAt(a, out IInputReplication input))
                                {
                                    component.RollbackLocalTick(a, delta, ref input);
                                }
                            }
                        }
                        
                        for (int a = replicator.nextLocalReconcilationTick + 1; a <= processTick; a++)
                        {
                            if (replicator.InputsBuffer.TryGetAt(a, out IInputReplication input))
                            {
                                component.ReconciliationTick(a, delta, ref input);
                            }
                        }
                    }
                }
                
                replicator.LocalReconciliationDictionary.Remove(replicator.nextLocalReconcilationTick);
                replicator.lastLocalReconcilatedTick = replicator.nextLocalReconcilationTick;
                replicator.nextLocalReconcilationTick += 1;
            }

            amount -= 1;
        }
    }

    private void ClearReconciliations(ref Replicator replicator)
    {
        int difference = replicator.LocalReconciliationDictionary.Count - replicator.maxLocalReconciliationsAmount;
        
        if (difference > 0)
        {
            for (int i = 0; i < difference; i++)
            {
                if (replicator.LocalReconciliationDictionary.TryGetValue(replicator.nextLocalReconcilationTick, out _))
                {
                    replicator.LocalReconciliationDictionary.Remove(replicator.nextLocalReconcilationTick);
                }
                
                replicator.nextLocalReconcilationTick += 1;
            }
            
            replicator.lastLocalReconcilatedTick = replicator.nextLocalReconcilationTick - 1;
        }
    }
}
