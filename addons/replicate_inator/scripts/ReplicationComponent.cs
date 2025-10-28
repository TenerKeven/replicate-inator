using Godot;
using Godot.Collections;
using ReplicateInator.addons.replicate_inator.scripts;

public abstract partial class ReplicationComponent : Node, IReplicationComponent
{
    protected int tick;
    public NodePath targetPath; 
    [Export] public bool rollback;
    
    public override void _Ready()
    {
        SetPhysicsProcess(false);
        SetProcess(false);
    }

    public virtual void Initialize(NodePath targetPath)
    {
        this.targetPath = targetPath;
    }
    
    public abstract bool NeedReconciliation(int reconciliationTick, Array<byte[]> data);
    public abstract void ReconciliationTick(int reconciliationTick, float delta, ref IInputReplication input);
    public abstract void RollbackLocalTick(int rollbackTick, float delta, ref IInputReplication input);
    public abstract Array<byte[]> ConvertStateToBytes(int processTick);

    public abstract void Simulate(float delta, int processTick, ulong timeNow, ref IInputReplication input);
    public abstract void SaveTickFromData(int processTick, Array<byte[]> data);
    public abstract void SaveTick(int processTick, ulong timeNow);
}
