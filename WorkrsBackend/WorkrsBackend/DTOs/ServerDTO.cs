namespace WorkrsBackend.DTOs
{
    public enum ServerMode
    {
        Primary,
        Secondary
    }

    public class ServerDTO
    {
        public string Name { get; set; }
        public string PairServer { get; set; }
        public ServerMode Mode { get; set; }
        public ServerDTO(string name, string pairServer, ServerMode mode)
        {
            Name = name;
            PairServer = pairServer;
            Mode = mode;
        }
    }
}
