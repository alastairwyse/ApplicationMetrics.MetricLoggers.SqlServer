ApplicationMetrics.MetricLoggers.SqlServer
---
An implementation of an [ApplicationMetrics](https://github.com/alastairwyse/ApplicationMetrics) [metric logger](https://github.com/alastairwyse/ApplicationMetrics/blob/master/ApplicationMetrics/IMetricLogger.cs) which writes metrics and instrumentation information to a Microsoft SQL Server database.


1. Provide interfaces which can be injected into client classes, and provide simple methods for logging metrics from these classes.
2. To ensure that details of how the metrics are stored and displayed is hidden behind the interfaces
3. To provide a simple mechanism of viewing metrics through the Windows Performance Monitor (i.e. simpler than that provided by the .NET PerformanceCounter and CounterCreationData classes)\*.
4. To provide additional implementation of metric loggers and viewers for files, console, and relational databases, plus base classes to allow consumers to easily provide their own implementations of metric loggers and viewers†.

\* Note that the PerformanceCounterMetricLogger class which was used to view metrics through the Windows Performance Monitor, has been moved to a [separate project](https://github.com/alastairwyse/ApplicationMetrics.MetricLoggers.WindowsPerformanceCounter) since this project was migrated to .NET Standard.  
† Note that the [MicrosoftAccessMetricLogger](https://github.com/alastairwyse/ApplicationMetrics/blob/1.5.0.0/ApplicationMetrics/MicrosoftAccessMetricLogger.cs) and [MicrosoftAccessMetricLoggerImplementation](https://github.com/alastairwyse/ApplicationMetrics/blob/1.5.0.0/ApplicationMetrics/MicrosoftAccessMetricLoggerImplementation.cs) classes have been deprecated as of version 2.0.0, but still serve as an example of implementing a metric logger that writes to a relational database

#### Getting Started

##### 1) Defining Metrics
Metrics are defined, by deriving from the CountMetric, AmountMetric, StatusMetric, and IntervalMetric classes.  The difference between these metric types is outlined below...

<table>
  <tr>
    <td><b>Class</b></td>
    <td><b>Description</b></td>
  </tr>
  <tr>
    <td valign="top">CountMetric</td>
    <td>Used when you need to record the number of instances of an event and where the events increments by 1 each time (e.g. number of disk read operations, number of messages sent to a remote system, number of cache hits)</td>
  </tr>
  <tr>
    <td valign="top">AmountMetric</td>
    <td>Used to record events which have an associated size which accumulates, but not necessarily by 1 each time (e.g. the size in bytes of a message sent to a remote system)</td>
  </tr>
  <tr>
    <td valign="top">StatusMetric</td>
    <td>Used to record events which have an associated size which varies over time rather than accumulating (e.g. total amount of free memory).  The distinction from AmountMetrics is that summing the total recorded amounts over successive AmountMetric events has meaning (e.g. the total number of bytes sent to a remote system totalled across multiple sent messages), whereas summing the total recorded amounts over successive StatusMetrics would not (e.g. summed free memory across multiple sampling points).</td>
  </tr>
  <tr>
    <td valign="top">IntervalMetric</td>
    <td>Used to record the time taken for an event to complete (e.g. total time taken to send a message to a remote system).  This is calculated by capturing the start and end times of an IntervalMetric event.</td>
  </tr>
</table>

In this sample case ApplicationMetrics is used to capture instrumentation from a class which sends a message to a remote location.  We would define the following 3 metrics...

````C#
class MessageSent : CountMetric
{
    public MessageSent()
    {
        base.name = "MessageSent";
        base.description = "The number of messages sent";
    }
}

class MessageSize : AmountMetric
{
    public MessageSize()
    {
        base.name = "MessageSize";
        base.description = "The size of a sent message";
    }
}

class MessageSendTime : IntervalMetric
{
    public MessageSendTime()
    {
        base.name = "MessageSendTime";
        base.description = "The time taken to send a message";
    }
}
````

##### 2) Using the IMetricLogger interface
The IMetricLogger interface should be injected into the client class.  The example below shows our message sending class, with an instance of IMetricLogger used to log the above metrics when a message is sent.

````C#
public class MessageSender
{
    private IMetricLogger metricLogger;

    public MessageSender(IMetricLogger metricLogger)
    {
        this.metricLogger = metricLogger;
    }

    public void Send(String message)
    {
        metricLogger.Begin(new MessageSendTime());

        // Call private method to perform the send
        try
        {
            SendMessage(message);
        }
        catch (Exception e)
        {
            metricLogger.CancelBegin(new MessageSendTime());
            throw e;
        }

        metricLogger.End(new MessageSendTime());
        metricLogger.Increment(new MessageSent());
        metricLogger.Add(new MessageSize(), message.Length);
    }
````

The MessageSender class could be instantiated using a FileMetricLogger with the below statements...

````C#
FileMetricLogger metricLogger  = new FileMetricLogger('|', @"C:\Test\MessageSenderMetrics.log", new LoopingWorkerThreadBufferProcessor(1000), true);
MessageSender testMessageSender = new MessageSender(metricLogger);
````

##### 3) Using the IMetricAggregateLogger interface
Classes that implement IMetricAggregateLogger (ConsoleMetricLogger and PerformanceCounterMetricLogger) let you define and log aggregates of individual metrics.  The example client code below shows how to define some aggregates for the above metrics...

````C#
static void Main(string[] args)
{
    LoopingWorkerThreadBufferProcessor bufferProcessor = new LoopingWorkerThreadBufferProcessor(5000);
    ConsoleMetricLogger metricLogger = new ConsoleMetricLogger(bufferProcessor, true);

    // Define a metric aggregate to record the average size of sent messages (total message size / number of messages sent)
    metricLogger.DefineMetricAggregate(new MessageSize(), new MessageSent(), "AverageMessageSize", "The average size of sent messages");

    // Define a metric aggregate to record the number of messages sent per second (number of messages sent / number of seconds of runtime)
    metricLogger.DefineMetricAggregate(new MessageSent(), TimeUnit.Second, "MessagesSentPerSecond", "The number of messages sent per second");
}
````

##### 4) Viewing the metrics
When started, the ConsoleMetricLogger will produce output similar to the following...

```
---------------------------------------------------
-- Application metrics as of 2015-06-16 13:01:11 --
---------------------------------------------------
MessageSent: 207
MessageSize: 1223510
MessageSendTime: 12834
AverageMessageSize: 5910.676328502415
MessagesSentPerSecond: 2.41545893719806
```

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