using Godot;
using ReplicateInator.addons.replicate_inator.scripts.replication_fields;

namespace ReplicateInator.addons.replicate_inator.scripts;

public interface IInputReplication
{
    public int Tick { get; set; }
    public ulong Timestamp { get; set; }
    public bool Processed { get; set; }

    public abstract byte[] Serialize();

    public static IInputReplication Deserialize(byte[] data)
    {
        var stream = new StreamPeerBuffer();
        stream.DataArray = data;
        
        var tick = stream.Get32();
        var timeStamp = stream.GetU64();
        
        return new EmptyInput(tick, timeStamp);
    }
    
    public abstract bool IsValidInput ();
}