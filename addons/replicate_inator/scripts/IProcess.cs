using ReplicateInator.addons.replicate_inator.scripts;

public interface IProcess
{
    public void Process(Replicator replicator);
    public void PhysicsProcess(Replicator replicator, float delta, int processTick, ulong timeNow);
}
