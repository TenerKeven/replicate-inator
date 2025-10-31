namespace ReplicateInator.addons.replicate_inator.scripts
{
    public interface IReplicationField
    {
        public int Tick { get; set; }
        public ulong Timestamp { get; set; }
        public bool FromServer { get; set; }

        public abstract byte[] Serialize();
        public static abstract IReplicationField Deserialize(byte[] data);
    }
}

