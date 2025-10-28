#if TOOLS

using Godot;
using Godot.Collections;
using System;
using ReplicateInator.addons.replicate_inator.scripts;


[Tool]
[GlobalClass]
public partial class replicate_inator : EditorPlugin
{
    private Texture2D _icon;
    
    public override void _EnterTree()
    {
       
        _icon = GD.Load<Texture2D>("res://addons/replicate_inator/icons/replicator_component.png");

        AddCustomType(
            "Replicator",           
            "Node",                 
            GD.Load<Script>("res://addons/replicate_inator/scripts/Replicator.cs"),  
            _icon                
        );
        
        AddCustomType(
            "CharacterBody3DReplication",           
            "Node",                 
            GD.Load<Script>("res://addons/replicate_inator/scripts/replication_components/CharacterBody3DReplication.cs"),  
            _icon                
        );
    }
    

    public override void _ExitTree()
    {
        RemoveCustomType("Replicator");
        RemoveCustomType("CharacterBody3DReplication");
    }
}

public enum EAuthorityType
{
    ServerOwner,
    ClientOwner
}

public enum EReplicationType
{
    Interpolated,
    Extrapolated,
    InterpolatedAndExtrapolated
}

public enum EProcessType
{
    None,
    
    Server,
    ServerAuthoritative,
    
    ClientPredicted,
    ClientInterpolated,
    ClientExtrapolated,
    ClientInterpolatedAndExtrapolated
}

#endif
