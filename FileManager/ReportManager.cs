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
            Directory.CreateDirectory(reportDir);

            string now = DateTime.Now.ToString("dd-MM-yyyy HHmmss");
            string fileName = $"{reportName}_{now}.csv";
            string reportPath = Path.Combine(reportDir, fileName);

            var processedData = rows.Select(r => new
            {
                hostOrRacName = r.HostOrRacName.Replace("Oracle Host ", "").Trim(),
                latest_end_time = r.LatestEndTime.ToString("yyyy-MM-ddTHH:mm:sszzz")
            })
            .OrderByDescending(r => r.latest_end_time)
            .DistinctBy(r => r.hostOrRacName)
            .ToList();

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