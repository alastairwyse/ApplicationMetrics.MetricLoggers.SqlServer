ApplicationMetrics.MetricLoggers.SqlServer
---
An implementation of an [ApplicationMetrics](https://github.com/alastairwyse/ApplicationMetrics) [metric logger](https://github.com/alastairwyse/ApplicationMetrics/blob/master/ApplicationMetrics/IMetricLogger.cs) which writes metrics and instrumentation information to a Microsoft SQL Server database.


#### Setup

##### 1) Create the Database and Objects
Run the [CreateDatabase.sql](https://github.com/alastairwyse/ApplicationMetrics.MetricLoggers.SqlServer/blob/master/ApplicationMetrics.MetricLoggers.SqlServer/Resources/CreateDatabase.sql) script against a SQL Server instance to create the 'ApplicationMetrics' database and objects to store the metrics.  The 'CREATE DATABASE' statement needs to be run separately, before the remainder of the script.  The name of the database can be changed via a find/replace operation on the script (replacing all instances of 'ApplicationMetrics' with a desired database name).  Alternatively, the objects can be created in an existing database.  In any case, the 'InitialCatalog' component of the connection string passed to the SqlServerMetricLogger class should be set to the matching database name.

##### 2) Setup and Call the SqlServerMetricLogger Class

The code below demonstrates the setup and use case (with fake metrics logged) of the SqlServerMetricLogger class...

````C#
var connStringBuilder = new SqlConnectionStringBuilder();
connStringBuilder.DataSource = "127.0.0.1";
connStringBuilder.InitialCatalog = "ApplicationMetrics";
connStringBuilder.Encrypt = false;
connStringBuilder.Authentication = SqlAuthenticationMethod.SqlPassword;
connStringBuilder.UserID = "sa";
connStringBuilder.Password = "password";

using (var bufferProcessor = new SizeLimitedBufferProcessor(5))
using (var metricLogger = new SqlServerMetricLogger("DefaultCategory", connStringBuilder.ToString(), 20, 10, bufferProcessor, true))
{
    metricLogger.Start();

    Guid beginId = metricLogger.Begin(new MessageSendTime());
    Thread.Sleep(20);
    metricLogger.Increment(new MessageSent());
    metricLogger.Add(new MessageSize(), 2661);
    metricLogger.End(beginId, new MessageSendTime());

    metricLogger.Stop();
}
````

SqlServerMetricLogger accepts the following constructor parameters...

| Parameter Name | Description |
| -------------- | ----------- |
| category | The category to log the metrics under.  The ability to specify a category allows instances of the same metrics to be logged, but also distinguished from each other... e.g. in the case of a multi-threaded application, the category could be set to reflect an individual thread. |
| connectionString | The connection string to connect to SQL Server. |
| retryCount | The number of times an operation against the database should be retried in the case of execution failure. |
| retryInterval | The time in seconds between operation retries. |
| bufferProcessingStrategy | An object implementing [IBufferProcessingStrategy](https://github.com/alastairwyse/ApplicationMetrics/blob/master/ApplicationMetrics.MetricLoggers/IBufferProcessingStrategy.cs) which decides when the buffers holding logged metric events should be flushed (and be written to SQL Server). |
| intervalMetricChecking | Specifies whether an exception should be thrown if the correct order of interval metric logging is not followed (e.g. End() method called before Begin()).  This parameter is ignored when the the SqlServerMetricLogger operates in ['interleaved'](https://github.com/alastairwyse/ApplicationMetrics#interleaved-interval-metrics) mode. |

Retries are implemented using the [configurable retry logic](https://docs.microsoft.com/en-us/sql/connect/ado-net/configurable-retry-logic-sqlclient-introduction?view=sql-server-ver16) functionality in the Microsoft.Data.SqlClient library.

##### 3) Viewing and Querying Logged Metrics
A view is available for each of the 4 different types of metrics (e.g. 'CountMetricInstancesView', 'AmountMetricInstancesView', etc...).  Additionally a view which consolidates all logged metrics ('AllMetricInstancesView') can be queried.  Standard SQL can be used to filter and aggregate the contents of these views.

A sample of the contents of 'AllMetricInstancesView' appears below...

![AllMetricInstancesView contents example](http://alastairwyse.net/applicationmetrics/images/allmetricinstancesview-example.png)

#### Links
The documentation below was written for version 1.* of ApplicationMetrics.  Minor implementation details may have changed in versions 2.0.0 and above, however the basic principles and use cases documented are still valid.  Note also that this documentation demonstrates the older ['non-interleaved'](https://github.com/alastairwyse/ApplicationMetrics#interleaved-interval-metrics) method of logging interval metrics.

Full documentation for the project...<br>
[http://www.alastairwyse.net/methodinvocationremoting/application-metrics.html](http://www.alastairwyse.net/methodinvocationremoting/application-metrics.html)

A detailed sample implementation...<br>
[http://www.alastairwyse.net/methodinvocationremoting/sample-application-5.html](http://www.alastairwyse.net/methodinvocationremoting/sample-application-5.html)

#### Release History

<table>
  <tr>
    <td><b>Version</b></td>
    <td><b>Changes</b></td>
  </tr>
  <tr>
    <td valign="top">1.0.0</td>
    <td>
      Initial release.
    </td>
  </tr>
</table>