using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using RubrikOracleRecovery.Model;
using RubrikOracleRecovery.Configuration;

namespace RubrikOracleRecovery.Services
{
    public class SnapshotService
    {
        private readonly HttpClient _httpClient;
        private readonly RscCredentials _credentials;
        private string _accessToken;

        public SnapshotService(RscCredentials credentials)
        {
            _credentials = credentials;
            _httpClient = new HttpClient();
        }

        private async Task AuthenticateAsync()
        {
            if (!string.IsNullOrEmpty(_accessToken))
                return;

            Console.WriteLine("Autenticando na Rubrik RSC...");

            var authPayload = new
            {
                client_id = _credentials.ClientId,
                client_secret = _credentials.ClientSecret,
                name = _credentials.Name
            };

            var content = new StringContent(JsonSerializer.Serialize(authPayload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_credentials.AccessTokenUri, content);
            
            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Falha na autenticação. Status: {response.StatusCode}. Detalhes: {error}");
            }

            var responseString = await response.Content.ReadAsStringAsync();
            using var jsonDoc = JsonDocument.Parse(responseString);
            _accessToken = jsonDoc.RootElement.GetProperty("access_token").GetString();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        }

        public async Task<List<OracleRecoveryPoint>> GetRecoveryPointsAsync(List<string> hosts)
        {
            await AuthenticateAsync();
            var results = new List<OracleRecoveryPoint>();

            string searchHostQuery = @"
                query OracleHostObjectSearch(
                  $first: Int!,
                  $filter: [Filter!]!,
                  $sortBy: HierarchySortByField,
                  $sortOrder: SortOrder
                ) {
                  globalSearchResults(
                    first: $first
                    filter: $filter
                    sortBy: $sortBy
                    sortOrder: $sortOrder
                  ) {
                    nodes {
                      id
                      name
                      objectType
                    }
                  }
                }";

            string getDatesQuery = @"
                query DataRecuperaveisOracle($input: GetOracleDbRecoverableRangesInput!) {
                  oracleRecoverableRanges(input: $input) {
                    data {
                      beginTime
                      endTime
                      dbSnapshotSummaries {
                        databaseName
                        hostOrRacName
                        tablespaces 
                      }
                    }
                  }
                }";

            foreach (var hostName in hosts)
            {
                Console.WriteLine($"Consultando host: {hostName}...");

                try
                {
                    var searchRequest = new
                    {
                        query = searchHostQuery,
                        variables = new 
                        {
                            first = 10,
                            sortBy = "NAME",
                            sortOrder = "ASC",
                            filter = new[]
                            {
                                new { field = "LOCATION", texts = new[] { hostName } },
                                new { field = "IS_GHOST", texts = new[] { "false" } },
                                new { field = "IS_ACTIVE", texts = new[] { "true" } }
                            }
                        }
                    };

                    var searchContent = new StringContent(JsonSerializer.Serialize(searchRequest), Encoding.UTF8, "application/json");
                    var searchResponse = await _httpClient.PostAsync(_credentials.GraphqlUrl, searchContent);
                    searchResponse.EnsureSuccessStatusCode();

                    var searchResponseStr = await searchResponse.Content.ReadAsStringAsync();
                    using var searchJson = JsonDocument.Parse(searchResponseStr);
                    
                    var nodes = searchJson.RootElement
                                          .GetProperty("data")
                                          .GetProperty("globalSearchResults")
                                          .GetProperty("nodes");

                    if (nodes.GetArrayLength() == 0)
                    {
                        Console.WriteLine($"Host {hostName} não encontrado na Rubrik.");
                        continue;
                    }

                    string workloadId = nodes[0].GetProperty("id").GetString();

                    var dateRequest = new
                    {
                        query = getDatesQuery,
                        variables = new 
                        { 
                            input = new 
                            {
                                id = workloadId,
                                shouldIncludeDbSnapshotSummaries = true
                            }
                        }
                    };

                    var dateContent = new StringContent(JsonSerializer.Serialize(dateRequest), Encoding.UTF8, "application/json");
                    var dateResponse = await _httpClient.PostAsync(_credentials.GraphqlUrl, dateContent);
                    dateResponse.EnsureSuccessStatusCode();

                    var dateResponseStr = await dateResponse.Content.ReadAsStringAsync();
                    using var dateJson = JsonDocument.Parse(dateResponseStr);
                    
                    var dataArray = dateJson.RootElement
                                            .GetProperty("data")
                                            .GetProperty("oracleRecoverableRanges")
                                            .GetProperty("data");

                    foreach (var range in dataArray.EnumerateArray())
                    {
                        string endTimeStr = range.GetProperty("endTime").GetString();
                        DateTime endTime = DateTime.Parse(endTimeStr);

                        var summaries = range.GetProperty("dbSnapshotSummaries");
                        foreach (var summary in summaries.EnumerateArray())
                        {
                            string returnedHostName = summary.GetProperty("hostOrRacName").GetString();

                            results.Add(new OracleRecoveryPoint 
                            { 
                                HostOrRacName = returnedHostName, 
                                LatestEndTime = endTime
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao consultar {hostName}: {ex.Message}");
                }
            }

            return results;
        }
    }
}