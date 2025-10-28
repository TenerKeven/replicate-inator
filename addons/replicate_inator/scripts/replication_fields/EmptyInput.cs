using System;
using Godot;

namespace ReplicateInator.addons.replicate_inator.scripts.replication_fields;

[InputId(0x01)]
public struct EmptyInput(int tick, ulong timeStamp) : IInputReplication
{
    public int Tick { get; set; } = Math.Max(tick, 0);
    public ulong Timestamp { get; set; } = timeStamp;
    public bool Processed { get; set; } = false;
    public byte[] Serialize()
    {
        var stream = new StreamPeerBuffer();
        
        stream.PutU16(InputRegistry.GetId<EmptyInput>());
        
        stream.Put32(Tick);
        stream.PutU64(Timestamp);
        
        return stream.DataArray; 
    }

    public static IInputReplication Deserialize(byte[] data)
    {
        var stream = new StreamPeerBuffer();
        stream.DataArray = data;
        
        var tick = stream.Get32();
        var timeStamp = stream.GetU64();
        
        return new EmptyInput(tick, timeStamp);
    }

    public bool IsValidInput()
    {
        return Tick >= 0;
    }
}