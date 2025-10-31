using Godot;
using Godot.Collections;
using ReplicateInator.addons.replicate_inator.scripts;

public abstract partial class ReplicationComponent : Node, IReplicationComponent
{
    protected int tick;
    private NodePath targetPath; 
    private NodePath visualPath;
    
    [Export] public bool rollback;
    
    public override void _Ready()
    {
        SetPhysicsProcess(false);
        SetProcess(false);
    }

    public virtual void Initialize(NodePath targetPath, NodePath visualPath)
    {
        this.targetPath = targetPath;
        this.visualPath = visualPath;
    }
    
    public abstract bool NeedReconciliation(int reconciliationTick, Array<byte[]> data);
    public abstract void ReconciliationTick(int reconciliationTick, float delta, ref IInputReplication input);
    public abstract void RollbackLocalTick(int rollbackTick, float delta, ref IInputReplication input);
    public abstract Array<byte[]> ConvertStateToBytes(int processTick);
    public abstract void AdjustVisuals();
    public abstract void SimulateInterpolation(float delta, int currentTick, int nextServerReceivedTick, float alphaBetweenTicks);
    public abstract void Simulate(float delta, int processTick, ulong timeNow, ref IInputReplication input);
    public abstract void SaveTickFromData(int processTick, Array<byte[]> data, bool applySave);
    public abstract void ApplyReplicatedServerData(int processTick, Array<byte[]> data, bool saveTick);
    public abstract void SaveTick(int processTick, ulong timeNow);
}
