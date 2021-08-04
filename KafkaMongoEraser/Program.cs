using System;

using MongoDB.Bson;
using MongoDB.Driver;

using Confluent.Kafka.Admin;
using System.Threading.Tasks;
using Confluent.Kafka;
using System.Security.Cryptography.X509Certificates;
using System.Security.Authentication;
using System.Net.Security;

namespace KafkaMongoEraser
{
    internal class Program
    {
        private static readonly string[] topicsName = new string[] { "articles", "books", "categories", "tree", "collections", "home", "category-filter-articles", "category-filter-books" };
        private const string topicsBaseName = "wly.glb.dummies.dummies-";
        private const string kafkaServer = "localhost:9092"; //ip-pl-kfkbq01.aws.wiley.com:9092

        public static async Task Main(string[] args)
        {
            bool executeForMongo = true;
            bool executeForKafka = false;

            if (executeForMongo)
            {
                string[] MongoCollections = new string[] { "articles", "books", "categories", "collections", "tree" };

                var client = new MongoClient("mongodb://localhost:27018");
                //var client = new MongoClient("SERVER_CONNECTION_STRING");
                //client = new MongoClient(SetupSettingsMongo());

                var database = client.GetDatabase("dummies_v2");

                foreach (string collection in MongoCollections)
                {
                    database.ListCollectionNames();
                    //database.DropCollection(collection);
                }
            }

            if (executeForKafka)
            {
                await DeleteTopicsAsync();
                await CreateTopicsAsync();
                //kafka-topics --bootstrap-server ip-pl-kfkbq01.aws.wiley.com:9092 --delete --topic wly.glb.dummies.dummies-category-filter-books
                //kafka-topics --bootstrap-server ip-pl-kfkbq01.aws.wiley.com:9092 --delete --topic wly.glb.dummies.dummies-category-filter-articles

                //kafka-topics --bootstrap-server ip-pl-kfkbq01.aws.wiley.com:9092 --create --topic wly.glb.dummies.dummies-category-filter-books
                //kafka-topics --bootstrap-server ip-pl-kfkbq01.aws.wiley.com:9092 --create --topic wly.glb.dummies.dummies-category-filter-articles
            }
        }

        private static MongoClientSettings SetupSettingsMongo()
        {
            string CERT_PATH = "cert_path";
            string URL_CONNECTION = "url_connection";
            string CERT_HASH = "cert_hash";

            using X509Store localTrustStore = new(StoreName.Root);
            var certificateCollection = new X509Certificate2Collection();
            certificateCollection.ImportFromPem(CERT_PATH);
            localTrustStore.Open(OpenFlags.ReadWrite);
            localTrustStore.AddRange(certificateCollection);

            var settings = MongoClientSettings.FromUrl(new MongoUrl(URL_CONNECTION));
            settings.SslSettings = new SslSettings
            {
                EnabledSslProtocols = SslProtocols.Tls12,
                ServerCertificateValidationCallback = delegate (object s, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
                {
                    return certificate.GetCertHashString() == CERT_HASH;
                }
            };
            return settings;
        }

        private static async Task CreateTopicsAsync()
        {
            using (var adminClient = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = kafkaServer }).Build())
            {
                foreach (string topic in topicsName)
                {
                    string topicName = topicsBaseName + topic;
                    try
                    {
                        await adminClient.CreateTopicsAsync(new TopicSpecification[] {
                        new TopicSpecification { Name = topicName } });
                    }
                    catch (CreateTopicsException e)
                    {
                        Console.WriteLine($"An error occured creating topic {e.Results[0].Topic}: {e.Results[0].Error.Reason}");
                    }
                }
            }
        }

        private static async Task DeleteTopicsAsync()
        {
            using (var adminClient = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = kafkaServer }).Build())
            {
                foreach (string topic in topicsName)
                {
                    string topicName = topicsBaseName + topic;
                    try
                    {
                        await adminClient.DeleteTopicsAsync(new string[] { topicName });
                    }
                    catch (DeleteTopicsException e)
                    {
                        Console.WriteLine($"An error occured creating topic {e.Results[0].Topic}: {e.Results[0].Error.Reason}");
                    }
                }
            }
        }
    }
}