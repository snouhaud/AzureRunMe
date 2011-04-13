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

public class LogEntry : TableServiceEntity
{
    public LogEntry()
    { }

    public LogEntry(string eventName, string details)
    {
        DateTime createdUtc = DateTime.UtcNow;

        // Keep all entries for one machine together
        this.PartitionKey = Environment.MachineName;

        // Use reverse time so that entries are sorted chronologically with latest entries appearing first
        this.RowKey = String.Format("{0}_{1}", (DateTime.MaxValue - createdUtc).Ticks.ToString("d19"), Guid.NewGuid().ToString());

        this.Timestamp = createdUtc;
        this.InstanceId = RoleEnvironment.CurrentRoleInstance.Id;
        this.EventName = eventName;
        this.Details = details;
        
    }

    public string InstanceId { get; set; }
    public string EventName { get; set; }
    public string Details { get; set; }
}