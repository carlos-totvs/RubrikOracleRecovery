using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using RubrikOracleRecovery.Configuration;
using RubrikOracleRecovery.Services;
using RubrikOracleRecovery.FileManager;

namespace RubrikOracleRecovery
{
    class Program
    {
        static async Task Main(string[] args)
        {
            string inputFile = "hosts.csv";

            // Parse dos argumentos de linha de comando (ex: --input hosts.csv)
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--input" && i + 1 < args.Length)
                {
                    inputFile = args[i + 1];
                }
            }

            string inputPath = Path.Combine("reports", "input", inputFile);
            if (!File.Exists(inputPath))
            {
                Console.WriteLine($"Erro: Arquivo de entrada {inputPath} não encontrado.");
                return;
            }

            Console.WriteLine("Carregando configurações Base64...");
            var config = ConfigManager.LoadConfig();

            Console.WriteLine($"Lendo hosts do arquivo {inputFile}...");
            var hosts = new List<string>();
            foreach (var line in File.ReadAllLines(inputPath))
            {
                if (!string.IsNullOrWhiteSpace(line))
                    hosts.Add(line.Trim());
            }

            Console.WriteLine("Consultando Rubrik RSC...");
            var snapshotService = new SnapshotService(config);
            var rawRecoveryPoints = await snapshotService.GetRecoveryPointsAsync(hosts);

            Console.WriteLine("Processando dados e gerando relatório...");
            ReportManager.GenerateOracleRecoveryReport("Oracle_Recovery_Points", rawRecoveryPoints);
        }
    }
}