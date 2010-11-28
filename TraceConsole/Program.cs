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
    using System.Configuration;
    using System.ServiceModel;
    using System.ServiceModel.Description;
    using Microsoft.ServiceBus;

    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("CloudTrace Console");
            Console.WriteLine("Connecting ...");

            //Retrieve Settings from App.Config
            string servicePath = ConfigurationManager.AppSettings["CloudTraceServicePath"];
            string serviceNamespace = ConfigurationManager.AppSettings["CloudTraceServiceNamespace"];
            string issuerName = ConfigurationManager.AppSettings["CloudTraceIssuerName"];
            string issuerSecret = ConfigurationManager.AppSettings["CloudTraceIssuerSecret"];

            //Construct a Service Bus URI
            Uri uri = ServiceBusEnvironment.CreateServiceUri("sb", serviceNamespace, servicePath);

            //Create a Behavior for the Credentials
            TransportClientEndpointBehavior sharedSecretServiceBusCredential = new TransportClientEndpointBehavior();
            sharedSecretServiceBusCredential.CredentialType = TransportClientCredentialType.SharedSecret;
            sharedSecretServiceBusCredential.Credentials.SharedSecret.IssuerName = issuerName;
            sharedSecretServiceBusCredential.Credentials.SharedSecret.IssuerSecret = issuerSecret;

            //Create the Service Host 
            ServiceHost host = new ServiceHost(typeof(TraceService), uri);
            ServiceEndpoint serviceEndPoint = host.AddServiceEndpoint(typeof(ITraceContract), new NetEventRelayBinding(), String.Empty);
            serviceEndPoint.Behaviors.Add(sharedSecretServiceBusCredential);
            
            //Open the Host
            host.Open();
            Console.WriteLine("Connected To: " + uri.ToString());
            Console.WriteLine("Hit [Enter] to exit");
            
            //Wait Until the Enter Key is Pressed and Close the Host
            Console.ReadLine();
            host.Close();         
        }
    }
}
