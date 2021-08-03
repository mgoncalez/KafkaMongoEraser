using System;

using MongoDB.Bson;
using MongoDB.Driver;

using Confluent.Kafka.Admin;
using System.Threading.Tasks;
using Confluent.Kafka;

namespace KafkaMongoEraser
{
    internal class Program
    {
        private static readonly string[] topicsName = new string[] { "articles", "books", "categories", "tree", "collections", "home", "category-filter-articles", "category-filter-books" };
        private const string topicsBaseName = "wly.glb.dummies.dummies-";
        private const string kafkaServer = "localhost:9092"; //ip-pl-kfkbq01.aws.wiley.com:9092

        public static async Task Main(string[] args)
        {
            bool executeForMongo = false;
            bool executeForKafka = true;

            if (executeForMongo)
            {
                string[] MongoCollections = new string[] { "articles", "books", "categories", "collections", "tree" };

                var client = new MongoClient("mongodb://localhost:27018");
                //var client = new MongoClient("mongodb://dummiesDev:hdEy1gfFm8feQpdtG92Cwd@dummies-dev-docdb.cluster-cafz4eqeitkh.us-east-1.docdb.amazonaws.com:27017/");

                var database = client.GetDatabase("dummies_v2");

                foreach (string collection in MongoCollections)
                {
                    database.DropCollection(collection);
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