using Godot;
using Godot.Collections;
using ReplicateInator.addons.replicate_inator.scripts.replication_fields;

namespace ReplicateInator.addons.replicate_inator.scripts.replication_components;

public sealed partial class CharacterBody3DReplication : ReplicationComponent
{
    private const int buffersMaxSize = 1000;
    private const float gravity = -50;
    private CircularRingBuffer<Transform3DField> transformBuffer;
    private CharacterBody3D characterBody3D;
    
    public override void _Ready()
    {
        characterBody3D.Position = new Vector3(0,1.5f,0);
    }
    
    public override void Initialize(NodePath targetPath)
    {
        base.Initialize(targetPath);
        characterBody3D = GetNodeOrNull<CharacterBody3D>(targetPath);
        transformBuffer = new CircularRingBuffer<Transform3DField>(buffersMaxSize);
    }
    
    public override bool NeedReconciliation(int reconciliationTick, Array<byte[]> data)
    {
        Transform3DField serverTransform3DField = (Transform3DField) Transform3DField.Deserialize(data[0]);
        
        if (transformBuffer.TryGetAt(reconciliationTick, out Transform3DField clientTransform3DField))
        {
            float distanceFromServer = serverTransform3DField.Transform.Origin.DistanceSquaredTo(clientTransform3DField.Transform.Origin);
            
            if (distanceFromServer >= 0.01)
            {
                GD.Print("recom cialiaton ", distanceFromServer);
                characterBody3D.Position = serverTransform3DField.Transform.Origin;
                characterBody3D.Velocity = serverTransform3DField.Velocity;

                SaveTick(reconciliationTick, clientTransform3DField.Timestamp);

                return true;
            }
        }

        return false;
    }

    public override void ReconciliationTick(int reconciliationTick, float delta, ref IInputReplication input)
    {
        Simulate(delta, reconciliationTick, input.Timestamp, ref input);
    }

    public override void RollbackLocalTick(int rollbackTick, float delta, ref IInputReplication input)
    {
        //usually movement doenst need rollback
    }

    public override Array<byte[]> ConvertStateToBytes(int processTick)
    {
        Array<byte[]> data = [];
        
        if (transformBuffer.TryGetAt(processTick, out Transform3DField transform3D))
        {
            data.Add(transform3D.Serialize());
        }

        if (characterBody3D.Multiplayer.IsServer())
        {
            
        }
        
        return data;
    }
    
    public override void Simulate(float delta, int processTick, ulong timeNow, ref IInputReplication input)
    {
        input.Processed = true;
        ProcessInput(ref input, delta);
        ProcessGravity(delta);
        characterBody3D.MoveAndSlide();
    }

    public override void SaveTickFromData(int processTick, Array<byte[]> data)
    {
        Transform3DField transform3DField = (Transform3DField) Transform3DField.Deserialize(data[0]);
        
        transformBuffer.TrySet(processTick,transform3DField);
    }
    
    public override void SaveTick(int processTick, ulong timeNow)
    { 
        transformBuffer.TrySet(processTick,new Transform3DField(processTick, timeNow, characterBody3D.Transform, characterBody3D.Velocity));
    }
    
    public void ProcessGravity(float delta)
    {
        if (characterBody3D.IsOnFloor()) return;

        characterBody3D.Velocity += new Vector3(0, gravity * delta, 0);
    }
    
    public void ProcessInput(ref IInputReplication input, float delta)
    {
        if (input is InputMovement3D movement3D)
        {
            Vector3 currentVelocity = characterBody3D.Velocity;
            Vector3 increaseVelocity = new Vector3(movement3D.inputDirection.X,0,movement3D.inputDirection.Y) * 10;
            float y = currentVelocity.Y;

            if (characterBody3D.IsOnFloor() && movement3D.jumped)
            {
                y += 20;
            }
            
            characterBody3D.Velocity = new Vector3(-increaseVelocity.X, y, increaseVelocity.Z);
        }else if (input is EmptyInput _)
        {
            Vector3 currentVelocity = characterBody3D.Velocity;
            
            characterBody3D.Velocity = new Vector3(0, currentVelocity.Y, 0);
        }
    }
}