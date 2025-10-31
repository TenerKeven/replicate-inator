using System;
using Godot;
using Godot.Collections;
using ReplicateInator.addons.replicate_inator.scripts;

public class ClientInterpolatedProcess : IProcess
{
    private int lastServerTickSaved;
    
    void IProcess.Process(Replicator replicator)
    {
        
    }
    
    void IProcess.PhysicsProcess(Replicator replicator, float delta, int processTick, ulong timeNow)
    {
        int currentInterpolationTick = replicator.currentInterpolationTick < processTick ? processTick : replicator.currentInterpolationTick;
        
        foreach (var component in replicator.ReplicationComponents)
        {
            component.SimulateInterpolation(delta, processTick, replicator.currentInterpolationTick, processTick / currentInterpolationTick);
        }

        if (currentInterpolationTick == processTick)
        {
            if (replicator.interpolationTicksOrder.Count > 0)
            {
                replicator.currentInterpolationTick = replicator.interpolationTicksOrder[0];
                replicator.interpolationTicksOrder.RemoveAt(0);
                replicator.Tick += 1;
            }
            else
            {
                replicator.currentInterpolationTick = processTick;
            }
        }
        else
        {
            replicator.Tick += 1;
        }
    }
}
