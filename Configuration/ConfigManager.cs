using System;
using System.IO;
using System.Security.Cryptography;
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
        private const string PrefixDpapi = "DPAPI:";

        public static RscCredentials LoadConfig(string path = "Configuration/config.txt")
        {
            // Garante que o caminho relativo funcione mesmo se a Console App for disparada 
            // pelo Agendador de Tarefas do Windows (onde o diretório de trabalho costuma ser C:\Windows\System32)
            string fullPath = Path.IsPathRooted(path)
                ? path
                : Path.Combine(AppContext.BaseDirectory, path);

            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"Arquivo de configuração não encontrado em {fullPath}");

            string fileContent = File.ReadAllText(fullPath).Trim();
            string jsonString;

            if (fileContent.StartsWith(PrefixDpapi, StringComparison.OrdinalIgnoreCase))
            {
                // 2ª execução em diante: já está blindado pela DPAPI da VM
                string protectedBase64 = fileContent[PrefixDpapi.Length..];
                byte[] protectedBytes = Convert.FromBase64String(protectedBase64);

                byte[] originalBytes = ProtectedData.Unprotect(
                    protectedBytes,
                    optionalEntropy: null,
                    scope: DataProtectionScope.CurrentUser);

                jsonString = Encoding.UTF8.GetString(originalBytes);
            }
            else
            {
                /// 1ª execução: lê o Base64 original e criptografa com DPAPI
                byte[] originalBytes = Convert.FromBase64String(fileContent);
                jsonString = Encoding.UTF8.GetString(originalBytes);

                byte[] protectedBytes = ProtectedData.Protect(
                    originalBytes,
                    optionalEntropy: null,
                    scope: DataProtectionScope.CurrentUser);

                string protectedContent = PrefixDpapi + Convert.ToBase64String(protectedBytes);

                // Limpa o array original da memória RAM
                Array.Clear(originalBytes, 0, originalBytes.Length);

                // Sobrescreve o arquivo físico com zeros antes de gravar o novo conteúdo (shredding)
                long tamanhoOriginal = new FileInfo(fullPath).Length;
                using (var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Write, FileShare.None))
                {
                    fs.Write(new byte[tamanhoOriginal], 0, (int)tamanhoOriginal);
                    fs.Flush(flushToDisk: true);
                }

                // Grava o conteúdo blindado com DPAPI
                File.WriteAllText(fullPath, protectedContent);
            }

            return JsonSerializer.Deserialize<RscCredentials>(jsonString)
                ?? throw new InvalidOperationException("Não foi possível desserializar as credenciais do config.txt.");
        }
    }
}