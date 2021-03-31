using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using HeyRed.Mime;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace BackupMySQLGoogleDrive
{
    class Program
    {
        static string[] Scopes = { DriveService.Scope.DriveReadonly };
        static string ApplicationName = "Backup MySQL To Google Drive";
        static void Main(string[] args)
        {
            //string constring = "server=localhost;user=root;pwd=;database=phr;";

            //// Important Additional Connection Options
            //constring += "charset=utf8;convertzerodatetime=true;";

            //string filePath = "D:\\backup.sql";

            //using (MySqlConnection conn = new MySqlConnection(constring))
            //{
            //    using (MySqlCommand cmd = new MySqlCommand())
            //    {
            //        using (MySqlBackup mb = new MySqlBackup(cmd))
            //        {
            //            cmd.Connection = conn;
            //            conn.Open();
            //            mb.ExportToFile(filePath);
            //            conn.Close();
            //        }
            //    }
            //}



            //google api
            UserCredential credential;
            string[] scopes = new string[] { DriveService.Scope.Drive,
                             DriveService.Scope.DriveFile};
            var client = new ClientSecrets
            {
                ClientId = "xxxx",
                ClientSecret = "xxx"
            };

            //using (var stream = new FileStream("credentials.json", FileMode.Open, FileAccess.Read))
            //{
            // The file token.json stores the user's access and refresh tokens, and is created
            // automatically when the authorization flow completes for the first time.
            string credPath = "token.json";
            credential = GoogleWebAuthorizationBroker.AuthorizeAsync(client, scopes, Environment.UserName, CancellationToken.None, new FileDataStore("MyAppsToken")).Result;
            Console.WriteLine("Credential file saved to: " + credPath);
            //}

            // Create Drive API service.
            var service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });

            service.HttpClient.Timeout = TimeSpan.FromMinutes(100);
            //Long Operations like file uploads might timeout. 100 is just precautionary value, can be set to any reasonable value depending on what you use your service for  

            // team drive root https://drive.google.com/drive/folders/0AAE83zjNwK-GUk9PVA   

            var respocne = uploadFile(service, "D:\\backup.sql", "data phuchaireal.com");
            Console.Read();



        }


        static Google.Apis.Drive.v3.Data.File uploadFile(DriveService _service, string _uploadFile, string _descrp)
        {
            if (System.IO.File.Exists(_uploadFile))
            {
                Google.Apis.Drive.v3.Data.File body = new Google.Apis.Drive.v3.Data.File();
                body.Name = System.IO.Path.GetFileName(_uploadFile);
                body.Description = _descrp;
                body.MimeType = MimeTypesMap.GetMimeType(_uploadFile);
                // body.Parents = new List<string> { _parent };// UN comment if you want to upload to a folder(ID of parent folder need to be send as paramter in above method)
                byte[] byteArray = System.IO.File.ReadAllBytes(_uploadFile);
                System.IO.MemoryStream stream = new System.IO.MemoryStream(byteArray);
                try
                {
                    FilesResource.CreateMediaUpload request = _service.Files.Create(body, stream, MimeTypesMap.GetMimeType(_uploadFile));
                    request.SupportsTeamDrives = true;
                    request.Upload();
                    return request.ResponseBody;
                }
                catch (Exception e)
                {
                    Console.WriteLine("error:" + e.Message);
                    return null;
                }
            }
            else
            {
                Console.WriteLine("The file does not exist.");
                return null;
            }
        }

    }
}
