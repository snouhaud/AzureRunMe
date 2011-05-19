#region Copyright (c) 2010 - 2011 Active Web Solutions Ltd
//
// (C) Copyright 2010 - 2011 Active Web Solutions Ltd
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

namespace WorkerRole
{
    /// <summary>
    /// A simple wrapper so that we can easily augment tracing
    /// </summary>
    class Tracer
    {
        // In deployments with multiple instances, it's convenient to 
        // prefix the source machine to log entries
        public static string format = Environment.MachineName + ":: {0:u} {1}";

        public static void WriteLine(string message, string category) {
            Trace.WriteLine(string.Format(format,DateTime.Now, message), category);
        }

        public static void WriteLine(object o, string category)
        {
            WriteLine(o.ToString(), category);
        }
    }
}
