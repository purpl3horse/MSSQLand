﻿using MSSQLand.Models;
using MSSQLand.Utilities;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;


namespace MSSQLand.Services
{
    public class ConfigurationService
    {
        private readonly QueryService _queryService;
        private readonly Server _server;

        public ConfigurationService(QueryService queryService, Server server)
        {
            _queryService = queryService;
            _server = server;
        }

        

        public bool CheckAssembly(string assemblyName)
        {
            string query = $"SELECT name FROM sys.assemblies WHERE name='{assemblyName}';";

            return _queryService.ExecuteScalar(query)?.ToString() == assemblyName;
        }

        public bool CheckTrustedAssembly(string assemblyName)
        {
            try
            {
                // Query to retrieve all trusted assemblies
                string query = "SELECT description FROM sys.trusted_assemblies;";
                DataTable trustedAssembliesTable = _queryService.ExecuteTable(query);

                if (trustedAssembliesTable.Rows.Count == 0)
                {
                    Logger.Warning("No trusted assemblies found");
                    return false;
                }

                // Log all trusted assemblies for debugging
                Logger.Debug("Trusted assemblies:");
                foreach (DataRow row in trustedAssembliesTable.Rows)
                {
                    string description = row["description"].ToString();
                    Logger.DebugNested(description);

                    string name = description.Split(',')[0];

                    // Check if the assemblyName is a substring of the description
                    if (name == assemblyName)
                    {
                        Logger.Success($"Assembly '{assemblyName}' is trusted");
                        return true;
                    }
                }

                Logger.Warning($"Assembly '{assemblyName}' is not trusted");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error retrieving trusted assemblies: {ex.Message}");
                return false;
            }
        }


        public bool CheckProcedures(string procedureName)
        {
            try
            {
                // Query to retrieve all trusted assemblies
                string query = "SELECT SCHEMA_NAME(schema_id), name, type FROM sys.procedures;";
                DataTable trustedAssembliesTable = _queryService.ExecuteTable(query);

                if (trustedAssembliesTable.Rows.Count == 0)
                {
                    Logger.Warning("No procedures found");
                    return false;
                }

                // Log all trusted assemblies for debugging
                Logger.Debug("Procedures:");
                foreach (DataRow row in trustedAssembliesTable.Rows)
                {
                    string name = row["name"].ToString();
                    Logger.DebugNested(name);

                    // Check if the assemblyName is a substring of the description
                    if (name == procedureName)
                    {
                        Logger.Success($"Procedure '{procedureName}' exist");
                        return true;
                    }
                }

                Logger.Warning($"Procedure '{procedureName}' does not exist");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error retrieving procedures: {ex.Message}");
                return false;
            }
        }


        /// <summary>
        /// Enables or disables a specified SQL Server configuration option using sp_configure.
        /// </summary>
        /// <param name="optionName">The name of the configuration option to modify.</param>
        /// <param name="value">The value to set for the configuration option (e.g., 1 to enable, 0 to disable).</param>
        public bool SetConfigurationOption(string optionName, int value)
        {

            EnableAdvancedOptions();

            Logger.Task($"Checking status of '{optionName}'");
            try
            {
                // Check other module status via sys.configurations
                var configValue = _queryService.ExecuteScalar($"SELECT value FROM sys.configurations WHERE name = '{optionName}';");
                if (configValue == null)
                {
                    Logger.Warning($"Configuration '{optionName}' not found or inaccessible");
                    return false;
                }

                if (Convert.ToInt32(configValue) == value)
                {
                    Logger.Info($"Configuration option '{optionName}' is already set to {value}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error checking module status for '{optionName}': {ex.Message}");
                return false;
            }

            try
            {
                // Update the configuration option
                Logger.Info($"Updating configuration option '{optionName}' to {value}.");
                _queryService.ExecuteNonProcessing($"EXEC sp_configure '{optionName}', {value}; RECONFIGURE;");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to set configuration option '{optionName}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Adds a CLR assembly hash to the list of trusted assemblies in SQL Server.
        /// </summary>
        /// <param name="assemblyHash">The SHA-512 hash of the assembly to trust.</param>
        /// <param name="assemblyDescription">A description of the assembly (e.g., name, version, etc.).</param>
        /// <returns>True if the hash was successfully added, false otherwise.</returns>
        public bool RegisterTrustedAssembly(string assemblyHash, string assemblyDescription)
        {
            if (_server.Legacy)
            {
                Logger.Warning("CLR hash cannot be added to legacy servers");
                return false;
            }

            try
            {
                // Check if the hash already exists
                string checkHash = _queryService.ExecuteScalar($"SELECT * FROM sys.trusted_assemblies WHERE hash = 0x{assemblyHash};")?.ToString()?.ToLower();

                if (checkHash?.Contains("permission was denied") == true)
                {
                    Logger.Error("Insufficient privileges to perform this action");
                    return false;
                }

                if (checkHash?.Contains("system.byte[]") == true)
                {
                    Logger.Warning("Hash already exists in sys.trusted_assemblies");

                    // Attempt to remove the existing hash
                    string deletionQuery = _queryService.ExecuteScalar($"EXEC sp_drop_trusted_assembly 0x{assemblyHash};")?.ToString()?.ToLower();

                    if (deletionQuery?.Contains("permission was denied") == true)
                    {
                        Logger.Error("Insufficient privileges to remove existing trusted assembly");
                        return false;
                    }

                    Logger.Success("Existing hash removed successfully");
                }

                // Add the new hash to the trusted assemblies
                _queryService.ExecuteNonProcessing($@"
                    EXEC sp_add_trusted_assembly
                    0x{assemblyHash},
                    N'{assemblyDescription}, version=0.0.0.0, culture=neutral, publickeytoken=null, processorarchitecture=msil';
                ");

                // Verify if the hash was successfully added
                if (CheckTrustedAssembly(assemblyDescription))
                {
                    Logger.Success($"Added assembly hash 0x{assemblyHash} as trusted");
                    return true;
                }

                Logger.Error("Failed to add hash to sys.trusted_assemblies");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"An error occurred while adding the CLR hash: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Deploys a CLR assembly, creates a stored procedure, executes it, and performs cleanup.
        /// </summary>
        public void DeployAndExecuteClrAssembly(
            string assemblyName,
            string procedureName,
            string assemblyHash,
            string trustedAssemblyDescription,
            string assemblyHexBytes)
        {
            string dropProcedure = $"DROP PROCEDURE IF EXISTS [{procedureName}];";
            string dropAssembly = $"DROP ASSEMBLY IF EXISTS [{assemblyName}];";
            string dropClrHash = $"EXEC sp_drop_trusted_assembly 0x{assemblyHash};";

            Logger.Task("Starting CLR assembly deployment process");

            try
            {

                if (_server.Legacy)
                {
                    Logger.Info("Legacy server detected. Enabling TRUSTWORTHY property");
                    _queryService.ExecuteNonProcessing($"ALTER DATABASE {_server.Database} SET TRUSTWORTHY ON;");
                }
                else
                {
                    if (RegisterTrustedAssembly(assemblyHash, trustedAssemblyDescription) == false)
                    {
                        return;
                    }
                }

                // Step 1: Drop existing procedure and assembly if they exist
                _queryService.ExecuteNonProcessing(dropProcedure);
                _queryService.ExecuteNonProcessing(dropAssembly);


                // Step 3: Create the assembly from the DLL bytes
                Logger.Task("Creating the assembly from DLL bytes");
                _queryService.ExecuteNonProcessing(
                    $"CREATE ASSEMBLY [{assemblyName}] FROM 0x{assemblyHexBytes} WITH PERMISSION_SET = UNSAFE;");

                if (!CheckAssembly(assemblyName))
                {
                    Logger.Error("Failed to create a new assembly.");
                    _queryService.ExecuteNonProcessing(dropAssembly);
                    _queryService.ExecuteNonProcessing(dropClrHash);
                    return;
                }

                Logger.Success($"Assembly '{assemblyName}' successfully created.");

                // Step 4: Create the stored procedure linked to the assembly
                Logger.Task("Creating the stored procedure linked to the assembly");
                _queryService.ExecuteNonProcessing(
                    $"CREATE PROCEDURE [dbo].[{procedureName}] AS EXTERNAL NAME [{assemblyName}].[StoredProcedures].[{procedureName}];");

                if (!CheckProcedures(procedureName))
                {
                    Logger.Error("Failed to create the stored procedure");
                    _queryService.ExecuteNonProcessing(dropProcedure);
                    _queryService.ExecuteNonProcessing(dropAssembly);
                    _queryService.ExecuteNonProcessing(dropClrHash);
                    return;
                }

                Logger.Success($"Stored procedure '{procedureName}' successfully created");

                // Step 5: Execute the stored procedure
                Logger.Task($"Executing the stored procedure '{procedureName}'");
                _queryService.ExecuteNonProcessing($"EXEC {procedureName};");
                Logger.Success("Stored procedure executed successfully");

                // Step 6: Cleanup - Drop procedure, assembly, and trusted hash
                Logger.Task("Performing cleanup");
                _queryService.ExecuteNonProcessing(dropProcedure);
                _queryService.ExecuteNonProcessing(dropAssembly);
                _queryService.ExecuteNonProcessing(dropClrHash);

                // Step 7: Reset TRUSTWORTHY property for legacy servers
                if (_server.Legacy)
                {
                    Logger.Info("Resetting TRUSTWORTHY property");
                    _queryService.ExecuteNonProcessing(
                        $"ALTER DATABASE {_server.Database} SET TRUSTWORTHY OFF;");
                }

                Logger.Success("Cleanup completed");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error during CLR assembly deployment: {ex.Message}");
                // Perform cleanup in case of failure
                _queryService.ExecuteNonProcessing(dropProcedure);
                _queryService.ExecuteNonProcessing(dropAssembly);
                _queryService.ExecuteNonProcessing(dropClrHash);

                if (_server.Legacy)
                {
                    _queryService.ExecuteNonProcessing(
                        $"ALTER DATABASE {_server.Database} SET TRUSTWORTHY OFF;");
                }
            }
        }



        /// <summary>
        /// Ensures that 'show advanced options' is enabled.
        /// </summary>
        private void EnableAdvancedOptions()
        {
            Logger.Task("Ensuring advanced options are enabled");
            var advancedOptionsEnabled = _queryService.ExecuteScalar("SELECT value_in_use FROM sys.configurations WHERE name = 'show advanced options';");

            if (advancedOptionsEnabled == null || Convert.ToInt32(advancedOptionsEnabled) != 1)
            {
                try
                {
                    Logger.Info("Enabling advanced options");
                    _queryService.ExecuteNonProcessing("EXEC sp_configure 'show advanced options', 1; RECONFIGURE;");
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Failed to enable 'show advanced options': {ex.Message}");
                }
            }
            else
            {
                Logger.Info("Advanced options already enabled");
            }
        }
    }
}
