using Godot;

namespace ReplicateInator.addons.replicate_inator.scripts.replication_fields;

public struct Transform3DField(int tick, ulong timeStamp, Transform3D transform, Vector3 velocity) : IReplicationField
{
    public int Tick { get; set; } = tick;
    public ulong Timestamp { get; set; } = timeStamp;
    
    public Vector3 Velocity { get; set; } = velocity;
    public Transform3D Transform { get; set; } = transform;
    
    public byte[] Serialize()
    {
        var stream = new StreamPeerBuffer();
        
        stream.PutFloat(Transform.Basis.X.X);
        stream.PutFloat(Transform.Basis.X.Y);
        stream.PutFloat(Transform.Basis.X.Z);

        stream.PutFloat(Transform.Basis.Y.X);
        stream.PutFloat(Transform.Basis.Y.Y);
        stream.PutFloat(Transform.Basis.Y.Z);

        stream.PutFloat(Transform.Basis.Z.X);
        stream.PutFloat(Transform.Basis.Z.Y);
        stream.PutFloat(Transform.Basis.Z.Z);
        
        stream.PutFloat(Transform.Origin.X);
        stream.PutFloat(Transform.Origin.Y);
        stream.PutFloat(Transform.Origin.Z);
        
        stream.PutFloat(Velocity.X);
        stream.PutFloat(Velocity.Y);
        stream.PutFloat(Velocity.Z);
        
        stream.Put32(Tick);
        stream.PutU64(Timestamp);

        return stream.DataArray; 
    }

    public static IReplicationField Deserialize(byte[] data)
    {
        var stream = new StreamPeerBuffer();
        stream.DataArray = data;

        var basisX = new Vector3(stream.GetFloat(), stream.GetFloat(), stream.GetFloat());
        var basisY = new Vector3(stream.GetFloat(), stream.GetFloat(), stream.GetFloat());
        var basisZ = new Vector3(stream.GetFloat(), stream.GetFloat(), stream.GetFloat());

        var origin = new Vector3(stream.GetFloat(), stream.GetFloat(), stream.GetFloat());
        var velocity = new Vector3(stream.GetFloat(), stream.GetFloat(), stream.GetFloat());
        
        var tick = stream.Get32();
        var timeStamp = stream.GetU64();
        
        return new Transform3DField(tick, timeStamp, new Transform3D(new Basis(basisX, basisY, basisZ), origin), velocity);
    }
}