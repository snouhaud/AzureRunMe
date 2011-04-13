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
using System.Diagnostics;
using System.Linq;
using System.Net;
using Microsoft.ServiceBus.Samples;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace WorkerRole
{
    public class WorkerRole : RoleEntryPoint
    {

        RunMe runMe = new RunMe();

        public override void Run()
        {
            Tracer.WriteLine("WorkerRole entry point called", "Information");
            Log.WriteEntry("WorkerRole.Run() called");
            runMe.Run();
            Tracer.WriteLine("WorkerRole exit", "Critical");
        }

        public override bool OnStart()
        {
            Log.WriteEntry("WorkerRole.OnStart() called");
            runMe.OnStart();

            return base.OnStart();
        }

        public override void OnStop()
        {
            Tracer.WriteLine("OnStop", "Critical");
            Log.WriteEntry("WorkerRole.OnStop() called");
            runMe.OnStop();

            base.OnStop();
        }
   
    }
}
