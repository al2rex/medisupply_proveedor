using backend.Config;
using backend.Dto;
using Confluent.Kafka;
using Pulsar.Client.Api;
using System.Text;
using System.Text.Json;
using System.Web;

namespace backend.Controllers
{
    public class PulsarService
    {
        private readonly PulsarClient _client;
        
        private readonly PulsarSettings _settings;

        private readonly KafkaSettings _kafkaSettings;

        private readonly IProducer<Null, string> _kafkaProducer;

        public PulsarService(PulsarSettings settings, KafkaSettings kafkaSettings)
        {
            _settings = settings;
            _kafkaSettings = kafkaSettings;

            _client = new PulsarClientBuilder()
                .ServiceUrl(_settings.BrokerUrl)
                .BuildAsync()
                .GetAwaiter()
                .GetResult();
            Console.WriteLine($"✅ Pulsar conectado a: {_settings.BrokerUrl}");

            var config = new ProducerConfig
            {
                BootstrapServers = _kafkaSettings?.BootstrapServers ?? "localhost:9092"
            };

            _kafkaProducer = new ProducerBuilder<Null, string>(config).Build();
            Console.WriteLine($"✅ Kafka configurado en: {_kafkaSettings.BootstrapServers}");
        }

        public async Task PublicarInventarioRecibidoAsync(string mensaje)
        {
            var topic_conmsumer = _settings.Topics.CONSUMER;

            var producer = await _client.NewProducer()
                .Topic(topic_conmsumer)
                .CreateAsync();

            await producer.SendAsync(Encoding.UTF8.GetBytes(mensaje));

            Console.WriteLine($"✅ Mensaje publicado en 'InventarioRecibido': {mensaje}");
        }

        public async Task SuscribirseASolicitudProveedorAsync()
        {
            var topic_producer = _settings.Topics.PRODUCER;
            var subscription = _settings.SubscriptionName;

            var consumer = await _client.NewConsumer()
                .Topic(topic_producer)
                .SubscriptionName(subscription)
                .SubscribeAsync();

            Console.WriteLine("📡 Escuchando mensajes en el tópico 'SolicitudProveedor'...");

            _ = Task.Run(async () =>
            {
                while (true)
                {
                    var msg = await consumer.ReceiveAsync();
                    var contenido = Encoding.UTF8.GetString(msg.Data);

                    Console.WriteLine("📨 Mensaje recibido crudo:");
                    Console.WriteLine(contenido);

                    try
                    {
                        // 🔹 Paso 1: Decodificar el texto URL (x-www-form-urlencoded)
                        string decoded = HttpUtility.UrlDecode(contenido);

                        // 🔹 Paso 2: Extraer el parámetro 'content={...}'
                        int start = decoded.IndexOf("content=");

                        if (start >= 0)
                        {
                            string contentJson = decoded.Substring(start + 8); // Salta 'content='
                            int end = contentJson.IndexOf("&");
                            if (end > 0)
                                contentJson = contentJson.Substring(0, end);

                            // 🔹 Paso 3: Parsear el JSON interno {"content": {...}}
                            using var doc = JsonDocument.Parse(contentJson);
                            if (doc.RootElement.TryGetProperty("content", out var inner))
                            {
                                var solicitud = JsonSerializer.Deserialize<SolicitudProveedorDto>(inner.ToString());
                                Console.WriteLine($"✅ Producto: {solicitud?.productID}, Stock: {solicitud?.stock}");

                                // 🔹 Publicar en Kafka
                                var kafkaTopic = _kafkaSettings.Topic;
                                var kafkaMessage = JsonSerializer.Serialize(solicitud);

                                await _kafkaProducer.ProduceAsync(kafkaTopic, new Message<Null, string> { Value = kafkaMessage });
                                Console.WriteLine($"📤 Publicado en Kafka ({kafkaTopic}): {kafkaMessage}");
                            }
                            else
                            {
                                Console.WriteLine("⚠️ No se encontró el campo 'content' en el JSON.");
                            }
                        }
                        else
                        {
                            Console.WriteLine("⚠️ No se encontró el parámetro 'content=' en el mensaje.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Error al parsear mensaje: {ex.Message}");
                    }

                    await consumer.AcknowledgeAsync(msg.MessageId);
                }
            });
        }
    }
}

