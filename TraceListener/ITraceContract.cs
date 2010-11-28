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
    using System.ServiceModel;

    [ServiceContract(Name = "ITraceContract",
                     Namespace = "http://samples.microsoft.com/ServiceModel/Relay/CloudTrace",
                     SessionMode = SessionMode.Allowed)]
    public interface ITraceContract
    {
        [OperationContract(IsOneWay = true, Name = "Write1")]
        void Write(string message);

        [OperationContract(IsOneWay = true, Name = "Write2")]
        void Write(string message, string category);

        [OperationContract(IsOneWay = true, Name = "WriteLine1")]
        void WriteLine(string message);

        [OperationContract(IsOneWay = true, Name = "WriteLine2")]
        void WriteLine(string message, string category);

        [OperationContract(IsOneWay = true, Name = "Fail1")]
        void Fail(string message);

        [OperationContract(IsOneWay = true, Name = "Fail2")]
        void Fail(string message, string detailMessage);
    }

    public interface ITraceChannel : ITraceContract, IClientChannel { }
}