using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Kusto.Data;
using Kusto.Data.Net.Client;
using Kusto.Data.Common;
using Newtonsoft.Json;

namespace Company.Function
{
    public static class ADXHttpTrigger
    {
        [FunctionName("ADXHttpTrigger")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string name = req.Query["name"];
            string clusterurl = Environment.GetEnvironmentVariable("ADXClusterUrl");
            string database = Environment.GetEnvironmentVariable("ADXDatabase");
            var AuthenticationMode = Environment.GetEnvironmentVariable("AuthenticationMode");
            var tenantId = Environment.GetEnvironmentVariable("TENANT_ID");
            // TODO: retrieve managedidentityid
            log.LogInformation($"INFO: Managed Identity Id is {Environment.GetEnvironmentVariable("MANAGED_IDENTITY_CLIENT_ID")}");

            // get table name from query payload
            string tableName= req.Query["tablename"];
            string symbol = req.Query["symbol"];
            string timespan = req.Query["timespan"];
            tableName = tableName ?? "sentiment_1h";
            symbol = symbol ?? "9896.HK";
            timespan = timespan ?? "365d";
            var kustoConnectionString = GenerateConnectionString(clusterurl,AuthenticationMode, tenantId, log);
            string result = string.Empty;
            using (var queryProvider = KustoClientFactory.CreateCslQueryProvider(kustoConnectionString))
            {
                var query = $"{tableName} | where symbol == '{symbol}' and versionCreated > ago({timespan})";
                var clientRequestProperties = new ClientRequestProperties
                {
                    Application = "QuickStart.csproj",
                    // It is strongly recommended that each request has its own unique request identifier.
                    // This is mandatory for some scenarios (such as cancelling queries) and will make troubleshooting easier in others.
                    ClientRequestId = $"SampleApp;{Guid.NewGuid().ToString()}"
                };
                var reader = await queryProvider.ExecuteQueryAsync(database, query, clientRequestProperties);
                var dataTable = new System.Data.DataTable();
                dataTable.Load(reader);
                reader.Close();
                result = JsonConvert.SerializeObject(dataTable);
            }

            // var client = Kusto.Data.Net.Client.KustoClientFactory.CreateCslQueryProvider("https://help.kusto.windows.net/Samples;Fed=true");
            // var reader = client.ExecuteQuery("StormEvents | count");
            // // Read the first row from reader -- it's 0'th column is the count of records in MyTable
            // // Don't forget to dispose of reader when done.

            // string responseMessage = string.IsNullOrEmpty(name)
            //     ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
            //     : $"Hello, {name}. This HTTP triggered function executed successfully.";
            return new OkObjectResult(result);
        }
    
                    /// <summary>
            /// Generates Kusto Connection String based on given Authentication Mode.
            /// </summary>
            /// <param name="clusterUrl"> Cluster to connect to.</param>
            /// <param name="authenticationMode">User Authentication Mode, Options: (UserPrompt|ManagedIdentity|AppKey|AppCertificate)</param>
            /// <param name="tenantId">Given tenant id</param>
            /// <returns>A connection string to be used when creating a Client</returns>
            private static KustoConnectionStringBuilder GenerateConnectionString(string clusterUrl, string authenticationMode, string tenantId, ILogger log)
            {
                // Learn More: For additional information on how to authorize users and apps in Kusto see:
                // https://docs.microsoft.com/azure/data-explorer/manage-database-permissions
                switch (authenticationMode)
                {
                    case "UserPrompt":
                        // Prompt user for credentials
                        return new KustoConnectionStringBuilder(clusterUrl).WithAadUserPromptAuthentication();

                    case "ManagedIdentity":
                        // Authenticate using a System-Assigned managed identity provided to an azure service, or using a User-Assigned managed identity.
                        // For more information, see https://docs.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/overview
                        return CreateManagedIdentityConnectionString(clusterUrl);

                    default:
                        log.LogInformation($"ERROR: Authentication mode '{authenticationMode}' is not supported");
                        return null;
                }
            }

            /// <summary>
            /// Generates Kusto Connection String based on 'ManagedIdentity' Authentication Mode.
            /// </summary>
            /// <param name="clusterUrl">Url of cluster to connect to</param>
            /// <returns>ManagedIdentity Kusto Connection String</returns>
            private static KustoConnectionStringBuilder CreateManagedIdentityConnectionString(string clusterUrl)
            {
                // Connect using the system - or user-assigned managed identity (Azure service only)
                // TODO (config - optional): Managed identity client ID if you are using a user-assigned managed identity
                var clientId = Environment.GetEnvironmentVariable("MANAGED_IDENTITY_CLIENT_ID");
                return clientId is null ? new KustoConnectionStringBuilder(clusterUrl).WithAadSystemManagedIdentity() : new KustoConnectionStringBuilder(clusterUrl).WithAadUserManagedIdentity(clientId);
            }

    }
}
