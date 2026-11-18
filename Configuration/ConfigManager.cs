using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace RubrikOracleRecovery.Configuration
{
    public class RscCredentials
    {
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string Url { get; set; }
    }

    public static class ConfigManager
    {
        public static RscCredentials LoadConfig(string path = "Configuration/config.json")
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"Arquivo de configuração não encontrado em {path}");

            // Lê o conteúdo Base64 do arquivo
            string base64Content = File.ReadAllText(path).Trim();
            
            // Decodifica o Base64 para a string JSON original
            byte[] jsonBytes = Convert.FromBase64String(base64Content);
            string jsonString = Encoding.UTF8.GetString(jsonBytes);

            return JsonSerializer.Deserialize<RscCredentials>(jsonString);
        }
    }
}