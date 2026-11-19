using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RubrikOracleRecovery.Configuration
{
    public class RscCredentials
    {
        [JsonPropertyName("client_id")]
        public string ClientId { get; set; }

        [JsonPropertyName("client_secret")]
        public string ClientSecret { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("access_token_uri")]
        public string AccessTokenUri { get; set; }

        [JsonPropertyName("graphql_url")]
        public string GraphqlUrl { get; set; }
    }

    public static class ConfigManager
    {
        public static RscCredentials LoadConfig(string path = "Configuration/config.json")
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"Arquivo de configuração não encontrado em {path}");

            string base64Content = File.ReadAllText(path).Trim();
            byte[] jsonBytes = Convert.FromBase64String(base64Content);
            string jsonString = Encoding.UTF8.GetString(jsonBytes);

            return JsonSerializer.Deserialize<RscCredentials>(jsonString);
        }
    }
}