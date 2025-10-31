using Godot;
using ReplicateInator.addons.replicate_inator.scripts;
using ReplicateInator.addons.replicate_inator.scripts.replication_fields;

public partial class teste : CharacterBody3D
{
    [Export] public int lol = 0;
    [Export] Replicator replicator = null;

    public override void _Ready()
    {
        SetPhysicsProcess(true);
    }
    
    public static IInputReplication GetMovementInput()
    {
        Vector2I inputDirection = Vector2I.Zero;
        bool jumped = false;

        if (Input.IsActionPressed("move_forward"))
            inputDirection.Y += 1;
        if (Input.IsActionPressed("move_backward"))
            inputDirection.Y -= 1;
        if (Input.IsActionPressed("move_left"))
            inputDirection.X += 1;
        if (Input.IsActionPressed("move_right"))
            inputDirection.X -= 1;
        if (Input.IsActionPressed("jump"))
            jumped = true;
        
        return inputDirection == Vector2I.Zero && !jumped ? new EmptyInput(default, default) : new InputMovement3D(default, default, inputDirection, jumped);
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        
        replicator.LocalInputsToProcess.Enqueue(GetMovementInput());
    } 
}
