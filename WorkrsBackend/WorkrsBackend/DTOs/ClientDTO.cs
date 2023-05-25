using RabbitMQ;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace WorkrsBackend.DTOs
{
    public class ClientDTO
    {
        public Guid ClientId { get; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string ServerName { get; set; }
        public string DataServer { get; }
        public string Firstname { get; }
        public string Lastname { get; }


        public ClientDTO(Guid clientId, string username, string password, string serverName, string dataServer, string firstname, string lastname)
        {
            ClientId = clientId;
            Username = username;
            Password = password;
            ServerName = serverName;
            DataServer = dataServer;
            Firstname= firstname;
            Lastname= lastname;
        }
    }
}