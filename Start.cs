using Godot;
using System.Linq;


public partial class Start : Node3D
{

    [Export] ENetMultiplayerPeer peer = null;
    [Export] public Node charactersNode = null;

    private const string Address = "127.0.0.1";
    private const int Port = 12345;

    public override void _Ready()
    {
        base._Ready();

        string[] args = OS.GetCmdlineArgs();

        if (args.Contains("--role"))
        {
            int index = System.Array.IndexOf(args, "--role");

            if (index + 1 < args.Length)
            {
                string role = args[index + 1];

                if (role == "server")
                {
                    ENetMultiplayerPeer peer = new();

                    Error result = peer.CreateServer(Port, 32);

                    if (result != Error.Ok)
                    {
                        return;
                    }

                    this.peer = peer;

                    Multiplayer.MultiplayerPeer = this.peer;
                    Multiplayer.PeerConnected += NewPeer;
                }
                else
                {
                    ENetMultiplayerPeer peer = new();

                    Error result = peer.CreateClient(Address, Port);

                    if (result != Error.Ok)
                    {
                        GD.Print("Error trying to connect to server");
                        return;
                    }

                    this.peer = peer;
        
                    Multiplayer.MultiplayerPeer = this.peer;
                }
                
                PingPong pong = new();
                
                AddChild(pong);
            }
        }
        else
        {
            GD.Print("Nenhum argumento --role foi passado.");
        }
    }

    public void NewPeer(long uuid)
    {
        teste mainCharacter = GD.Load<PackedScene>("res://character_body_3d.tscn").Instantiate<teste>();
        
        mainCharacter.Name = "" + uuid; 
        mainCharacter.SetMultiplayerAuthority((int)uuid);
        
        charactersNode.AddChild(mainCharacter);
    }
}
