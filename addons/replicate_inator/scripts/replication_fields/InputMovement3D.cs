using System;
using Godot;

namespace ReplicateInator.addons.replicate_inator.scripts.replication_fields;

[InputId(0x02)]
public struct InputMovement3D (int tick, ulong timeStamp, Vector2I inputDirection, bool jumped) : IInputReplication
{
    public int Tick { get; set; } = Math.Max(tick, 0);
    public ulong Timestamp { get; set; } = timeStamp;
    public Vector2I inputDirection { get; set; } = inputDirection;
    public bool jumped { get; set; } = jumped;
    public bool Processed { get; set; } = false;
    
    public byte[] Serialize()
    {
        var stream = new StreamPeerBuffer();
        
        stream.PutU16(InputRegistry.GetId<InputMovement3D>());
        
        stream.Put32(Tick);
        stream.PutU64(Timestamp);
        
        stream.Put32(inputDirection.X);
        stream.Put32(inputDirection.Y);
        
        stream.Put8((sbyte)(jumped ? 1 : 0));
        
        return stream.DataArray; 
    }

    public static IInputReplication Deserialize(byte[] data)
    {
        var stream = new StreamPeerBuffer();
        stream.DataArray = data;
        
        var tick = stream.Get32();
        var timeStamp = stream.GetU64();
        
        var inputDirection = new Vector2I(stream.Get32(), stream.Get32());
        
        var jumped = stream.Get8() != 0;
        
        return new InputMovement3D(tick, timeStamp, inputDirection, jumped);
    }

    public bool IsValidInput()
    {
        return (inputDirection.X <= 1 && inputDirection.Y <= 1) && (inputDirection.X >= -1 && inputDirection.Y >= -1) && Tick >= 0;
    }
}