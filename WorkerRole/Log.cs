#region Copyright (c) 2011 Active Web Solutions Ltd
//
// (C) Copyright 2011 Active Web Solutions Ltd
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
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.StorageClient;

namespace WorkerRole
{

    public static class Log
    {
        static readonly string LOG_TABLE_NAME = "AzureRunMeLog";
        static object semaphore = new object();
        static bool initialized = false;

        public class LogEntry : TableServiceEntity
        {
            public LogEntry()
            { }

            public LogEntry(string message)
            {
                DateTime createdUtc = DateTime.UtcNow;

                // separate partition key for each day
                this.PartitionKey = (DateTime.MaxValue - createdUtc.Date).Ticks.ToString("d19");
                this.RowKey = String.Format("{0}_{1}", (DateTime.MaxValue - createdUtc).Ticks.ToString("d19"), Guid.NewGuid().ToString());
                this.Timestamp = createdUtc;
                this.ComputerName = Environment.MachineName;
                this.InstanceId = RoleEnvironment.CurrentRoleInstance.Id;
                this.Message = message;
            }

            public string ComputerName { get; set; }
            public string InstanceId { get; set; }
            public string Message { get; set; }
        }


        public static void Initialize()
        {
            Tracer.WriteLine("Log.Initialize() called", "Information");

            try
            {
                var account = CloudStorageAccount.Parse(RoleEnvironment.GetConfigurationSettingValue("LogConnectionString"));
                var dataContext = account.CreateCloudTableClient().GetDataServiceContext();

                account.CreateCloudTableClient().CreateTableIfNotExist(LOG_TABLE_NAME);

                lock (semaphore)
                    initialized = true;
            }
            catch (Exception e)
            {
                Tracer.WriteLine(e.ToString(), "Critical");
            }

        }

        public static void WriteEntry(string message)
        {
            if (initialized == false)
                Initialize();

            try
            {
                var account = CloudStorageAccount.Parse(RoleEnvironment.GetConfigurationSettingValue("LogConnectionString"));
                var dataContext = account.CreateCloudTableClient().GetDataServiceContext();

                dataContext.AddObject(LOG_TABLE_NAME, new LogEntry(message));
                dataContext.SaveChangesWithRetries();
            }
            catch (Exception e)
            {
                Tracer.WriteLine(e.ToString(), "Critical");
            }
        }



    }
}
