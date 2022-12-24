/*
 * Copyright 2022 Alastair Wyse (https://github.com/alastairwyse/ApplicationMetrics.MetricLoggers.SqlServer/)
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using StandardAbstraction;
using ApplicationLogging;

namespace ApplicationMetrics.MetricLoggers.SqlServer
{
    /// <summary>
    /// Writes metric events to a Microsoft SQL Server database.
    /// </summary>
    public class SqlServerMetricLogger : MetricLoggerBuffer, IDisposable
    {
        // In the base MetricLoggerBuffer class, methods Process*MetricEvents() are called synchronously in sequence.
        // However since in this implementation the different metric types are written to different tables in SQL Server, we can improve performance by executing the work
        //   of each of these methods in parallel in worker threads.
        // Hence Tasks and thread signalling classes are used in this class to execute the work of the 4 methods in parallel, but not return control from the final
        //   ProcessIntervalMetricEvents() method until all parallel work is completed.

        #pragma warning disable 1591

        protected const String idColumnName = "Id"; 
        protected const String metricNameColumnName = "MetricName";
        protected const String metricDescriptionColumnName = "MetricDescription";
        protected const String eventTimeColumnName = "EventTime";
        protected const String amountColumnName = "Amount";
        protected const String valueColumnName = "Value";
        protected const String durationColumnName = "Duration";
        protected const String insertCountMetricsStoredProcedureName = "InsertCountMetrics";
        protected const String insertAmountMetricsStoredProcedureName = "InsertAmountMetrics";
        protected const String insertStatusMetricsStoredProcedureName = "InsertStatusMetrics";
        protected const String insertIntervalMetricsStoredProcedureName = "InsertIntervalMetrics";
        protected const String categoryParameterName = "@Category";
        protected const String countMetricsParameterName = "@CountMetrics";
        protected const String amountMetricsParameterName = "@AmountMetrics";
        protected const String statusMetricsParameterName = "@StatusMetrics";
        protected const String intervalMetricsParameterName = "@IntervalMetrics";

        #pragma warning restore 1591

        /// <summary>The category to log all metrics under.</summary>
        protected String category;
        /// <summary>The string to use to connect to the SQL Server database.</summary>
        protected String connectionString;
        /// <summary>The number of times an operation against the SQL Server database should be retried in the case of execution failure.</summary>
        protected Int32 retryCount;
        /// <summary>The time in seconds between operation retries.</summary>
        protected Int32 retryInterval;
        /// <summary>The retry logic to use when connecting to and executing against the SQL Server database.</summary>
        protected SqlRetryLogicOption sqlRetryLogicOption;
        /// <summary>A set of SQL Server database engine error numbers which denote a transient fault.</summary>
        /// <see href="https://docs.microsoft.com/en-us/sql/relational-databases/errors-events/database-engine-events-and-errors?view=sql-server-ver16"/>
        /// <see href="https://docs.microsoft.com/en-us/azure/azure-sql/database/troubleshoot-common-errors-issues?view=azuresql"/>
        protected List<Int32> sqlServerTransientErrorNumbers;
        /// <summary>Signal that is set by the designated first Process*MetricEvents() method is called.  Allows that method to properly initialize object state before the other methods call SQL server.</summary>
        protected ManualResetEvent parallelProcessStartSignal;
        /// <summary>Signal that is waited on by the designated last Process*MetricEvents() method before returning.  Allows that method to ensure that all the other methods have been called and completed before control is returned to the base class DequeueAndProcessMetricEvents() method.</summary>
        protected CountdownEvent parallelProcessCompletedSignal;
        /// <summary>Holds any exceptions which are thrown on worker threads.</summary>
        protected ConcurrentQueue<Exception> workerThreadExceptions;
        /// <summary>Whether an exception occurred on one of the threads sending events to the the SQL Server database.</summary>
        protected volatile Int32 processedEventCount;
        /// <summary>Holds the time the calls to the Process*MetricEvents() methods started.</summary>
        protected System.DateTime processingStartTime;
        /// <summary>The logger to use for performance statistics.</summary>
        protected IApplicationLogger logger;
        /// <summary>Wraps calls to execute stored procedures so that they can be mocked in unit tests.</summary>
        protected IStoredProcedureExecutionWrapper storedProcedureExecutor;

        /// <summary>
        /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.SqlServer.SqlServerMetricLogger class.
        /// </summary>
        /// <param name="category">The category to log all metrics under.</param>
        /// <param name="connectionString">The string to use to connect to the SQL Server database.</param>
        /// <param name="retryCount">The number of times an operation against the SQL Server database should be retried in the case of execution failure.</param>
        /// <param name="retryInterval">The time in seconds between operation retries.</param>
        /// <param name="bufferProcessingStrategy">Object which implements a processing strategy for the buffers (queues).</param>
        /// <param name="intervalMetricChecking">Specifies whether an exception should be thrown if the correct order of interval metric logging is not followed (e.g. End() method called before Begin()).  Note that this parameter only has an effect when running in 'non-interleaved' mode.</param>
        public SqlServerMetricLogger(String category, String connectionString, Int32 retryCount, Int32 retryInterval, IBufferProcessingStrategy bufferProcessingStrategy, bool intervalMetricChecking)
            : base(bufferProcessingStrategy, intervalMetricChecking)
        {
            ValidateAndInitialiseConstructorParameters(category, connectionString, retryCount, retryInterval);
        }

        /// <summary>
        /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.SqlServer.SqlServerMetricLogger class.
        /// </summary>
        /// <param name="category">The category to log all metrics under.</param>
        /// <param name="connectionString">The string to use to connect to the SQL Server database.</param>
        /// <param name="retryCount">The number of times an operation against the SQL Server database should be retried in the case of execution failure.</param>
        /// <param name="retryInterval">The time in seconds between operation retries.</param>
        /// <param name="bufferProcessingStrategy">Object which implements a processing strategy for the buffers (queues).</param>
        /// <param name="intervalMetricChecking">Specifies whether an exception should be thrown if the correct order of interval metric logging is not followed (e.g. End() method called before Begin()).  Note that this parameter only has an effect when running in 'non-interleaved' mode.</param>
        /// <param name="logger">The logger to use for performance statistics.</param>
        public SqlServerMetricLogger
        (
            String category, 
            String connectionString, 
            Int32 retryCount, 
            Int32 retryInterval, 
            IBufferProcessingStrategy bufferProcessingStrategy, 
            bool intervalMetricChecking, 
            IApplicationLogger logger
        )
            : base(bufferProcessingStrategy, intervalMetricChecking)
        {
            ValidateAndInitialiseConstructorParameters(category, connectionString, retryCount, retryInterval);
            this.logger = logger;
        }

        /// <summary>
        /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.SqlServer.SqlServerMetricLogger class.
        /// </summary>
        /// <param name="category">The category to log all metrics under.</param>
        /// <param name="connectionString">The string to use to connect to the SQL Server database.</param>
        /// <param name="retryCount">The number of times an operation against the SQL Server database should be retried in the case of execution failure.</param>
        /// <param name="retryInterval">The time in seconds between operation retries.</param>
        /// <param name="bufferProcessingStrategy">Object which implements a processing strategy for the buffers (queues).</param>
        /// <param name="intervalMetricChecking">Specifies whether an exception should be thrown if the correct order of interval metric logging is not followed (e.g. End() method called before Begin()).  Note that this parameter only has an effect when running in 'non-interleaved' mode.</param>
        /// <param name="logger">The logger to use for performance statistics.</param>
        /// <param name="dateTime">A test (mock) <see cref="System.DateTime"/> object.</param>
        /// <param name="stopWatch">A test (mock) <see cref="Stopwatch"/> object.</param>
        /// <param name="guidProvider">A test (mock) <see cref="IGuidProvider"/> object.</param>
        /// <param name="storedProcedureExecutor">A test (mock) <see cref="IStoredProcedureExecutionWrapper"/> object.</param>
        /// <remarks>This constructor is included to facilitate unit testing.</remarks>
        public SqlServerMetricLogger
        (
            String category, 
            String connectionString, 
            Int32 retryCount, 
            Int32 retryInterval, 
            IBufferProcessingStrategy bufferProcessingStrategy, 
            bool intervalMetricChecking, 
            IApplicationLogger logger, 
            IDateTime dateTime, 
            IStopwatch stopWatch, 
            IGuidProvider guidProvider, 
            IStoredProcedureExecutionWrapper storedProcedureExecutor
        )
            : base(bufferProcessingStrategy, intervalMetricChecking, dateTime, stopWatch, guidProvider)
        {
            ValidateAndInitialiseConstructorParameters(category, connectionString, retryCount, retryInterval);
            this.storedProcedureExecutor = storedProcedureExecutor;
            this.logger = logger;
        }

        #region Base Class Abstract Method Implementations

        /// <summary>
        /// Writes logged count metric events to SQL Server.
        /// </summary>
        /// <param name="countMetricEvents">The count metric events to process.</param>
        protected override void ProcessCountMetricEvents(Queue<CountMetricEventInstance> countMetricEvents)
        {
            if (workerThreadExceptions.Count > 0)
            {
                throw new AggregateException("One or more exceptions occurred on worker threads whilst writing metrics to SQL Server.", workerThreadExceptions);
            }
            parallelProcessCompletedSignal.Reset();
            processedEventCount = 0;
            processingStartTime = GetStopWatchUtcNow();
            // Allow the other parallel calls to SQL Server on worker threads to start
            parallelProcessStartSignal.Set();

            Task.Run(() =>
            {
                try
                {
                    CreateAndPopulateStagingTableAndCallStoredProcedure<CountMetricEventInstance, CountMetric>
                    (
                        countMetricEvents, 
                        null, 
                        (actionRow, actionMetricEventInstance) => { }, 
                        insertCountMetricsStoredProcedureName, 
                        countMetricsParameterName
                    );
                    Interlocked.Add(ref processedEventCount, countMetricEvents.Count);
                }
                catch (Exception e)
                {
                    var outerException = new Exception("An error occurred writing count metrics to SQL Server.", e);
                    workerThreadExceptions.Enqueue(outerException);
                    throw e;
                }
                finally
                {
                    parallelProcessCompletedSignal.Signal();
                }
            });
        }

        /// <summary>
        /// Writes logged amount metric events to SQL Server.
        /// </summary>
        /// <param name="amountMetricEvents">The amount metric events to process.</param>
        protected override void ProcessAmountMetricEvents(Queue<AmountMetricEventInstance> amountMetricEvents)
        {
            Task.Run(() =>
            {
                // Wait for the 'first' Process*MetricEvents() to reset the complete signal
                parallelProcessStartSignal.WaitOne();

                try
                {
                    using (var amountColumn = new DataColumn(amountColumnName, typeof(Int64)))
                    {
                        Action<DataRow, AmountMetricEventInstance> addToDataRowAction = (row, metricEventInstance) =>
                        {
                            row[amountColumnName] = metricEventInstance.Amount;
                        };
                        CreateAndPopulateStagingTableAndCallStoredProcedure<AmountMetricEventInstance, AmountMetric>
                        (
                            amountMetricEvents, 
                            amountColumn, 
                            addToDataRowAction, 
                            insertAmountMetricsStoredProcedureName, 
                            amountMetricsParameterName
                        );
                        Interlocked.Add(ref processedEventCount, amountMetricEvents.Count);
                    }
                }
                catch (Exception e)
                {
                    var outerException = new Exception("An error occurred writing amount metrics to SQL Server.", e);
                    workerThreadExceptions.Enqueue(outerException);
                    throw e;
                }
                finally
                {
                    parallelProcessCompletedSignal.Signal();
                }
            });
        }

        /// <summary>
        /// Writes logged status metric events to SQL Server.
        /// </summary>
        /// <param name="statusMetricEvents">The status metric events to process.</param>
        protected override void ProcessStatusMetricEvents(Queue<StatusMetricEventInstance> statusMetricEvents)
        {
            Task.Run(() =>
            {
                // Wait for the 'first' Process*MetricEvents() to reset the complete signal
                parallelProcessStartSignal.WaitOne();

                try
                {
                    using (var valueColumn = new DataColumn(valueColumnName, typeof(Int64)))
                    {
                        Action<DataRow, StatusMetricEventInstance> addToDataRowAction = (row, metricEventInstance) =>
                        {
                            row[valueColumnName] = metricEventInstance.Value;
                        };
                        CreateAndPopulateStagingTableAndCallStoredProcedure<StatusMetricEventInstance, StatusMetric>
                        (
                            statusMetricEvents, 
                            valueColumn,
                            addToDataRowAction, 
                            insertStatusMetricsStoredProcedureName, 
                            statusMetricsParameterName
                        );
                        Interlocked.Add(ref processedEventCount, statusMetricEvents.Count);
                    }
                }
                catch (Exception e)
                {
                    var outerException = new Exception("An error occurred writing status metrics to SQL Server.", e);
                    workerThreadExceptions.Enqueue(outerException);
                    throw e;
                }
                finally
                {
                    parallelProcessCompletedSignal.Signal();
                }
            });
        }

        /// <summary>
        /// Writes logged interval metric events to SQL Server.
        /// </summary>
        /// <param name="intervalMetricEventsAndDurations">The interval metric events and corresponding durations of the events (in milliseconds) to process.</param>
        protected override void ProcessIntervalMetricEvents(Queue<Tuple<IntervalMetricEventInstance, Int64>> intervalMetricEventsAndDurations)
        {
            // Wait for the 'first' Process*MetricEvents() to reset the complete signal
            parallelProcessStartSignal.WaitOne();
            // Since the whole flush process surrounding this class runs on a dedicated worker thread this 'last' Process*MetricEvents() method can run on that worker thread (i.e. the current thread)
            try
            {
                // Unfortunately we can't reuse the CreateAndPopulateStagingTableAndCallStoredProcedure() method here, as the contents of the queue are a Tuple, not a subclass of MetricEventInstance
                using (var stagingTable = new DataTable())
                using (var idColumn = new DataColumn(idColumnName, typeof(Int64)))
                using (var metricNameColumn = new DataColumn(metricNameColumnName, typeof(String)))
                using (var metricDescriptionColumn = new DataColumn(metricDescriptionColumnName, typeof(String)))
                using (var eventTimeColumn = new DataColumn(eventTimeColumnName, typeof(System.DateTime))) 
                using (var durationColumn = new DataColumn(durationColumnName, typeof(Int64)))
                {
                    // Build the staging data table for the metrics
                    stagingTable.Columns.Add(idColumn);
                    stagingTable.Columns.Add(metricNameColumn);
                    stagingTable.Columns.Add(metricDescriptionColumn);
                    stagingTable.Columns.Add(eventTimeColumn);
                    stagingTable.Columns.Add(durationColumn);

                    // Store the types of metrics we've already encountered, so that we only include the description for a metric on its first instance (matches logic in the stored procedure which is called)
                    var encounteredMetricTypes = new HashSet<Type>();
                    Int64 idValue = 0;
                    foreach (Tuple<IntervalMetricEventInstance, Int64> currentMetricEventInstanceAndDuration in intervalMetricEventsAndDurations)
                    {
                        var row = stagingTable.NewRow();
                        row[idColumnName] = idValue;
                        row[metricNameColumnName] = currentMetricEventInstanceAndDuration.Item1.Metric.Name;
                        if (encounteredMetricTypes.Contains(currentMetricEventInstanceAndDuration.Item1.MetricType) == false)
                        {
                            row[metricDescriptionColumnName] = currentMetricEventInstanceAndDuration.Item1.Metric.Description;
                            encounteredMetricTypes.Add(currentMetricEventInstanceAndDuration.Item1.MetricType);
                        }
                        else
                        {
                            row[metricDescriptionColumnName] = "";
                        }
                        row[eventTimeColumnName] = currentMetricEventInstanceAndDuration.Item1.EventTime;
                        row[durationColumnName] = currentMetricEventInstanceAndDuration.Item2;
                        stagingTable.Rows.Add(row);
                        idValue++;
                    }

                    // Call the stored procedure
                    var parameters = new List<SqlParameter>()
                    {
                        CreateSqlParameterWithValue(categoryParameterName, SqlDbType.NVarChar, category),
                        CreateSqlParameterWithValue(intervalMetricsParameterName, SqlDbType.Structured, stagingTable),
                    };
                    storedProcedureExecutor.Execute(insertIntervalMetricsStoredProcedureName, parameters);
                    Interlocked.Add(ref processedEventCount, intervalMetricEventsAndDurations.Count);
                }
            }
            catch (Exception e)
            {
                var outerException = new Exception("An error occurred writing interval metrics to SQL Server.", e);
                workerThreadExceptions.Enqueue(outerException);
                // Since this 'last' Process*MetricEvents() method runs on the main thread, don't throw the exception here, as it would hide/mask any exceptions which occurred on the worker threads running the other Process*MetricEvents() methods
            }
            finally
            {
                parallelProcessCompletedSignal.Wait();
                Int32 processingTime = Convert.ToInt32(Math.Round((base.GetStopWatchUtcNow() - processingStartTime).TotalMilliseconds));
                logger.Log(this, LogLevel.Information, $"Processed {processedEventCount} metric events in {processingTime} milliseconds.");
                parallelProcessStartSignal.Reset();
            }
        }

        #endregion

        #region Private/Protected Methods

        /// <summary>
        /// Returns a list of SQL Server error numbers which indicate errors which are transient (i.e. could be recovered from after retry).
        /// </summary>
        /// <returns>The list of SQL Server error numbers.</returns>
        /// <remarks>See <see href="https://docs.microsoft.com/en-us/azure/azure-sql/database/troubleshoot-common-errors-issues?view=azuresql">Troubleshooting connectivity issues and other errors with Azure SQL Database and Azure SQL Managed Instance</see></remarks> 
        protected List<Int32> GenerateSqlServerTransientErrorNumbers()
        {
            // Below obtained from https://docs.microsoft.com/en-us/azure/azure-sql/database/troubleshoot-common-errors-issues?view=azuresql
            var returnList = new List<Int32>() { 26, 40, 615, 926, 4060, 4221, 10053, 10928, 10929, 11001, 40197, 40501, 40613, 40615, 40544, 40549, 49918, 49919, 49920 };
            // These are additional error numbers encountered during testing
            returnList.AddRange(new List<Int32>() { -2, 53, 121 });

            return returnList;
        }

        /// <summary>
        /// Common method to validate and set/initialise parameters passed to the constructor.
        /// </summary>
        /// <param name="category">The category to log all metrics under.</param>
        /// <param name="connectionString">The string to use to connect to the SQL Server database.</param>
        /// <param name="retryCount">The number of times an operation against the SQL Server database should be retried in the case of execution failure.</param>
        /// <param name="retryInterval">The time in seconds between operation retries.</param>
        protected void ValidateAndInitialiseConstructorParameters(String category, String connectionString, Int32 retryCount, Int32 retryInterval)
        {
            if (String.IsNullOrWhiteSpace(category) == true)
                throw new ArgumentException($"Parameter '{nameof(category)}' must contain a value.", nameof(category));
            if (String.IsNullOrWhiteSpace(connectionString) == true)
                throw new ArgumentException($"Parameter '{nameof(connectionString)}' must contain a value.", nameof(connectionString));
            if (retryCount < 0)
                throw new ArgumentOutOfRangeException(nameof(retryCount), $"Parameter '{nameof(retryCount)}' with value {retryCount} cannot be less than 0.");
            if (retryCount > 59)
                throw new ArgumentOutOfRangeException(nameof(retryCount), $"Parameter '{nameof(retryCount)}' with value {retryCount} cannot be greater than 59.");
            if (retryInterval < 0)
                throw new ArgumentOutOfRangeException(nameof(retryInterval), $"Parameter '{nameof(retryInterval)}' with value {retryInterval} cannot be less than 0.");
            if (retryInterval > 120)
                throw new ArgumentOutOfRangeException(nameof(retryInterval), $"Parameter '{nameof(retryInterval)}' with value {retryInterval} cannot be greater than 120.");

            this.category = category;
            this.connectionString = connectionString;
            this.retryCount = retryCount;
            this.retryInterval = retryInterval;
            parallelProcessStartSignal = new ManualResetEvent(false);
            parallelProcessCompletedSignal = new CountdownEvent(3);
            workerThreadExceptions = new ConcurrentQueue<Exception>();
            storedProcedureExecutor = new StoredProcedureExecutionWrapper((String procedureName, IEnumerable<SqlParameter> parameters) => { ExecuteStoredProcedure(procedureName, parameters); });            
            // Setup retry logic
            sqlServerTransientErrorNumbers = GenerateSqlServerTransientErrorNumbers();
            sqlRetryLogicOption = new SqlRetryLogicOption();
            sqlRetryLogicOption.NumberOfTries = retryCount + 1;  // According to documentation... "1 means to execute one time and if an error is encountered, don't retry"
            sqlRetryLogicOption.MinTimeInterval = TimeSpan.FromSeconds(0);
            sqlRetryLogicOption.MaxTimeInterval = TimeSpan.FromSeconds(120);
            sqlRetryLogicOption.DeltaTime = TimeSpan.FromSeconds(retryInterval);
            sqlRetryLogicOption.TransientErrors = sqlServerTransientErrorNumbers;
            logger = new NullLogger();
        }

        /// <summary>
        /// Populates the specified staging data table with metric events, before calling the specified stored procedure and passing the staging table as a parameter.
        /// </summary>
        /// <typeparam name="TMetricEventInstance">The type of metric event instances being added to the staging table and passed to the stored procedure.</typeparam>
        /// <typeparam name="TMetricEvent">The type of metrics represented by the event instances.</typeparam>
        /// <param name="metricEventInstances">The metric event instances to add to the staging table.</param>
        /// <param name="metricValueColumn">A column definition for holding the 'value' of each subclass of <see cref="MetricBase"/>, e.g. <see cref="AmountMetric"/> 'Amount' or <see cref="StatusMetric"/> 'Value' properties.  Should be set null if the subclass of <see cref="MetricBase"/> does not contain a 'value' column (e.g. in the case of <see cref="CountMetric"/>).</param>
        /// <param name="addToDataRowAction">An action which adds additional fields to a row of the staging table.  Accepts two parameters: The row to add the fields to, and the metric event instance being added to the row.</param>
        /// <param name="storedProcedureName">The name of the stored procedure to call.</param>
        /// <param name="stagingTableParameterName">The name of the stored procedure parameter holding the staging table.</param>
        /// <remarks>Parameter 'addToDataRowAction' is included to allowing adding properties that are specific to each subclass of <see cref="MetricBase"/>, e.g. <see cref="AmountMetric"/> 'Amount' or <see cref="StatusMetric"/> 'Value' properties.</remarks>
        protected void CreateAndPopulateStagingTableAndCallStoredProcedure<TMetricEventInstance, TMetricEvent>(Queue<TMetricEventInstance> metricEventInstances, DataColumn metricValueColumn, Action<DataRow, TMetricEventInstance> addToDataRowAction, String storedProcedureName, String stagingTableParameterName) where TMetricEventInstance : MetricEventInstance<TMetricEvent> where TMetricEvent : MetricBase
        {
            using (var stagingTable = new DataTable())
            using (var idColumn = new DataColumn(idColumnName, typeof(Int64)))
            using (var metricNameColumn = new DataColumn(metricNameColumnName, typeof(String)))
            using (var metricDescriptionColumn = new DataColumn(metricDescriptionColumnName, typeof(String)))
            using (var eventTimeColumn = new DataColumn(eventTimeColumnName, typeof(System.DateTime)))
            {
                // Build the staging data table for the metrics
                stagingTable.Columns.Add(idColumn);
                stagingTable.Columns.Add(metricNameColumn);
                stagingTable.Columns.Add(metricDescriptionColumn);
                stagingTable.Columns.Add(eventTimeColumn);
                if (metricValueColumn != null)
                {
                    stagingTable.Columns.Add(metricValueColumn);
                }

                PopulateStagingTableAndCallStoredProcedure<TMetricEventInstance, TMetricEvent>(stagingTable, metricEventInstances, addToDataRowAction, storedProcedureName, stagingTableParameterName);
            }
        }

        /// <summary>
        /// Populates the specified staging data table with metric events, before calling the specified stored procedure and passing the staging table as a parameter.
        /// </summary>
        /// <typeparam name="TMetricEventInstance">The type of metric event instances being added to the staging table and passed to the stored procedure.</typeparam>
        /// <typeparam name="TMetricEvent">The type of metrics represented by the event instances.</typeparam>
        /// <param name="stagingTable">The staging table to pass to the stored procedure.</param>
        /// <param name="metricEventInstances">The metric event instances to add to the staging table.</param>
        /// <param name="addToDataRowAction">An action which adds additional fields to a row of the staging table.  Accepts two parameters: The row to add the fields to, and the metric event instance being added to the row.</param>
        /// <param name="storedProcedureName">The name of the stored procedure to call.</param>
        /// <param name="stagingTableParameterName">The name of the stored procedure parameter holding the staging table.</param>
        /// <remarks>Parameter 'addToDataRowAction' is included to allowing adding properties that are specific to each subclass of <see cref="MetricBase"/>, e.g. <see cref="AmountMetric"/> 'Amount' or <see cref="StatusMetric"/> 'Value' properties.</remarks>
        protected void PopulateStagingTableAndCallStoredProcedure<TMetricEventInstance, TMetricEvent>(DataTable stagingTable, Queue<TMetricEventInstance> metricEventInstances, Action<DataRow, TMetricEventInstance> addToDataRowAction, String storedProcedureName, String stagingTableParameterName) where TMetricEventInstance : MetricEventInstance<TMetricEvent> where TMetricEvent : MetricBase
        {
            // Store the types of metrics we've already encountered, so that we only include the description for a metric on its first instance (matches logic in the stored procedure which is called)
            var encounteredMetricTypes = new HashSet<Type>();
            Int64 idValue = 0;
            foreach (TMetricEventInstance currentMetricEventInstance in metricEventInstances)
            {
                var row = stagingTable.NewRow();
                row[idColumnName] = idValue;
                row[metricNameColumnName] = currentMetricEventInstance.Metric.Name;
                if (encounteredMetricTypes.Contains(currentMetricEventInstance.MetricType) == false)
                {
                    row[metricDescriptionColumnName] = currentMetricEventInstance.Metric.Description;
                    encounteredMetricTypes.Add(currentMetricEventInstance.MetricType);
                }
                else
                {
                    row[metricDescriptionColumnName] = "";
                }
                row[eventTimeColumnName] = currentMetricEventInstance.EventTime;
                addToDataRowAction.Invoke(row, currentMetricEventInstance);
                stagingTable.Rows.Add(row);
                idValue++;
            }

            // Call the stored procedure
            var parameters = new List<SqlParameter>()
            {
                CreateSqlParameterWithValue(categoryParameterName, SqlDbType.NVarChar, category),
                CreateSqlParameterWithValue(stagingTableParameterName, SqlDbType.Structured, stagingTable),
            };
            storedProcedureExecutor.Execute(storedProcedureName, parameters);
        }

        /// <summary>
        /// Attempts to execute a stored procedure which does not return a result set.
        /// </summary>
        /// <param name="procedureName">The name of the stored procedure.</param>
        /// <param name="parameters">The parameters to pass to the stored procedure.</param>
        protected void ExecuteStoredProcedure(String procedureName, IEnumerable<SqlParameter> parameters)
        {
            try
            {
                using (var connection = new SqlConnection(connectionString))
                using (var command = new SqlCommand(procedureName))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    foreach (SqlParameter currentParameter in parameters)
                    {
                        command.Parameters.Add(currentParameter);
                    }
                    connection.RetryLogicProvider = SqlConfigurableRetryFactory.CreateFixedRetryProvider(sqlRetryLogicOption);
                    connection.Open();
                    command.Connection = connection;
                    command.CommandTimeout = retryInterval * retryCount;
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Failed to execute stored procedure '{procedureName}' in SQL Server.", e);
            }
        }

        /// <summary>
        /// Creates a <see cref="SqlParameter" />.
        /// </summary>
        /// <param name="parameterName">The name of the parameter.</param>
        /// <param name="parameterType">The type of the parameter.</param>
        /// <param name="parameterValue">The value of the parameter.</param>
        /// <returns>The created parameter.</returns>
        protected SqlParameter CreateSqlParameterWithValue(String parameterName, SqlDbType parameterType, Object parameterValue)
        {
            var returnParameter = new SqlParameter(parameterName, parameterType);
            returnParameter.Value = parameterValue;

            return returnParameter;
        }

        #endregion

        #region Finalize / Dispose Methods

        /// <summary>
        /// Provides a method to free unmanaged resources used by this class.
        /// </summary>
        /// <param name="disposing">Whether the method is being called as part of an explicit Dispose routine, and hence whether managed resources should also be freed.</param>
        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                try
                {
                    if (disposing)
                    {
                        // Free other state (managed objects).
                        if (parallelProcessStartSignal != null)
                        {
                            parallelProcessStartSignal.Dispose();
                        }
                        if (parallelProcessCompletedSignal != null)
                        {
                            parallelProcessCompletedSignal.Dispose();
                        }
                    }
                    // Free your own state (unmanaged objects).

                    // Set large fields to null.
                }
                finally
                {
                    base.Dispose(disposing);
                }
            }
        }

        #endregion

        #region Inner Classes

        /// <summary>
        /// Implementation of IStoredProcedureExecutionWrapper which allows executing stored procedures through a configurable <see cref="Action"/>.
        /// </summary>
        protected class StoredProcedureExecutionWrapper : IStoredProcedureExecutionWrapper
        {
            /// <summary>An action which executed the stored procedure.</summary>
            protected Action<String, IEnumerable<SqlParameter>> executeAction;

            /// <summary>
            /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.SqlServer.SqlServerMetricLogger+StoredProcedureExecutionWrapper class.
            /// </summary>
            /// <param name="executeAction"></param>
            public StoredProcedureExecutionWrapper(Action<String, IEnumerable<SqlParameter>> executeAction)
            {
                this.executeAction = executeAction;
            }

            /// <summary>
            /// Executes a stored procedure which does not return a result set.
            /// </summary>
            /// <param name="procedureName">The name of the stored procedure.</param>
            /// <param name="parameters">The parameters to pass to the stored procedure.</param>
            public void Execute(String procedureName, IEnumerable<SqlParameter> parameters)
            {
                executeAction.Invoke(procedureName, parameters);
            }
        }

        /// <summary>
        /// Implementation of <see cref="IMetricLogger"/> which does not log.
        /// </summary>
        class NullLogger : IApplicationLogger
        {
            public void Log(LogLevel level, string text)
            {
            }

            public void Log(object source, LogLevel level, string text)
            {
            }

            public void Log(int eventIdentifier, LogLevel level, string text)
            {
            }

            public void Log(object source, int eventIdentifier, LogLevel level, string text)
            {
            }

            public void Log(LogLevel level, string text, Exception sourceException)
            {
            }

            public void Log(object source, LogLevel level, string text, Exception sourceException)
            {
            }

            public void Log(int eventIdentifier, LogLevel level, string text, Exception sourceException)
            {
            }

            public void Log(object source, int eventIdentifier, LogLevel level, string text, Exception sourceException)
            {
            }
        }

        #endregion
    }
}
