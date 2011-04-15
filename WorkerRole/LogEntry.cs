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
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.StorageClient;

namespace WorkerRole
{
    public class LogEntry : TableServiceEntity
    {
        public LogEntry()
        { }

        public LogEntry(string eventName, string notes, string label)
        {
            DateTime createdUtc = DateTime.UtcNow;

            // Use reverse time so that entries are sorted chronologically with latest entries appearing first
            this.PartitionKey = String.Format("{0}_{1}", (DateTime.MaxValue - createdUtc).Ticks.ToString("d19"), Guid.NewGuid().ToString());

            this.RowKey = RoleEnvironment.DeploymentId;

            this.Timestamp = createdUtc;
            this.InstanceId = RoleEnvironment.CurrentRoleInstance.Id;
            this.EventName = eventName;
            this.Notes = notes;
            this.RoleInstanceId = RoleEnvironment.CurrentRoleInstance.Id;
            this.MachineName = Environment.MachineName;
            this.Label = label;

        }

        public string InstanceId { get; set; }
        public string EventName { get; set; }
        public string Notes { get; set; }
        public string RoleInstanceId { get; set; }
        public string MachineName { get; set; }
        public string Label { get; set; }
    }
}