#region Copyright (c) 2010 Active Web Solutions Ltd
//
// (C) Copyright 2010 Active Web Solutions Ltd
//      All rights reserved.
//
// This software is provided "as is" without warranty of any kind,
// express or implied, including but not limited to warranties as to
// quality and fitness for a particular purpose. Active Web Solutions Ltd
// does not support the Software, nor does it warrant that the Software
// will meet your requirements or that the operation of the Software will
// be uninterrupted or error free or that any defects will be
// corrected. Nothing in this statement is intended to limit or exclude
// any liability for personal injury or death caused by the negligence of
// Active Web Solutions Ltd, its employees, contractors or agents.
//
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using Microsoft.ServiceBus.Samples;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.StorageClient;
using SevenZip;


namespace WorkerRole
{
    public class RunMe
    {
        CloudStorageAccount storageAccount;
        CloudStorageAccount cloudDriveStorageAccount; 
        DiagnosticMonitorConfiguration config;
        CloudDrive cloudDrive = null;
        string workingDirectory = null;
        string environmentVariables = null;
        bool isStopping = false;
        Log log;

        public RunMe()
        {
            storageAccount = CloudStorageAccount.Parse(RoleEnvironment.GetConfigurationSettingValue("DataConnectionString"));
            cloudDriveStorageAccount = CloudStorageAccount.Parse(RoleEnvironment.GetConfigurationSettingValue("CloudDriveConnectionString"));

            log = new Log(RoleEnvironment.GetConfigurationSettingValue("LogConnectionString"));
        
        }

        /// <summary>
        /// Get the version from the Assembly Information
        /// </summary>
        public static string Version()
        {
            return new AssemblyName(Assembly.GetExecutingAssembly().FullName).Version.ToString();
        }

        /// <summary>
        /// Expands keywords in the buffer to allow configuration strings
        /// to be set dynamically at runtime
        /// </summary>
        private string ExpandKeywords(string buffer)
        {
            buffer = buffer.Replace("$approot$", @"$roleroot$\approot");
            buffer = buffer.Replace("$deploymentid$", RoleEnvironment.DeploymentId);
            buffer = buffer.Replace("$roleinstanceid$", RoleEnvironment.CurrentRoleInstance.Id);
            buffer = buffer.Replace("$computername$", Environment.MachineName);
            buffer = buffer.Replace("$guid$", Guid.NewGuid().ToString());
            buffer = buffer.Replace("$now$", DateTime.Now.ToString());
            buffer = buffer.Replace("$roleroot$", Environment.GetEnvironmentVariable("RoleRoot"));

            if (cloudDrive != null)
                buffer = buffer.Replace("$clouddrive$", cloudDrive.LocalPath);

            return buffer;
        }

        private void Configure()
        {
            string traceFormat = RoleEnvironment.GetConfigurationSettingValue("TraceFormat");
            Tracer.format = ExpandKeywords(traceFormat);

            LogLevel logLevel = (LogLevel)Enum.Parse(typeof(LogLevel), RoleEnvironment.GetConfigurationSettingValue("ScheduledTransferLogLevelFilter"));
            TimeSpan scheduledTransferPeriod = TimeSpan.FromMinutes(int.Parse(RoleEnvironment.GetConfigurationSettingValue("ScheduledTransferPeriod")));

            // Windows Performance Counters
            List<string> counters = new List<string>();
            counters.Add(@"\Processor(_Total)\% Processor Time");
            counters.Add(@"\Memory\Available Mbytes");
            counters.Add(@"\TCPv4\Connections Established");
            counters.Add(@"\Network Interface(*)\Bytes Received/sec");
            counters.Add(@"\Network Interface(*)\Bytes Sent/sec");
            foreach (string counter in counters)
            {
                PerformanceCounterConfiguration counterConfig = new PerformanceCounterConfiguration();
                counterConfig.CounterSpecifier = counter;
                counterConfig.SampleRate = scheduledTransferPeriod;
                config.PerformanceCounters.DataSources.Add(counterConfig);
            }
            config.PerformanceCounters.ScheduledTransferPeriod = scheduledTransferPeriod;

            // Event Logs
            config.WindowsEventLog.DataSources.Add("System!*");
            config.WindowsEventLog.DataSources.Add("Application!*");
            
            // NB Dont do this -> config.WindowsEventLog.DataSources.Add("Security!*");

            config.WindowsEventLog.ScheduledTransferLogLevelFilter = logLevel; 
            config.WindowsEventLog.ScheduledTransferPeriod = scheduledTransferPeriod;
 
            // Basic Logs
            config.Logs.ScheduledTransferLogLevelFilter = logLevel;
            config.Logs.ScheduledTransferPeriod = scheduledTransferPeriod;

            // NB Only enables crash dumps for the worker role, not crash dumps for spawned processes
            // See http://www.robblackwell.org.uk/2010/10/27/advanced-debugging-on-windows-azure-with-adplus.html
            CrashDumps.EnableCollection(true);

        }

        public void InitialiseTraceConsole(string traceConnectionString)
        {

            // Start a CloudTraceListener so that we can trace from
            // a TraceConsole app running on the desktop
            string servicePath = "";
            string serviceNamespace = "";
            string issuerName = "";
            string issuerSecret = "";

            string[] traceConnectionSettings = traceConnectionString.Split(';');
            foreach (string traceConnectionSetting in traceConnectionSettings)
            {
                string[] setting = traceConnectionSetting.Split(new char[] { '=' }, 2);
                if (setting[0] == "ServicePath")
                    servicePath = setting[1];
                else if (setting[0] == "ServiceNamespace")
                    serviceNamespace = setting[1];
                else if (setting[0] == "IssuerName")
                    issuerName = setting[1];
                else if (setting[0] == "IssuerSecret")
                    issuerSecret = setting[1];
            }

            // Expand keywords in the service path to allow dynamic configuration based on deploymentid, roleinstance id etc
            servicePath = ExpandKeywords(servicePath);

            // Trace to service bus
            CloudTraceListener cloudTraceListener = new CloudTraceListener(servicePath, serviceNamespace, issuerName, issuerSecret);
            Trace.Listeners.Add(cloudTraceListener);
        }

        private string PackageReceiptFileName(string packageName)
        {
            string directory = Environment.GetEnvironmentVariable("RoleRoot");
            return Path.Combine(directory, packageName + ".receipt");
        }

        /// <summary>
        /// Creates a package receipt (a simple text file in the temp directory) 
        /// to record the successful download and installation of a package
        /// </summary>
        private void WritePackageReceipt(string packageName)
        {
            string receiptFileName = PackageReceiptFileName(packageName);

            TextWriter textWriter = new StreamWriter(receiptFileName);
            textWriter.WriteLine(DateTime.Now);
            textWriter.Close();

            Tracer.WriteLine(string.Format("Writing package receipt {0}", receiptFileName), "Information");
        }

        /// <summary>
        /// Checks a package in Blob Storage against any previous package receipt
        /// to determine whether to reinstall it
        /// </summary>
        private bool IsNewPackage(string containerName, string packageName)
        {
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

            blobClient.RetryPolicy = RetryPolicies.Retry(100, TimeSpan.FromSeconds(1));
            blobClient.Timeout = TimeSpan.FromSeconds(600);

            CloudBlobContainer container = blobClient.GetContainerReference(containerName);
            CloudBlockBlob blob = container.GetBlockBlobReference(packageName);

            blob.FetchAttributes();
            DateTime blobTimeStamp = blob.Attributes.Properties.LastModifiedUtc;

            DateTime fileTimeStamp = File.GetCreationTimeUtc(PackageReceiptFileName(packageName));

            if (fileTimeStamp.CompareTo(blobTimeStamp) < 0)
            {
                Tracer.WriteLine(string.Format("{0} is new or not yet installed.", packageName), "Information");
                return true;
            }
            else
            {
                Tracer.WriteLine(string.Format("{0} has previously been installed. Skipping download.", packageName), "Information");
                return false;
            }
        }

        /// <summary>
        /// Download a package from blob storage and unzip it
        /// </summary>
        /// <param name="containerName">The Blob storage container name</param>
        /// <param name="packageName">The name of the zip file package</param>
        /// <param name="workingDirectory">Where to extract the files</param>
        private void InstallPackage(string containerName, string packageName, string workingDirectory)
        {
           
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            
            blobClient.RetryPolicy = RetryPolicies.Retry(100, TimeSpan.FromSeconds(1));
            blobClient.Timeout = TimeSpan.FromSeconds(600);

            CloudBlobContainer container = blobClient.GetContainerReference(containerName);
            CloudBlockBlob blob = container.GetBlockBlobReference(packageName);

            Tracer.WriteLine(string.Format("Downloading {0} to {1}", blob.Uri, workingDirectory), "Information");

            using (MemoryStream stream = new MemoryStream())
            {
                blob.DownloadToStream(stream);

                Tracer.WriteLine(string.Format("Extracting {0}", packageName), "Information");

                var extractor = new SevenZipExtractor(stream);
                extractor.ExtractArchive(workingDirectory);
            }

            Tracer.WriteLine("Extraction finished", "Information");
        }

        private static void SetEnvironmentVariable(ProcessStartInfo startInfo, string variable, string value)
        {
            try
            {
                startInfo.EnvironmentVariables.Add(variable, value);
                Tracer.WriteLine(string.Format("Setting %{0}% to {1}", variable, value), "Information");
            }
            catch (ArgumentException)
            {
                Tracer.WriteLine(string.Format("Environment Variable %{0}% already set", variable), "Information");
            }
        }


        private void MountCloudDrive(string container,string vhdName, int size)
        {
            Tracer.WriteLine("Configuring CloudDrive", "Information");

            LocalResource localCache = RoleEnvironment.GetLocalResource("MyAzureDriveCache");

            const int TRIES = 30;

            // Temporary workaround for ERROR_UNSUPPORTED_OS seen with Windows Azure Drives
            // See http://blogs.msdn.com/b/windowsazurestorage/archive/2010/12/17/error-unsupported-os-seen-with-windows-azure-drives.aspx
            for (int i = 0; i < TRIES; i++)
            {
                   try
                   {
                       CloudDrive.InitializeCache(localCache.RootPath, localCache.MaximumSizeInMegabytes);
                       break;                   
                   }
                   catch (CloudDriveException ex)
                   {
                       if (!ex.Message.Equals("ERROR_UNSUPPORTED_OS"))
                       {
                           throw;
                       }

                       if (i >= (TRIES - 1))
                       {
                           // If the workaround fails then it would be dangerous to continue silently, so exit 
                           Tracer.WriteLine("Workaround for ERROR_UNSUPPORTED_OS see http://bit.ly/fw7qzo FAILED", "Error");
                           System.Environment.Exit(-1);
                       }

                       Tracer.WriteLine("Using temporary workaround for ERROR_UNSUPPORTED_OS see http://bit.ly/fw7qzo", "Information");
                       Thread.Sleep(10000);
                   }
            }            

            CloudBlobClient blobClient = cloudDriveStorageAccount.CreateCloudBlobClient();
            blobClient.GetContainerReference(container).CreateIfNotExist();

            CloudPageBlob pageBlob = blobClient
                .GetContainerReference(container)
                .GetPageBlobReference(vhdName);

            cloudDrive = cloudDriveStorageAccount.CreateCloudDrive(pageBlob.Uri.ToString());

            try
            {
                if (!pageBlob.Exists())
                {
                    Tracer.WriteLine(string.Format("Creating page blob {0}", cloudDrive.Uri), "Information");
                    cloudDrive.Create(size);
                }

                Tracer.WriteLine(string.Format("Mounting {0}", cloudDrive.Uri), "Information");
                cloudDrive.Mount(25, DriveMountOptions.Force);
            }
            catch (CloudDriveException e)
            {
                Tracer.WriteLine(e, "Error");
            }

            Tracer.WriteLine(string.Format("CloudDrive {0} mounted at {1}", cloudDrive.Uri, cloudDrive.LocalPath), "Information");

        }

        /// <summary>
        /// Runs a batch file or executable and hooks up stdout and stderr
        /// </summary>
        /// <param name="workingDirectory">Directory on disk</param>
        /// <param name="script">Batch file name (e.g. runme.bat)</param>
        private Process Run(string workingDirectory, string environmentVariables, string batchFile)
        {
            const string IP_ADDRESS = "ipaddress";
            const string DEPLOYMENT_ID = "deploymentid";
            const string ROLE_INSTANCE_ID = "roleinstanceid";
            const string CLOUD_DRIVE = "clouddrive";

            string command = Path.Combine(
                    workingDirectory,
                    batchFile);

            ProcessStartInfo startInfo = new ProcessStartInfo(command)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory
            };

            
            EnvironmentVariables(startInfo, environmentVariables);

            // Set an environment variable for each InstanceEndPoint
            foreach (var endpoint in RoleEnvironment.CurrentRoleInstance.InstanceEndpoints)
            {
                string variable = endpoint.Key;
                string value = endpoint.Value.IPEndpoint.Port.ToString();

                SetEnvironmentVariable(startInfo, variable, value);
                
                if (!startInfo.EnvironmentVariables.ContainsKey(IP_ADDRESS))
                {
                    string ipAddress = endpoint.Value.IPEndpoint.Address.ToString();
                    SetEnvironmentVariable(startInfo, IP_ADDRESS, ipAddress);
                }
            }

            SetEnvironmentVariable(startInfo, DEPLOYMENT_ID, RoleEnvironment.DeploymentId);
            SetEnvironmentVariable(startInfo, ROLE_INSTANCE_ID, RoleEnvironment.CurrentRoleInstance.Id);

            if (cloudDrive != null)
            {
                SetEnvironmentVariable(startInfo, CLOUD_DRIVE, cloudDrive.LocalPath);
            }

            Tracer.WriteLine(string.Format("Start Process {0}", command), "Information");

            Process process = new Process()
            {
                StartInfo = startInfo
            };

            process.ErrorDataReceived += (sender, e) => { Tracer.WriteLine(e.Data, "Information"); };
            process.OutputDataReceived += (sender, e) => { Tracer.WriteLine(e.Data, "Information"); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            Tracer.WriteLine(string.Format("Process {0}", process.Handle), "Information");

            return process;
        }

        public bool OnStart()
        {
            log.WriteEntry("OnStart", "OnStart");

            config = DiagnosticMonitor.GetDefaultInitialConfiguration();

            Configure();

            // Start the diagnostic monitor with the modified configuration.
            DiagnosticMonitor.Start("DiagnosticsConnectionString", config);

            // For information on handling configuration changes
            // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.
            RoleEnvironment.Changing += RoleEnvironmentChanging;
            RoleEnvironment.Changed += RoleEnvironmentChanged;

            // Set the maximum number of concurrent outbound connections 
            ServicePointManager.DefaultConnectionLimit = int.Parse(RoleEnvironment.GetConfigurationSettingValue("DefaultConnectionLimit"));

            // If a TraceConnectionString is specified then start a TraceConsole via the AppFabric Service Bus
            string traceConnectionString = RoleEnvironment.GetConfigurationSettingValue("TraceConnectionString");
            if (!String.IsNullOrEmpty(traceConnectionString))
                InitialiseTraceConsole(traceConnectionString);

            // Trace to Azure Diagnostics (table storage).
            DiagnosticMonitorTraceListener diagnosticMonitorTraceListener = new DiagnosticMonitorTraceListener();
            Trace.Listeners.Add(diagnosticMonitorTraceListener);

            Trace.AutoFlush = true;

            return true;
        }

        public void Run()
        {
            Tracer.WriteLine("WorkerRole entry point called", "Information");
            log.WriteEntry("Run", "Run");

            Tracer.WriteLine(string.Format("AzureRunMe {0}", Version()), "Information");
            Tracer.WriteLine("Copyright (c) 2010 - 2011 Active Web Solutions Ltd [www.aws.net]", "Information");
            Tracer.WriteLine("", "Information");

            Tracer.WriteLine(string.Format("DeploymentId: {0}", RoleEnvironment.DeploymentId), "Information");
            Tracer.WriteLine(string.Format("RoleInstanceId: {0}", RoleEnvironment.CurrentRoleInstance.Id), "Information");
            Tracer.WriteLine(string.Format("MachineName: {0}", Environment.MachineName), "Information");
            Tracer.WriteLine(string.Format("ProcessorCount: {0}", Environment.ProcessorCount), "Information");
            Tracer.WriteLine(string.Format("Time: {0}", DateTime.Now), "Information");
            
            try
            {

                // If specified, mount a cloud drive
                string cloudDrive = RoleEnvironment.GetConfigurationSettingValue("CloudDrive");
                if (cloudDrive != "")
                {
                    int cloudDriveSize = Int32.Parse(RoleEnvironment.GetConfigurationSettingValue("CloudDriveSize"));
                    cloudDrive = ExpandKeywords(cloudDrive);
                    string[] parts = cloudDrive.Split('\\');
                    MountCloudDrive(parts[0], parts[1], cloudDriveSize);
                }

                workingDirectory = RoleEnvironment.GetConfigurationSettingValue("WorkingDirectory");
                workingDirectory = ExpandKeywords(workingDirectory);

                // set 7zip dll path
                string sevenZipPath = Path.Combine(workingDirectory, @"Redist\7z64.dll");
                SevenZipExtractor.SetLibraryPath(sevenZipPath);

                environmentVariables = RoleEnvironment.GetConfigurationSettingValue("EnvironmentVariables");

                bool alwaysInstallPackages = bool.Parse(RoleEnvironment.GetConfigurationSettingValue("AlwaysInstallPackages"));
                Tracer.WriteLine(string.Format("AlwaysInstallPackages: {0}", alwaysInstallPackages), "Information");

                // Retrieve the semicolon delimitted list of zip file packages and install them
                string[] packages = RoleEnvironment.GetConfigurationSettingValue("Packages").Split(';');
                foreach (string package in packages)
                {
                    try
                    {
                        if (package != string.Empty)
                        {
                            // Parse out the container\file pair
                            string[] fields = package.Split(new char[] {'/','\\'}, 2);

                            string containerName = fields[0];
                            string packageName = fields[1];

                            if (alwaysInstallPackages || IsNewPackage(containerName, packageName))
                            {
                                InstallPackage(containerName, packageName, workingDirectory);
                                WritePackageReceipt(packageName);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Tracer.WriteLine(string.Format("Package \"{0}\" failed to install, {1}", package, e), "Information");
                    }
                }

                string commands = RoleEnvironment.GetConfigurationSettingValue("Commands");
                RunCommands(commands);

                // If DontExit is set, then keep runing even though all the Commands have finished
                // (Useful if you want to RDP in afterwards). 
                bool dontExit = bool.Parse(RoleEnvironment.GetConfigurationSettingValue("DontExit"));

                Tracer.WriteLine(string.Format("DontExit: {0}", dontExit), "Information");

                if (dontExit)
                    while (!isStopping)
                        Thread.Sleep(1000);

                Tracer.WriteLine("Run method exiting", "Information");

            }
            catch (Exception e)
            {
                Tracer.WriteLine(e, "Error");
            }

            Tracer.WriteLine("WorkerRole exit", "Critical");
        }

        public void OnStop()
        {
            Tracer.WriteLine("OnStop", "Critical");
            log.WriteEntry("OnStop", "OnStop");

            isStopping = true;

            string commands = RoleEnvironment.GetConfigurationSettingValue("OnStopCommands");
            RunCommands(commands);

            if (cloudDrive != null)
            {
                Tracer.WriteLine(string.Format("Unmounting {0} from {1}", cloudDrive.Uri, cloudDrive.LocalPath), "Information");
                cloudDrive.Unmount();
            }
        }

        private void EnvironmentVariables(ProcessStartInfo processStartInfo, string environmentVariables)
        {
            string[] assignments = environmentVariables.Split(';');

            foreach (string assignment in assignments)
            {
                if (!String.IsNullOrEmpty(assignment)) {
                    string[] parts = assignment.Split('=');
                    SetEnvironmentVariable(processStartInfo, parts[0].Trim(), parts[1].Trim());
                }
            }
        }

        private void RunCommands(string commandList)
        {
            // Spawn new a new process for each command
            List<Process> processes = new List<Process>();
            string[] commands = commandList.Split(';');
            foreach (string command in commands)
            {
                try
                {
                    if (command != string.Empty)
                    {
                        Process process = Run(workingDirectory, environmentVariables, command);
                        processes.Add(process);
                    }
                }
                catch (Exception e)
                {
                    Tracer.WriteLine(string.Format("Command \"{0}\" , {1}", command, e), "Information");
                }
            }

            // Wait for all processes to exit
            foreach (Process process in processes)
            {
                process.WaitForExit();
                Tracer.WriteLine(string.Format("Exit {0}, code {1}", process.Handle, process.ExitCode), "Information");
            }

        }

        private void RoleEnvironmentChanging(object sender, RoleEnvironmentChangingEventArgs e)
        {
            Tracer.WriteLine("RoleEnvironmentChanging", "Information");
            log.WriteEntry("RoleEnvironmentChanging", "RoleEnvironmentChanging");

            // These configuration changes don't require a restart
            string[] exemptConfigurationItems =
                new[] { "ScheduledTransferLogLevelFilter", "ScheduledTransferPeriod", "LogFormat" };

            var changes = from x in e.Changes.OfType<RoleEnvironmentConfigurationSettingChange>() select x.ConfigurationSettingName;

            foreach (string change in changes)
            {
                if (!exemptConfigurationItems.Contains(change))
                    // Restart this instance
                    e.Cancel = true;
            }

            if (e.Cancel)
                Tracer.WriteLine("RoleEnvironmentChanging cancelled", "Information");
            else
                Tracer.WriteLine("RoleEnvironmentChanging continued", "Information");

        }

        private void RoleEnvironmentChanged(object sender, RoleEnvironmentChangedEventArgs e)
        {
            Tracer.WriteLine("RoleEnvironmentChanged", "Information");
            log.WriteEntry("RoleEnvironmentChanged", "RoleEnvironmentChanged");

            Configure();
        }
    }
}
