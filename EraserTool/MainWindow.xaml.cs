using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;

using Confluent.Kafka.Admin;
using Confluent.Kafka;
using System.Security.Cryptography.X509Certificates;
using System.Security.Authentication;
using System.Net.Security;
using MongoDB.Driver;
using Microsoft.Extensions.Configuration;

namespace EraserTool
{
    internal class ItemKafkaMongo
    {
        public string Item { get; set; }
        public string Kafka { get; set; }
        public string Mongo { get; set; }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string topicsBaseName = "wly.glb.dummies.dummies-";
        private List<ItemKafkaMongo> listKafkaMongo;

        public MainWindow()
        {
            InitializeComponent();
            FillFieldsFromConfig();
        }

        private void FillFieldsFromConfig()
        {
            string workingDirectory = Environment.CurrentDirectory;

            var builder = new ConfigurationBuilder()
                        .SetBasePath(Directory.GetParent(workingDirectory).Parent.Parent.FullName)
                        .AddJsonFile($"appsettings.json", false, true);

            var environment = "production";

            var configurationRoot = builder.Build();

            TextBoxKafkaServer.Text = configurationRoot.GetSection(environment + ":kafka-server-name").Value;
            TextBoxMongoConnection.Text = configurationRoot.GetSection(environment + ":mongo-connection-string").Value;
            TextBoxMongoCertPath.Text = configurationRoot.GetSection(environment + ":mongo-cert-path").Value;
            TextBoxMongoHash.Text = configurationRoot.GetSection(environment + ":mongo-hash").Value;

            var KafkaPrefix = configurationRoot.GetSection("kafka-topic-prefix").Value;

            var array = configurationRoot.GetSection("items").GetChildren();
            listKafkaMongo = new List<ItemKafkaMongo>();

            foreach (IConfigurationSection sections in array)
            {
                ItemKafkaMongo item = new ItemKafkaMongo();

                item.Item = sections.Key;
                item.Kafka = KafkaPrefix + configurationRoot.GetSection("items:" + sections.Key + ":kafka-topic").Value;
                item.Mongo = configurationRoot.GetSection("items:" + sections.Key + ":mongo-collection").Value;
                ListBoxItems.Items.Add(sections.Key);
                listKafkaMongo.Add(item);
            }
        }

        private void SelectAllKafka_Click(object sender, RoutedEventArgs e)
        {
            if (ListBoxItems.SelectedItems.Count > 0)
            {
                ListBoxItems.UnselectAll();
            }
            else
            {
                ListBoxItems.SelectAll();
            }
        }

        private async void ExecuteKafka_ClickAsync(object sender, RoutedEventArgs e)
        {
            await DeleteTopicsAsync();
            await CreateTopicsAsync();

            MessageBox.Show("Done Kafka Executing");
        }

        private async Task CreateTopicsAsync()
        {
            using (var adminClient = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = TextBoxKafkaServer.Text }).Build())
            {
                foreach (var topic in ListBoxItems.SelectedItems)
                {
                    string topicName = listKafkaMongo.Find(x => x.Item == (string)topic).Kafka;

                    try
                    {
                        await adminClient.CreateTopicsAsync(new TopicSpecification[] {
                        new TopicSpecification { Name = topicName } });
                    }
                    catch (CreateTopicsException e)
                    {
                        MessageBox.Show($"An error occured creating topic {e.Results[0].Topic}: {e.Results[0].Error.Reason}");
                    }
                }

                if (ChkNoRecreate.IsChecked == false)
                {
                    foreach (var topic in TextBoxTopic.Text.Split(','))
                    {
                        string topicName = topic;

                        try
                        {
                            await adminClient.CreateTopicsAsync(new TopicSpecification[] {
                        new TopicSpecification { Name = topicName } });
                        }
                        catch (CreateTopicsException e)
                        {
                            MessageBox.Show($"An error occured creating topic {e.Results[0].Topic}: {e.Results[0].Error.Reason}");
                        }
                    }
                }
            }
        }

        private async Task DeleteTopicsAsync()
        {
            using (var adminClient = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = TextBoxKafkaServer.Text }).Build())
            {
                foreach (var topic in ListBoxItems.SelectedItems)
                {
                    var topicName = listKafkaMongo.Find(x => x.Item == (string)topic).Kafka;
                    try
                    {
                        await adminClient.DeleteTopicsAsync(new string[] { topicName });
                    }
                    catch (DeleteTopicsException e)
                    {
                        MessageBox.Show($"An error occured creating topic {e.Results[0].Topic}: {e.Results[0].Error.Reason}");
                    }
                }

                foreach (var topic in TextBoxTopic.Text.Split(','))
                {
                    var topicName = topic;
                    try
                    {
                        await adminClient.DeleteTopicsAsync(new string[] { topicName });
                    }
                    catch (DeleteTopicsException e)
                    {
                        MessageBox.Show($"An error occured creating topic {e.Results[0].Topic}: {e.Results[0].Error.Reason}");
                    }
                }
            }
        }

        private void ExecuteMongo_Click(object sender, RoutedEventArgs e)
        {
            var msgBoxResult = MessageBox.Show("This will erase all selected MongoDB collection. Are you sure do you want to continue?", "Warning", MessageBoxButton.YesNo);

            if (msgBoxResult == MessageBoxResult.No)
            {
                return;
            }

            var client = new MongoClient(SetupSettingsMongo());

            var database = client.GetDatabase("dummies_v2");

            var list = database.ListCollectionNames();

            foreach (var collection in ListBoxItems.SelectedItems)
            {
                var item = listKafkaMongo.Find(x => x.Item == (string)collection).Mongo;
                database.GetCollection<object>(item).DeleteManyAsync(Builders<object>.Filter.Empty);
            }

            foreach (var collection in TextBoxTopic.Text.Split(','))
            {
                var item = collection;
                database.GetCollection<object>(item).DeleteManyAsync(Builders<object>.Filter.Empty);
            }

            MessageBox.Show("Clearing completed");
        }

        private MongoClientSettings SetupSettingsMongo()
        {
            using (X509Store localTrustStore = new(StoreName.Root))
            {
                var certificateCollection = new X509Certificate2Collection();
                certificateCollection.ImportFromPem(TextBoxMongoCertPath.Text);
                try
                {
                    localTrustStore.Open(OpenFlags.ReadWrite);
                    localTrustStore.AddRange(certificateCollection);
                }
                catch (Exception ex)
                {
                    throw new Exception(ex.Message);
                }
                finally
                {
                    localTrustStore.Close();
                }

                var settings = MongoClientSettings.FromUrl(new MongoUrl(TextBoxMongoConnection.Text));
                settings.SslSettings = new SslSettings
                {
                    EnabledSslProtocols = SslProtocols.Tls12,
                    ServerCertificateValidationCallback = delegate (object s, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
                    {
                        return true;// certificate.GetCertHashString() == TextBoxMongoHash.Text;
                    }
                };
                return settings;
            }
        }

        private void ChkNoRecreate_Checked(object sender, RoutedEventArgs e)
        {
        }
    }
}