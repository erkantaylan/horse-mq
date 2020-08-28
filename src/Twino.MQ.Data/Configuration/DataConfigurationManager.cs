using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Twino.MQ.Queues;

namespace Twino.MQ.Data.Configuration
{
    internal class DataConfigurationManager
    {
        private readonly object _optionsLock = new object();

        private DataConfiguration Config => ConfigurationFactory.Configuration;

        /// <summary>
        /// Loads configurations
        /// </summary>
        public DataConfiguration Load(string fullpath)
        {
            if (!File.Exists(fullpath))
            {
                var c = DataConfiguration.Empty();
                string serialized = Newtonsoft.Json.JsonConvert.SerializeObject(c);

                string dir = FindDirectoryIfFile(ConfigurationFactory.Builder.ConfigFile);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(ConfigurationFactory.Builder.ConfigFile, serialized);
                return c;
            }

            string json = File.ReadAllText(fullpath);
            DataConfiguration configuration = Newtonsoft.Json.JsonConvert.DeserializeObject<DataConfiguration>(json);
            return configuration;
        }

        private string FindDirectoryIfFile(string fullpath)
        {
            return fullpath.Substring(0, fullpath.LastIndexOf('/'));
        }

        /// <summary>
        /// Saves current configurations
        /// </summary>
        public void Save()
        {
            try
            {
                string serialized;
                lock (_optionsLock)
                    serialized = Newtonsoft.Json.JsonConvert.SerializeObject(Config);

                string dir = FindDirectoryIfFile(ConfigurationFactory.Builder.ConfigFile);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(ConfigurationFactory.Builder.ConfigFile, serialized);
            }
            catch (Exception e)
            {
                if (ConfigurationFactory.Builder.ErrorAction != null)
                    ConfigurationFactory.Builder.ErrorAction(null, null, e);
            }
        }

        /// <summary>
        /// Adds new queue into configurations
        /// </summary>
        public bool Add(TwinoQueue queue, string filename)
        {
            QueueOptionsConfiguration queueOptions = queue.Options.ToConfiguration();
            PersistentDeliveryHandler deliveryHandler = (PersistentDeliveryHandler) queue.DeliveryHandler;

            QueueConfiguration queueConfiguration = new QueueConfiguration();
            queueConfiguration.Configuration = queueOptions;
            queueConfiguration.Name = queue.Name;
            queueConfiguration.File = filename;
            queueConfiguration.Queue = queue;
            queueConfiguration.DeleteWhen = Convert.ToInt32(deliveryHandler.DeleteWhen);
            queueConfiguration.ProducerAck = Convert.ToInt32(deliveryHandler.ProducerAckDecision);

            lock (_optionsLock)
                Config.Queues.Add(queueConfiguration);

            return true;
        }

        /// <summary>
        /// Removes queue from configurations
        /// </summary>
        public void Remove(TwinoQueue queue)
        {
            lock (_optionsLock)
            {
                QueueConfiguration queueConfiguration = Config.Queues.FirstOrDefault(x => x.Name == queue.Name);

                if (queueConfiguration != null)
                    Config.Queues.Remove(queueConfiguration);
            }
        }

        /// <summary>
        /// Loads messages of queues in configuration
        /// </summary>
        public async Task LoadQueues(TwinoMQ server)
        {
            foreach (QueueConfiguration queueConfiguration in Config.Queues)
            {
                TwinoQueue queue = server.FindQueue(queueConfiguration.Name);
                if (queue == null)
                {
                    queue = await Extensions.CreateQueue(server,
                                                         queueConfiguration.Name,
                                                         (DeleteWhen) queueConfiguration.DeleteWhen,
                                                         (ProducerAckDecision) queueConfiguration.ProducerAck,
                                                         queueConfiguration.Configuration.ToOptions());

                    //queue creation not permitted, skip
                    if (queue == null)
                        continue;
                }
                else
                {
                    PersistentDeliveryHandler deliveryHandler = (PersistentDeliveryHandler) queue.DeliveryHandler;
                    await deliveryHandler.Initialize();
                }

                queueConfiguration.Queue = queue;
            }
        }
    }
}