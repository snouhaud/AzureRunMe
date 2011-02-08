AzureRunMe 1.0.0.15
===================

Probably the quickest way to get your legacy or third-party code running on Windows Azure.

N.B. AzureRunMe has moved to https://github.com/blackwre/AzureRunMe

Introduction
------------

AzureRunMe is a boostrap program that provides an off-the-shelf CSPKG file that you can upload to Windows Azure Conpute and run.

From there you can upload you code via ZIP files in Blob Store and kick off your processes in a repeatable way, just by changing configuration.

AzureRunMe preconfigures Remote Desktop access, making it easy to debug and diagnose problems.

If you are using Java, Clojure, C++ or other languages this might be the quickest way to get your code running in Azure without having to worry about building any .NET code.

Background
----------

There are a number of code samples that show how to run Java, Ruby, Python etc on Windows Azure, but they all vary in approach and complexity. 
Everyone seems to write their own boostrap program. I thought there ought to be a simplified, standardised way.

I wanted something simple that took a self contained ZIP file, unpacked it and just executed a batch file. 
All the role information like ipaddress and port could be passed as environment variables %IPAddress% or %Http% etc.

I wanted ZIP files to be stored in Blob store to allow them to be easily updated with all Configuration settings in the Azure Service Configuration.

I wanted real time tracing of stdio, stderr, debug, log4j etc.

AzureRunMe was born, and to my very great suprise, is now being used by a number of commercial organisations, hobbyists and even Microsoft themselves!

Example Scenarios
-----------------

Run one or more simple Java console apps:
* One or more CSharp console apps,without any code change
* A Tomcat hosted web application
* A JBOSS hosted app
* A legacy C / C++ application
* A Clojure / Compojure app
* A Common Lisp app
* A Ruby on Rails web app
* Use PortBridgeAgent to proxy some ports from an intranet server e.g. LDAP.
* Use PortBridge to proxy internal endpoints back to on premises e.g. JPDA to your Eclipse debugger

Fast Start
----------

There are three files in the dist directory

AzureRunMe.cspkg - The package file, ready to upload and use
ServiceConfiguration.cscfg - The configuration file - you'll need to edit this with your various credentials
AzureRunMeRDP.pfx - A sample certificate that you can use for RDP **

Upload the certificate (password is tiger123!)

You can remote desktop by clicking on teh RDP link in the portal - 

user: scott
password: tiger123!

** NB This certificate is provided for quickstart /demo only - you should use your own for any serious, or
production work!! You should also change the RDP password ASAP.

Getting Started
---------------

Organise your project so that it can all run from under one directory and has a batch file at the top level.

In my case, I have a directory called c:\foo. Under that I have copied the Java Runtime JRE. I have my JAR files in a subdirectory called test and a runme.bat above those that looks like this:

	cd test
	..\jre\bin\java -cp Test.jar;lib\* Test %http%

I can bring up a console window using cmd and change directory into Foo

Then I can try things out locally by typing:

	C:>Foo> set http=8080
	C:>Foo> runme.bat

The application runs and serves a web page on port 8080.

I package the jre directory as jre.zip and the test directory along with the runme.bat file together as dist.zip.

Having two ZIP files saves me time - I don't have to keep uploading the JRE each time I change my Java application.

My colleague has a ruby.zip file containing Ruby and Mongrel and his web application in rubyapp.zip in a similar way.

Upload the zip files to blob store. Create a container called "packages" and put them in there. The easiest way to do this is via Cerebrata Cloud Studio. Another alternative is to use the UploadBlob command line app distributed with this project.

The next step is to build and deploy Azure RunMe ..

Load Azure RunMe in Visual Studio and build.

Change the ServiceConfiguration.cscfg file: 

Update DiagnosticsConnectionString with your Windows Azure Storage account details so that AzureRunme can send trace information to storage. 

Update DataConnectionString with your Windows Azure Storage account details so that AzureRunme can get ZIP files from Blob store. 

Change the TraceConnectionString to your appFabric Service Bus credentials so that you can use the CloudTraceListener to trace your applications.

By default, Packages is set to "packages\jre.zip;packages\dist.zip"  which means download and extract jre.zip then download and extract  dist.zip, before executing runme.bat (Specified as the Command configuration setting.

Click on AzureRunMe and Publish your Azure package. Sign into the [Windows Azure Developer Portal](http://windows.azure.com).

Create a New Hosted Service and upload the package and config to your Windows Azure account. You are nearly ready to go.

Change the app.config file for TraceConsole to include your own service bus credentials.

Right Click, Publish, Configure Remote Desktop Connection.

Run the TraceConsole locally on your desktop machine. Now run the Azure instance by clicking on Run in the Windows Azure Developer Portal.

Deployment might take some time (maybe 10 minutes or more), but after a while you should see trace information start spewing out in your console app. You should see that it's downloading your ZIP files and extracting the, Finally it should run your runme.bat file.

If all goes well your app should now be running in the cloud!

Environment Variables
---------------------

Before running your script, the following environment variables are set up

* %IPAddress% to the IP Address of this instance.

All InputEndPoints are setup too, according to the CSDEF, by default this is:

* %http% the port corresponding to 80
* %telnet% the port corresponding to 23
* %http-alt% the port corresponding to 8080

There are also two default InternalEndPoints %port1% and %port2%

Compiling AzureRunMe
--------------------

Prerequisites:

* Visual Studio 2010
* The Windows Azure SDK & Tools for Visual Studio
* The Windows Azure AppFabric 
(see http://msdn.microsoft.com/en-us/windowsazure/cc974146.aspx )

Diagnostics
-----------

By default, log files, event logs and some performance counters are written to table storage using Windows Azure Diagnostics.

The level of logging and the frequency (in minutes) with which logs are shipped to storage can be changed in the CSCFG file.

		<Setting name="ScheduledTransferLogLevelFilter" value="Verbose"/>
		<Setting name="ScheduledTransferPeriod" value="1"/>

I recommend Cerebrata's [Windows Azure Diagostics Manager](http://www.cerebrata.com/products/AzureDiagnosticsManager/Default.aspx) for viewing
the output. (I really ought to be on commission!).

Packages
--------

You store your packages as a series of ZIP files in Azure Blob Store

The following config setting controls which files are extacted and in which order.

		<Setting name="Packages" value="packages/jre.zip;packages/dist.zip" />

It's usually a good idea to separate your deployment up into several packages so that you dont have large uploads everytime something changes. Here is a recent example

	<Setting name="Packages" value="packages/jdk1.6.0_21.zip;packages/sed.zip;packages/portbridge.zip;packages/apache-tomcat-6.0.29.zip" />

Commands
--------

To run a single command, use a config like this

	<Setting name="Commands" value="runme.bat"/>

If you want to start multiple processes, you can specify them in a semicolon separated list, like this

	<Setting name="Commands" value="portbridge.exe;tomcat.bat"/>

If you leave Commands blank and set DontExit, like this
	
	<Setting name="Commands" value=""/>
	<Setting name="DontExit" value="True" />

Then the instance boots up without running any code, but you can still remote desktop in and start playing.

Optionally you can run some commands when the instance is stopped (i.e. via the OnStop method)

	<Setting name="OnStopCommands" value="cleanup.bat"/>

DefaultConnectionLimit
----------------------

The default connection limit specifies the number of outbound TCP connections.If your code makes a lot of outbound requests, you may need to tweak this.

		<Setting name="DefaultConnectionLimit" value ="12"/>

Configuration Keyword Expansions
--------------------------------

Several of the configuration file settings support expansion of these variables

* $deploymentid$ expands to the deployment id - something like 3bdbf69e94c645f1ab19f2e428eb05fe
* $roleinstanceid$ expands to the role instance id - something like {"WorkerRole_IN_0"}
* $computername$ expands to the computer NETBIOS name - something like RD00155D3A111A
* $guid$ expands to a new Globally Unique Identifier
* $now$ expands to DateTime.Now (the current time).
* $roleroot$" expands to the role root directory
* $clouddrive$" expands to the directory where the clouddrive is mounted
* $approot$ expands to $roleroot$\approot

Advanced Tracing
----------------

With a fixed service path of trace/azurerunme, all service bus based tracing from all instances goes to the same TraceConsole.

With multiple instances or multiple deployments that can be confusing - it's hard to see which instance wrote which message when they are interleaved.

For that reason, the service path is configurable with keyword expansions (See above).

The default setting is now trace/$roleinstanceid$ which with single instance deployments becomes trace/WorkerRole_IN_0

so for more advanced deployments, a setting like this might be better:

	<Setting name="TraceConnectionString" value="ServicePath=$deploymentid$/$roleinstanceid$;ServiceNamespace=YOURNAMESPACE;IssuerName=YOURISSUERNAME;IssuerSecret=YOURISSUERSECRET" />

That way you can have separate console listeners attached to different instances at separate endpoints

	sb://YOURNAME.servicebus.windows.net/3bdbf69e94c645f1ab19f2e428eb05fe/WorkerRole_IN_0/

and

	sb://YOURNAME.servicebus.windows.net/3bdbf69e94c645f1ab19f2e428eb05fe/WorkerRole_IN_1/

It's also possible to configure the trace output format similarly

	<Setting name="LogFormat" value="$computername$: {0:u} {1}"/>

Keywords like $computername$ get expanded as above and the rest of the string is as expected by String.Format with field 0 being the message and field 1 being the time.

Cloud Drives
------------

You can mount a Windows Azure Drive (aka X-Drive), using the following config settings

	<Setting name="CloudDriveConnectionString" value="DefaultEndpointsProtocol=http;AccountName=YOURACCOUNTNAME;AccountKey=YOURACCOUNTKEY" />
	<Setting name="CloudDrive" value ="drives\mydrive.vhd"/>
	<Setting name="CloudDriveSize" value="64" />

The CloudDriveConnectionString is separate to the DataConnectionString because cloud drives don't
support HTTPS.

CloudDrive supports the same expansion syntax as the service path above so that VHD file names can
be tailored according to your requirements. "drives\$computername$.vhd" is often useful.

CloudDriveSize is specified in megabytes.

At run time, the cloud drive location is available through the %clouddrive% environment variable.

DontExit
--------

If DontExit is set to True then the WorkerRole's Run Method wont exit until the WorkerRole is explicitly stopped using OnStop.
If DontExit is set to False then the WorkerRole's Run Method will exit as soon as any processes created from the "Commands" section have exited.

	<Setting name="DontExit" value="True" />


AlwaysInstallPackages
---------------------

AlwaysInstallPackages True ensures that packages are always downloaded and extracted from Blob Storage even if they have previously been installed 
(This is the default and backwards compatible behaviour). 

	<Setting name="AlwaysInstallPackages" value="True" />

Setting AlwaysInstall to False can optimise the time it takes to restart an instance, by only reinstalling packages that have been updated in Blob Store.


Batch file tricks
-----------------

It's common to kick off your processes from a batch file and its idoimatic to call it runme.bat

It can contain all the old-fashioned DOS-like commands echo etc.

I often use

	SET

to display a list of all the environment variables.

Some useful variables include: 

	%ipaddress%  
	%computername%
	%deploymentid%
	%roleinstanceid%

I have a copy of SED (The Unix Stream editor) packaged in a ZIP, and this allows me to perform simple file based configurations changes:

When I start tomcat, I do it like this rather than using the startup script

	apache-tomcat\bin\catalina.bat run

Whilst the CMD shell doesnt really have proper job control, you can start background processes with the START command.

Issues
------

There are a number of Java apps that use the java.nio library to try to establish non blocking IO connections to the loopback adapter. This isnt allowed by the Windows Azure security policy.
If you are working with Java, you might like to read http://www.robblackwell.org.uk/2010/11/06/java-on-windows-azure-roundup.html  NEEDS RETESTING AND REVALIDATING

For compatability issues with specific applications, see below.

Unfortunately we cant configure more that 5 end points on the Load balancer, so you may need to recompile with your own CSDEF file to fiddle with ports.
The vmsize attribute is unfortunately, also baked into this file.

Clojure + Compojure
-------------------

Works as of Summer 2010

Tomcat
------

Works.

Restlet 
-------

Works

Jetty
-----

Runs, but not with NIO support. NEEDS RETESTING AND REVALIDATING

JBOSS
-----

Not extensively tested, but a basic system comes up for web serving. Some issues I belive with JCA etc because it uses java.nio. NEEDS RETESTING AND REVALIDATING

ApacheDS LDAP Server
--------------------

Doesn't work because it uses java.nio NEEDS RETESTING AND REVALIDATING

ANSI C Code, C++
----------------

With the appropriate Visual C++ runtime and libraries this is known to work.  You may need to install your VC++ runtime via Startup.cmd for legacy runtime redistributables.

ADPlus Debugging tools
----------------------

Need a crash dump? See http://www.robblackwell.org.uk/2010/10/27/advanced-debugging-on-windows-azure-with-adplus.html

SysInternals PSTools
--------------------

Works.

The trick is to use the /accepteula flag! Thanks to Jsun for this one!

AzureCommandLineTools
---------------------

UploadBlob adnd DownloadBlob have been replaced with a more comprehensive set of command line tools, see https://github.com/blackwre/AzureCommandLineTools.

	> PutBlob myfilename mycontainer/myblob
	> GetBlob mycontainer/myblob

7Zip
----

That old swiss army knife 7 Zip http://www.7-zip.org/, works too. Thanks to Jsun for this one!

	> 7zip x myfile.zip


Credits
-------

This project uses Ionic Zip library, part of a CodePlex project at http://www.codeplex.com/DotNetZip which is distributed under the terms of the Microsoft Public License.

TraceConsole and TraceListener are code samples from the Microsoft appFabric SDK (with minor modifications).

Thanks to Jsun for lots of constructive ideas and testing to near destruction!

Thanks to Steve Marx for inspirational code samples and workarounds.

Commercial Support
------------------

This code is now being used for real, on several commercial projects! 

See http://www.aws.net/azurelaunchpad or contact info@aws.net if you'd like to hire us or would like to purchase a formal support contract.

Rob Blackwell

January 2011

