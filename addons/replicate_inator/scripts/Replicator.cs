using System.Collections.Generic;
using System.Linq;
using Godot;
using Godot.Collections;
using ReplicateInator.addons.replicate_inator.scripts.replication_fields;
using ReplicateInator.addons.replicate_inator.scripts.replication_processes;

namespace ReplicateInator.addons.replicate_inator.scripts
{
    public partial class Replicator : Node
    {
        [Export] private EAuthorityType authorityType;
        [Export] private NodePath targetPath;
        [Export] private EReplicationType replicationType;
        [Export] private Array<ReplicationComponent> replicationComponents = [];
        [Export] private int buffersMaxSize = 1000;
        [Export] public bool rollback;
        
        private int minimunTickOffset = 10;
        private EProcessType processType;
        private Node replicationNode;
        private float deltaTicks = 1.0f / Engine.PhysicsTicksPerSecond;
        private float tickDeltaMs = 1000.0f / Engine.PhysicsTicksPerSecond;
        private int tick;
        private int networkId;
        public bool initialized;
        private bool firstDataReceived;
        private IProcess replicationProcess;
        private PingPong pingPong;
        private CircularRingBuffer<IInputReplication> inputsBuffer;
        
        // Server Vars //
        [Export] public int maxInputsToServerStore = 150;
        [Export] public int maxTickOffset = 10;
        
        private System.Collections.Generic.Dictionary<int, IInputReplication> clientInputsToProcess = new();
        public int currentClientInputToProcess = -1;
        public int lastClientTickProcessed = -1;
         
        // Client vars //    
        [Export] public int maxLocalInputsToProcess = 50;
        [Export] public int maxLocalInputsWaitingToReconstitute = 50;
        [Export] public int maxLocalReconciliationsAmount = 50;
        [Export] public int minInterpolationTicksOffset = 5;
        [Export] public int maxInterpolationTicksOffset = 20;
        [Export] private NodePath targetVisualPath;
        
        public int lastLocalReconcilatedTick = -1;
        public int nextLocalReconcilationTick = -1;

        public int currentInterpolationTick = -1;
        public int newestInterpolationTickFromServer = -1;
        public Array<int> interpolationTicksOrder = [];
        
        private Queue<IInputReplication> localInputsToProcess = [];
        private Queue<int> localInputsNotConfirmed = new();
        private System.Collections.Generic.Dictionary<int, bool> localInputsConfirmed = [];
        private System.Collections.Generic.Dictionary<int, IInputReplication> localInputsWaitingToReconstitute = new();
        private System.Collections.Generic.Dictionary<int, Array<Array<byte[]>>> localReconciliationDictionary = new();
        
        public int Tick { get  => tick; set => tick = value; }

        public Queue<IInputReplication> LocalInputsToProcess
        {
            get {return localInputsToProcess;}
        }

        public CircularRingBuffer<IInputReplication> InputsBuffer
        {
            get { return inputsBuffer; }
        }
        
        public Godot.Collections.Array<ReplicationComponent> ReplicationComponents
        {
            get { return replicationComponents; }
        }
        
        public System.Collections.Generic.Dictionary<int, Array<Array<byte[]>>> LocalReconciliationDictionary
        {
            get { return localReconciliationDictionary; }
        }

        public System.Collections.Generic.Dictionary<int, IInputReplication> ClientInputsToProcess
        {
            get { return clientInputsToProcess; }
        }
        
        public Queue<int> LocalInputsNotConfirmed
        {
            get { return localInputsNotConfirmed; }
        }
        
        public System.Collections.Generic.Dictionary<int, bool> LocalInputsConfirmed
        {
            get { return localInputsConfirmed; }
        }
        
        public System.Collections.Generic.Dictionary<int, IInputReplication> LocalInputsWaitingToReconstitute
        {
            get { return localInputsWaitingToReconstitute; }
        }

        public PingPong PingPong
        {
            get {return pingPong;}
        }

        public int NetworkId
        {
            get { return networkId; }
        }

        public int MinimunTickOffset
        {
            get { return minimunTickOffset; }
        }

        public float TickDeltaMs
        {
            get { return tickDeltaMs; }
        }

        public override void _Ready()
        {
            base._Ready();

            inputsBuffer = new CircularRingBuffer<IInputReplication>(buffersMaxSize);
            
            foreach (var replicationComponent in replicationComponents)
            {
                replicationComponent.Initialize(targetPath, targetVisualPath);
            } 
            
            SaveTick(tick, Time.GetTicksMsec(), default);
            SetPhysicsProcess(true);
            
            tick += 1;
        }

        public override void _EnterTree()
        {
            base._EnterTree();
            
            ulong timeNow = Time.GetTicksMsec();
            
            replicationNode = GetNodeOrNull<Node>(targetPath);

            if (replicationNode == null)
            {
                GD.PushWarning("Node to replicate not selected");
                return;
            }

            Name = replicationNode.Name;
            
            ProcessPhysicsPriority = replicationNode.ProcessPhysicsPriority + 1;
            ProcessPriority = replicationNode.ProcessPriority + 1;
            
            networkId = replicationNode.GetMultiplayerAuthority();
            
            if (Multiplayer.IsServer())
            {
                switch (authorityType)
                {
                    case EAuthorityType.ServerOwner:
                        processType = EProcessType.ServerAuthoritative;
                        SetMultiplayerAuthority(1);
                        break;
                    case EAuthorityType.ClientOwner:
                        processType = EProcessType.Server;
                        SetMultiplayerAuthority(networkId);
                        break;
                }

                if (ReplicateGlobalObjects.pingPong != null)
                { 
                    pingPong = ReplicateGlobalObjects.pingPong;
                    initialized = true;
                } 
            }
            else
            {
                switch (authorityType)
                {
                    case EAuthorityType.ServerOwner:
                        SetMultiplayerAuthority(1);
                        break;
                    case EAuthorityType.ClientOwner:
                        RpcId(1, nameof(RequestMultiplayerAuthority));
                        break;
                }
            }
        }

        public override void _Process(double delta)
        {
            base._Process(delta);
            
            if (!initialized && ReplicateGlobalObjects.pingPong != null)
            {
                if (Multiplayer.IsServer())
                {
                    pingPong = ReplicateGlobalObjects.pingPong;
                    initialized = true;
                }else if (firstDataReceived)
                {
                    pingPong = ReplicateGlobalObjects.pingPong;
                    initialized = true;
                }
            }
            
            CreateReplicationProcess();
            
            if (replicationProcess == null || !initialized) return;
            
            replicationProcess.Process(this);
        }

        public override void _PhysicsProcess(double delta)
        {
            base._PhysicsProcess(delta);
            
            CreateReplicationProcess();
            
            if (replicationProcess == null || !initialized) return;

            ulong timeNow = Time.GetTicksMsec();
            
            replicationProcess.PhysicsProcess(this, (float) delta, tick, timeNow);
        }

        public void SaveTick(int processTick, ulong timeNow, IInputReplication input)
        {
            inputsBuffer.Enqueue(input);
            
            foreach (var replicationComponent in replicationComponents)
            {
                replicationComponent.SaveTick(processTick, timeNow);
            }
        }

        private void CreateReplicationProcess()
        {
            if (replicationProcess != null || !initialized) return;

            switch (processType)
            {
                case EProcessType.Server:
                    replicationProcess = new ServerProcess();
                    break;
                case EProcessType.ServerAuthoritative:
                    replicationProcess = new ServerAuthoritativeProcess();
                    break;
                case EProcessType.ClientPredicted:
                    replicationProcess = new ClientPredictedProcess();
                    break;
                case EProcessType.ClientInterpolated:
                    replicationProcess = new ClientInterpolatedProcess();
                    break;
                case EProcessType.ClientExtrapolated:
                    replicationProcess = new ClientExtrapolatedProcess();
                    break;
                case EProcessType.ClientInterpolatedAndExtrapolated :
                    replicationProcess = new ClientInterpolatedAndExtrapolatedProcess();
                    break;
            }
        }
        
        //Here we are requesting the Node multiplayer authority since godot doenst auto replicate it for us
        
        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        private void RequestMultiplayerAuthority()
        {
            RpcId(Multiplayer.GetRemoteSenderId(), nameof(ReceiveMultiplayerAuthority), networkId);
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
        private void ReceiveMultiplayerAuthority(int networkId)
        {
            this.networkId = networkId;

            if (this.networkId == Multiplayer.GetUniqueId())
            {
                processType = EProcessType.ClientPredicted;
            }
            else
            {
                switch (replicationType)
                {
                    case EReplicationType.Interpolated:
                        processType = EProcessType.ClientInterpolated;
                        break;
                    case EReplicationType.Extrapolated:
                        processType = EProcessType.ClientExtrapolated;
                        break;
                    case EReplicationType.InterpolatedAndExtrapolated:
                        processType = EProcessType.ClientInterpolatedAndExtrapolated;
                        break;
                }
            }

            firstDataReceived = true;
        }
        
        //Here we can deal with inputs
        
        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
        public void SentLocalInput(byte[] data)
        {
            IInputReplication input = InputRegistry.Deserialize(data);
            int inputTick = input.Tick;
            ulong inputTimeStamp = input.Timestamp;
            
            if (!input.IsValidInput())
            {
                input = new EmptyInput(inputTick, inputTimeStamp);
            }

            RpcId(networkId, nameof(ConfirmLocalInput), inputTick, inputTick > lastClientTickProcessed, false);

            if (clientInputsToProcess.TryGetValue(inputTick, out var _))
            {
                return;
            }
            
            clientInputsToProcess.Add(inputTick,input);

            if (currentClientInputToProcess == -1 || inputTick < currentClientInputToProcess)
            {
                currentClientInputToProcess = inputTick;
            }
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
        public void ReconstituteLocalInput(byte[] data)
        {
            IInputReplication input = InputRegistry.Deserialize(data);
            int inputTick = input.Tick;
            ulong inputTimeStamp = input.Timestamp;
            
            if (!input.IsValidInput())
            {
                input = new EmptyInput(inputTick, inputTimeStamp);
            }

            if (inputTick <= lastClientTickProcessed || clientInputsToProcess.TryGetValue(inputTick, out var _))
            {
                return;
            }
            
            RpcId(networkId, nameof(ConfirmLocalInput), inputTick, true, true);
            
            clientInputsToProcess.Add(inputTick,input);

            if (currentClientInputToProcess == -1 || inputTick < currentClientInputToProcess)
            {
                currentClientInputToProcess = inputTick;
            }
        }

        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
        public void ConfirmLocalInput(int confirmTick, bool processed, bool isReconcilationInput)
        {
            if (isReconcilationInput)
            {
                if (localInputsWaitingToReconstitute.TryGetValue(confirmTick, out IInputReplication input))
                {
                    inputsBuffer.TrySet(confirmTick, input);
                    localInputsWaitingToReconstitute.Remove(confirmTick);
                }
            }
            else
            {
                localInputsConfirmed.Add(confirmTick, processed);
            }
        }
        
        // Here we deal with reconciliation //
        
        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
        public void UpdateLocalClientReconciliationStates(Array<Array<byte[]>> updateArray, int clientTick)
        {
            if (clientTick < lastLocalReconcilatedTick)
            {
                for (int i = 0; i < updateArray.Count; i++)
                {
                    replicationComponents[i].SaveTickFromData(clientTick, updateArray[i],false);
                }
                return;
            }

            if (updateArray[0].Count == 0)
            {
                GD.Print("0 bro");
            }
            
            localReconciliationDictionary.Add(clientTick, updateArray);

            if (nextLocalReconcilationTick == -1 || clientTick < nextLocalReconcilationTick)
            {
                nextLocalReconcilationTick = clientTick;
            }
        }
        
        // Here we do with non local replication, bassicaly receiving informations about others clients / server owner only data //
        [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
        public void UpdateNonLocalReplicationStates(Array<Array<byte[]>> updateArray, int serverTick)
        {
            int newClientTick = serverTick + minInterpolationTicksOffset;

            if (newClientTick < tick)
            {
                for (int i = 0; i < updateArray.Count; i++)
                {
                    replicationComponents[i].SaveTickFromData(newClientTick, updateArray[i],false);
                }
                
                return;
            }
            
            ulong timeNow = Time.GetTicksMsec();

            if (serverTick > newestInterpolationTickFromServer)
            {
                newestInterpolationTickFromServer = serverTick;

                for (int i = tick; i < newClientTick; i ++ )
                {
                    foreach (var component in replicationComponents)
                    {
                        component.SaveTick(i, timeNow);
                    }
                }
                
                for (int i = 0; i < updateArray.Count; i++)
                {
                    replicationComponents[i].ApplyReplicatedServerData(serverTick + minInterpolationTicksOffset, updateArray[i], true);
                }
            }

            if (currentInterpolationTick == -1 || newClientTick < currentInterpolationTick)
            {
                currentInterpolationTick = newClientTick;
            }
            
            interpolationTicksOrder.Add(newClientTick);
            interpolationTicksOrder.Sort();

            int difference = newClientTick - tick;

            if (difference > maxInterpolationTicksOffset)
            {
                GD.Print("too biig diff");
                tick = newClientTick - minInterpolationTicksOffset;
            }
        }
    }
    
}

