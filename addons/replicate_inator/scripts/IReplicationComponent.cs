using Godot;
using Godot.Collections;
using ReplicateInator.addons.replicate_inator.scripts;

public interface IReplicationComponent
{
    public abstract void Initialize(NodePath targetPath);
    public abstract bool NeedReconciliation(int reconciliationTick, Array<byte[]> data);
    public abstract void ReconciliationTick(int reconciliationTick, float delta, ref IInputReplication input);
    public abstract void RollbackLocalTick(int rollbackTick, float delta, ref IInputReplication input);
    public abstract Array<byte[]> ConvertStateToBytes(int processTick);
    public abstract void Simulate(float delta, int processTick, ulong timeNow, ref IInputReplication input);
    public abstract void SaveTickFromData(int processTick, Array<byte[]> data);
    public abstract void SaveTick(int processTick, ulong timeNow);
}
