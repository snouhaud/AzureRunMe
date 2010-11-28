//---------------------------------------------------------------------------------
// Microsoft (R) .NET Services
// Software Development Kit
// 
// Copyright (c) Microsoft Corporation. All rights reserved.  
//
// THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, 
// EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED WARRANTIES 
// OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE. 
//---------------------------------------------------------------------------------

namespace Microsoft.ServiceBus.Samples
{
    using System;
    using System.ServiceModel;
    using System.Diagnostics;

    [ServiceBehavior(Name = "TraceService", Namespace = "http://samples.microsoft.com/ServiceModel/Relay/CloudTrace")]
    class TraceService : ITraceContract
    {
        #region ITraceContract Members

        public void Write(string message)
        {
            //Write to the Console
            Console.Write(message);

            //Also write to a local Trace Listener
            Trace.Write(message);
        }

        public void Write(string message, string category)
        {
            //Write to the Console
            Console.Write(message,category);

            //Also write to a local Trace Listener
            Trace.Write(message, category);
        }

        public void WriteLine(string message)
        {
            //Write to the Console
            Console.WriteLine(message);

            //Also write to a local Trace Listener
            Trace.WriteLine(message);
        }

        public void WriteLine(string message, string category)
        {
            //Write to the Console
            //Console.WriteLine(message,category); // Bug - what if message contains format specifiers!?!
            Console.WriteLine("{0} {1}", category, message);

            //Also write to a local Trace Listener
            Trace.WriteLine(message, category);

        }

        public void Fail(string message)
        {
            //Write to the Console
            Console.WriteLine(String.Format("Fail: {0}", message));

            //Also write to a local Trace Listener
            Trace.Fail(message);
        }

        public void Fail(string message, string detailMessage)
        {
            //Write to the Console
            Console.WriteLine(String.Format("Fail: {0}, {1}", message,detailMessage));

            //Also write to a local Trace Listener
            Trace.Fail(message, detailMessage);
        }

        #endregion
    }
}
