using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RubrikOracleRecovery.Model;

namespace RubrikOracleRecovery.FileManager
{
    public static class ReportManager
    {
        public static void GenerateOracleRecoveryReport(string reportName, List<OracleRecoveryPoint> rows)
        {
            string reportDir = Path.Combine(Directory.GetCurrentDirectory(), "reports");
            Directory.CreateDirectory(reportDir); // Garante que a pasta reports exista

            string now = DateTime.Now.ToString("dd-MM-yyyy HHmmss");
            string fileName = $"{reportName}_{now}.csv";
            string reportPath = Path.Combine(reportDir, fileName);

            // 1. Limpeza do nome (Remove "Oracle Host") e formatação
            // Equivalente a: processed_row["hostOrRacName"] = raw_host.replace("Oracle Host ", "").strip()
            var processedData = rows.Select(r => new
            {
                hostOrRacName = r.HostOrRacName.Replace("Oracle Host ", "").Trim(),
                latest_end_time = r.LatestEndTime.ToString("yyyy-MM-ddTHH:mm:sszzz")
            })
            // 2. Ordenação decrescente pela data mais recente
            // Equivalente a: df.sort_values("latest_end_time", ascending=False)
            .OrderByDescending(r => r.latest_end_time)
            // 3. Remoção de duplicidades mantendo o último backup
            // Equivalente a: .drop_duplicates(subset=["hostOrRacName"])
            .DistinctBy(r => r.hostOrRacName)
            .ToList();

            // 4. Gravação do CSV (Apenas as duas colunas: hostOrRacName e latest_end_time)
            using (var writer = new StreamWriter(reportPath))
            {
                writer.WriteLine("hostOrRacName,latest_end_time");
                foreach (var row in processedData)
                {
                    writer.WriteLine($"{row.hostOrRacName},{row.latest_end_time}");
                }
            }

            Console.WriteLine($"\nOutput gerado com sucesso em: {reportPath}");
        }
    }
}