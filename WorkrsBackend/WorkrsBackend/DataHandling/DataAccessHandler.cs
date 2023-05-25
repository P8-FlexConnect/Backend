using Microsoft.AspNetCore.Mvc.TagHelpers.Cache;
using Microsoft.Data.Sqlite;
using System;
using System.Data;
using System.Runtime.ConstrainedExecution;
using WorkrsBackend.DTOs;

namespace WorkrsBackend.DataHandling
{
    public class DataAccessHandler : IDataAccessHandler
    {
        const string sharedConnectionStringName = "Data Source=servicehostSharded.db";
        const string localConnectionStringName = "Data Source=servicehostLocal.db";
        string sharedConnectionString = new SqliteConnectionStringBuilder(sharedConnectionStringName)
        {
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();

        string localConnectionString = new SqliteConnectionStringBuilder(localConnectionStringName)
        {
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();

        SqliteConnection sharedDatabase;
        SqliteConnection localDatabase;

        public DataAccessHandler()
        {
            CreateShardedDatabase();
            CreateLocalDatabase();
        }

        void CreateLocalDatabase()
        {
            localDatabase = new SqliteConnection(localConnectionString);
            localDatabase.Open();
            var command = localDatabase.CreateCommand();

            command.CommandText =
          @"
                CREATE TABLE IF NOT EXISTS serviceTask(
                    tasksId TEXT PRIMARY KEY NOT NULL, 
                    clientId TEXT NOT NULL,
                    name TEXT NOT NULL,
                    description TEXT NOT NULL,
                    dateAdded DATETIME NOT NULL,
                    lastActivity DATETIME NOT NULL,
                    status INTEGER NOT NULL,
                    sourcePath TEXT NOT NULL,
                    backupPath TEXT NOT NULL,
                    resultPath TEXT NOT NULL
                );
            ";
            command.ExecuteNonQuery();

            command.CommandText =
          @"
                CREATE TABLE IF NOT EXISTS locations(
                    id INTEGER PRIMARY KEY,
                    name TEXT NOT NULL,
                    latitude DECIMAL(8,6),
                    longitude DECIMAL(9,6)
                );
            ";
            command.ExecuteNonQuery();

            command.CommandText =
          @"
                DELETE FROM locations
            ";
            command.ExecuteNonQuery();

            command.CommandText =
          @"
                INSERT INTO locations(
                    name,
                    latitude,
                    longitude
                )
                VALUES
                    (
                        'Trekanten Makerspace',
                        57.027710,
                        10.000400
                    ),
                    (
                        'Open Space Aarhus',
                        56.186200,
                        10.188390
                    ),
                    (
                        'Teck-Teket Makerspace',
                        55.729700,
                        12.359700
                    ),
                    (
                        'Silkeborg Makerspace',
                        56.166170,
                        9.546070
                    );
            ";
            command.ExecuteNonQuery();

        }

        void CreateShardedDatabase()
        {
            sharedDatabase = new SqliteConnection(sharedConnectionString);

            sharedDatabase.Open();

            var command = sharedDatabase.CreateCommand();

            command.CommandText =
            @"
                CREATE TABLE IF NOT EXISTS clientdht(
                    userId TEXT PRIMARY KEY NOT NULL,
                    username TEXT NOT NULL,
                    password TEXT NOT NULL,
                    servername TEXT NOT NULL,
                    dataserver TEXT NOT NULL,
                    firstname TEXT NOT NULL,
                    lastname TEXT NOT NULL
                );
            ";
            command.ExecuteNonQuery();

            command.CommandText =
            @"
                CREATE TABLE IF NOT EXISTS workerdht(
                    workerId TEXT PRIMARY KEY NOT NULL,
                    status INTEGER NOT NULL,
                    serverName TEXT NOT NULL,
                    jobId TEXT NOT NULL,
                    lanIP TEXT NOT NULL,
                    ftpUser TEXT NOT NULL,
                    ftpPassword TEXT NOT NULL
                );
            ";
            command.ExecuteNonQuery();

            command.CommandText =
            @"
                CREATE TABLE IF NOT EXISTS serverdht(
                    name TEXT PRIMARY KEY NOT NULL,
                    pairServer TEXT NOT NULL,
                    mode INTEGER NOT NULL
                );
            ";
            command.ExecuteNonQuery();
        }

        public ClientDTO? FindClientByUserName(string username)
        {
            ClientDTO? retval = null;

            var command = sharedDatabase.CreateCommand();
            command.CommandText =
            @"
                    SELECT *
                    FROM clientdht
                    WHERE username = $username;
                ";
            command.Parameters.AddWithValue("$username", username);

            using (var reader = command.ExecuteReader())
            {
                if(reader.Read())
                {
                    retval = new ClientDTO(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6));
                }
            }

            return retval;
        }

        public Dictionary<Guid, ClientDTO> GetClientDHT()
        {
            Dictionary<Guid, ClientDTO> retval = new();

            var command = sharedDatabase.CreateCommand();
            command.CommandText =
            @"
                    SELECT *
                    FROM clientdht
                ";
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    retval.Add(reader.GetGuid(0), new ClientDTO(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6)));
                }
            }

            return retval;
        }

        public void UpdateClientDHT(ClientDTO client)
        {
            var command = sharedDatabase.CreateCommand();
            command.CommandText =
            @"
                    UPDATE clientdht
                    SET servername = $servername, username = $username, dataserver = $dataserver, firstname = $firstname, lastname = $lastname
                    WHERE userId = $clientId
                ";

            command.Parameters.AddWithValue("$clientId", client.ClientId);
            command.Parameters.AddWithValue("$username", client.Username);
            command.Parameters.AddWithValue("$servername", client.ServerName);
            command.Parameters.AddWithValue("$dataserver", client.DataServer);
            command.Parameters.AddWithValue("firstname", client.Firstname);
            command.Parameters.AddWithValue("$lastname", client.Lastname);

            command.ExecuteNonQuery();
        }

        public void AddClientToClientDHT(ClientDTO client)
        {
            var command = sharedDatabase.CreateCommand();
            command.CommandText =
            @"
                    INSERT INTO clientdht (userId, username, password, servername, dataserver, firstname, lastname)
                    VALUES ($clientId, $username, $password, $servername, $dataserver, $firstname, $lastname);
                ";

            command.Parameters.AddWithValue("$clientId", client.ClientId);
            command.Parameters.AddWithValue("$username", client.Username);
            command.Parameters.AddWithValue("$password", client.Password);
            command.Parameters.AddWithValue("$servername", client.ServerName);
            command.Parameters.AddWithValue("$dataserver", client.DataServer);
            command.Parameters.AddWithValue("$firstname", client.Firstname);
            command.Parameters.AddWithValue("$lastname", client.Lastname);

            command.ExecuteNonQuery();
        }

        public void DeleteClientFromClientDHT(Guid id)
        {
            var command = sharedDatabase.CreateCommand();
            command.CommandText =
            @"
                    DELETE from clientdht
                    WHERE userId = $clientId;
                ";

            command.Parameters.AddWithValue("$clientId", id);
            command.ExecuteNonQuery();
        }

        public Dictionary<Guid, WorkerDTO> GetWorkerDHT()
        {
            Dictionary<Guid, WorkerDTO> retval = new();

            var command = sharedDatabase.CreateCommand();
            command.CommandText =
            @"
                    SELECT *
                    FROM workerdht
                ";
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    retval.Add(reader.GetGuid(0), new WorkerDTO(
                        reader.GetGuid(0), 
                        (WorkerStatus)reader.GetInt32(1), 
                        reader.GetString(2), 
                        Guid.Parse(reader.GetString(3)),
                        reader.GetString(4),
                        reader.GetString(5),
                        reader.GetString(6)
                        ));
                }
            }

            return retval;
        }

        public void UpdateWorkerDHT(WorkerDTO worker)
        {
            var command = sharedDatabase.CreateCommand();
            command.CommandText =
            @"
                    UPDATE workerdht
                    SET status = $status, serverName = $serverName, jobId = $jobId, lanIP = $lanIP, ftpUser = $ftpUser, ftpPassword = $ftpPassword
                    WHERE workerId = $workerId
                ";

            command.Parameters.AddWithValue("$workerId", worker.WorkerId);
            command.Parameters.AddWithValue("$status", (int)worker.Status);
            command.Parameters.AddWithValue("$serverName", worker.ServerName);
            command.Parameters.AddWithValue("$jobId", worker.JobId);
            command.Parameters.AddWithValue("$lanIP", worker.LANIp);
            command.Parameters.AddWithValue("$ftpUser", worker.FTPUser);
            command.Parameters.AddWithValue("$ftpPassword", worker.FTPPassword);
            command.ExecuteNonQuery();
        }

        public void AddWorkerToWorkerDHT(WorkerDTO worker)
        {
            var command = sharedDatabase.CreateCommand();
            command.CommandText =
            @"
                    INSERT INTO workerdht (workerId, status, serverName, jobId, lanIP, ftpUser, ftpPassword)
                    VALUES ($workerId, $status, $serverName, $jobId, $lanIP, $ftpUser, $ftpPassword);
                ";

            command.Parameters.AddWithValue("$workerId", worker.WorkerId);
            command.Parameters.AddWithValue("$status", (int)worker.Status);
            command.Parameters.AddWithValue("$serverName", worker.ServerName);
            command.Parameters.AddWithValue("$jobId", worker.JobId);
            command.Parameters.AddWithValue("$lanIP", worker.LANIp);
            command.Parameters.AddWithValue("$ftpUser", worker.FTPUser);
            command.Parameters.AddWithValue("$ftpPassword", worker.FTPPassword);
            command.ExecuteNonQuery();
        }

        public Dictionary<string, ServerDTO> GetPrimaryServers()
        {
            Dictionary<string, ServerDTO> retval = new();

            var command = sharedDatabase.CreateCommand();
            command.CommandText =
            @"
                    SELECT *
                    FROM serverdht
                    WHERE mode = 0
                ";
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    retval.Add(reader.GetString(0), new ServerDTO(reader.GetString(0), reader.GetString(1), (ServerMode)reader.GetInt32(1)));
                }
            }

            return retval;
        }

        public ServerDTO? GetServerInfo(string serverName)
        {
            ServerDTO? server = null;
            var command = sharedDatabase.CreateCommand();
            command.CommandText =
            @"
                    SELECT *
                    FROM serverdht
                    WHERE name = $name
                ";
            command.Parameters.AddWithValue("$name", serverName);
            using (var reader = command.ExecuteReader())
            {
                if(reader.Read())
                {
                    server = new ServerDTO(reader.GetString(0), reader.GetString(1), (ServerMode)reader.GetInt32(2));
                }
            }
            return server;
        }

        public void AddServerToServerDHT(ServerDTO server)
        {
            var command = sharedDatabase.CreateCommand();
            command.CommandText =
            @"
                    INSERT INTO serverdht (name, pairServer, mode)
                    VALUES ($name, $pairServer, $mode);
                ";

            command.Parameters.AddWithValue("$name", server.Name);
            command.Parameters.AddWithValue("$pairServer", server.PairServer);
            command.Parameters.AddWithValue("$mode", server.Mode);

            command.ExecuteNonQuery();
        }

        public void UpdateServerDHT(ServerDTO server)
        {
            var command = sharedDatabase.CreateCommand();
            command.CommandText =
            @"
                    UPDATE serverdht
                    SET name = $name, pairServer = $pairServer, mode = $mode
                    WHERE name = $name
                ";

            command.Parameters.AddWithValue("$name", server.Name);
            command.Parameters.AddWithValue("$pairServer", server.PairServer);
            command.Parameters.AddWithValue("$mode", server.Mode);

            command.ExecuteNonQuery();
        }

        public void AddTask(ServiceTaskDTO task)
        {
            var command = localDatabase.CreateCommand();
            command.CommandText =
            @"
                    INSERT INTO serviceTask 
                    (tasksId, name, description, dateAdded, lastActivity, clientId, status, sourcePath, backupPath, resultPath)
                    VALUES 
                    ($tasksId, $name, $description, $dateAdded, $lastActivity, $clientId, $status, $sourcePath, $backupPath, $resultPath);
                ";
            command.Parameters.AddWithValue("$tasksId", task.Id);
            command.Parameters.AddWithValue("$name", task.Name);
            command.Parameters.AddWithValue("$description", task.Description);
            command.Parameters.AddWithValue("$dateAdded", task.DateAdded);
            command.Parameters.AddWithValue("$lastActivity", task.LastActivity);
            command.Parameters.AddWithValue("$clientId", task.ClientId);
            command.Parameters.AddWithValue("$status", task.Status);
            command.Parameters.AddWithValue("$sourcePath", task.SourcePath);
            command.Parameters.AddWithValue("$backupPath", task.BackupPath);
            command.Parameters.AddWithValue("$resultPath", task.ResultPath);

            command.ExecuteNonQuery();
        }

        public void UpdateTask(ServiceTaskDTO task) 
        {
            var command = localDatabase.CreateCommand();
            command.CommandText =
            @"
                    UPDATE serviceTask
                    SET 
                    name = $name, 
                    description = $description,
                    dateAdded = $dateAdded,
                    lastActivity = $lastActivity,
                    clientId = $clientId, 
                    status = $status,
                    sourcePath = $sourcePath,
                    backupPath = $backupPath,
                    resultPath = $resultPath
                    WHERE tasksId = $tasksId
                ";

            command.Parameters.AddWithValue("$name", task.Name);
            command.Parameters.AddWithValue("$description", task.Description);
            command.Parameters.AddWithValue("$dateAdded", task.DateAdded);
            command.Parameters.AddWithValue("$lastActivity", task.LastActivity);
            command.Parameters.AddWithValue("$clientId", task.ClientId);
            command.Parameters.AddWithValue("$status", task.Status);
            command.Parameters.AddWithValue("$sourcePath", task.SourcePath);
            command.Parameters.AddWithValue("$backupPath", task.BackupPath);
            command.Parameters.AddWithValue("$resultPath", task.ResultPath);
            command.Parameters.AddWithValue("$tasksId", task.Id);

            command.ExecuteNonQuery();
        }

        public ServiceTaskDTO? GetTaskFromId(Guid taskId)
        {
            ServiceTaskDTO? retVal = null;
            var command = localDatabase.CreateCommand();

            command.CommandText =
            @"
                    SELECT *
                    FROM serviceTask
                    WHERE tasksId = $tasksId
                ";
            command.Parameters.AddWithValue("$tasksId", taskId);
            using (var reader = command.ExecuteReader())
            {
                if (reader.Read())
                {
                    retVal = new ServiceTaskDTO(
                        reader.GetGuid(0), 
                        reader.GetGuid(1), 
                        reader.GetString(2),
                        reader.GetString(3),
                        reader.GetDateTime(4),
                        reader.GetDateTime(5),
                        (ServiceTaskStatus)reader.GetInt32(6),
                        reader.GetString(7),
                        reader.GetString(8),
                        reader.GetString(9));
                }
            }

            return retVal;
        }

        public List<LocationDTO> GetLocations()
        {
            List<LocationDTO> retVal = new();
            var command = localDatabase.CreateCommand();
            command.CommandText =
           @"
                    SELECT *
                    FROM locations
                ";

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read()) 
                {
                    retVal.Add(new LocationDTO(
                        reader.GetString(1),
                        reader.GetDecimal(2),
                        reader.GetDecimal(3)));
                }
            }
            return retVal;
        }

        public List<ServiceTaskDTO> GetTaskForClient(Guid clientId)
        {
            List<ServiceTaskDTO> retVal = new List<ServiceTaskDTO>();
            var command = localDatabase.CreateCommand();

            command.CommandText =
            @"
                    SELECT *
                    FROM serviceTask
                    WHERE clientId = $clientId
                ";
            command.Parameters.AddWithValue("$clientId", clientId);
            using (var reader = command.ExecuteReader())
            {
                while(reader.Read())
                {
                    retVal.Add(new ServiceTaskDTO(
                        reader.GetGuid(0),
                        reader.GetGuid(1),
                        reader.GetString(2),
                        reader.GetString(3),
                        reader.GetDateTime(4),
                        reader.GetDateTime(5),
                        (ServiceTaskStatus)reader.GetInt32(6),
                        reader.GetString(7),
                        reader.GetString(8),
                        reader.GetString(9)));
                }                    
            }

            return retVal;
        }

        public List<ServiceTaskDTO> GetTasksFromStatus(ServiceTaskStatus status)
        {
            List<ServiceTaskDTO> retval = new();

            var command = localDatabase.CreateCommand();
            command.CommandText =
            @"
                    SELECT *
                    FROM serviceTask
                    WHERE status = $status
                ";

            command.Parameters.AddWithValue("$status", (int)status);

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                   retval.Add(new ServiceTaskDTO(
                        reader.GetGuid(0),
                        reader.GetGuid(1),
                        reader.GetString(2),
                        reader.GetString(3),
                        reader.GetDateTime(4),
                        reader.GetDateTime(5),
                        (ServiceTaskStatus)reader.GetInt32(6),
                        reader.GetString(7),
                        reader.GetString(8),
                        reader.GetString(9)));
                }
            }
            return retval;
        }

    }
}
