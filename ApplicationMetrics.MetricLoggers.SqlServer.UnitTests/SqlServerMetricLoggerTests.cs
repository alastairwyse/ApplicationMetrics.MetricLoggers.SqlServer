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
using System.Linq;
using System.Globalization;
using System.Data;
using System.Threading;
using Microsoft.Data.SqlClient;
using NUnit.Framework;
using NUnit.Framework.Internal;
using NSubstitute;
using StandardAbstraction;
using ApplicationMetrics.MetricLoggers.SqlServer;

namespace ApplicationMetrics.MetricLoggers.SqlServer.UnitTests
{
    /// <summary>
    /// Unit tests for the ApplicationMetrics.MetricLoggers.SqlServer.SqlServerMetricLogger class.
    /// </summary>
    public class SqlServerMetricLoggerTests
    {
        private const String idColumnName = "Id";
        private const String metricNameColumnName = "MetricName";
        private const String metricDescriptionColumnName = "MetricDescription";
        private const String eventTimeColumnName = "EventTime";
        private const String amountColumnName = "Amount";
        private const String valueColumnName = "Value";
        private const String durationColumnName = "Duration";
        private const String insertCountMetricsStoredProcedureName = "InsertCountMetrics";
        private const String insertAmountMetricsStoredProcedureName = "InsertAmountMetrics";
        private const String insertStatusMetricsStoredProcedureName = "InsertStatusMetrics";
        private const String insertIntervalMetricsStoredProcedureName = "InsertIntervalMetrics";
        private const String categoryParameterName = "@Category";
        private const String countMetricsParameterName = "@CountMetrics";
        private const String amountMetricsParameterName = "@AmountMetrics";
        private const String statusMetricsParameterName = "@StatusMetrics";
        private const String intervalMetricsParameterName = "@IntervalMetrics";

        private string testCategory;
        private string testConnectionString;
        private IBufferProcessingStrategy mockBufferProcessingStrategy;
        private IDateTime mockDateTimeProvider;
        private IStopwatch mockStopwatch;
        private IGuidProvider mockGuidProvider;
        private IStoredProcedureExecutionWrapper mockStoredProcedureExecutionWrapper;
        private SqlServerMetricLoggerWithProtectedMembers testSqlServerMetricLogger;

        [SetUp]
        protected void SetUp()
        {
            testCategory = "TestCategory";
            testConnectionString = "Server=testServer; Database=testDB; User Id=userId; Password=password;";

            mockBufferProcessingStrategy = Substitute.For<IBufferProcessingStrategy>();
            mockDateTimeProvider = Substitute.For<IDateTime>();
            mockStopwatch = Substitute.For<IStopwatch>();
            mockGuidProvider = Substitute.For<IGuidProvider>();
            mockStoredProcedureExecutionWrapper = Substitute.For<IStoredProcedureExecutionWrapper>();
            testSqlServerMetricLogger = new SqlServerMetricLoggerWithProtectedMembers(testCategory, testConnectionString, 5, 10, mockBufferProcessingStrategy, true, mockDateTimeProvider, mockStopwatch, mockGuidProvider, mockStoredProcedureExecutionWrapper);
        }

        [TearDown]
        protected void TearDown()
        {
            testSqlServerMetricLogger.Dispose();
        }

        [Test]
        public void Constructor_CategoryStringParameterWhitespace()
        {
            var e = Assert.Throws<ArgumentException>(delegate
            {
                var testSqlServerMetricLogger = new SqlServerMetricLogger(" ", "Server=testServer; Database=testDB; User Id=userId; Password=password;", 5, 10, mockBufferProcessingStrategy, true);
            });

            Assert.That(e.Message, Does.StartWith($"Parameter 'category' must contain a value."));
            Assert.AreEqual("category", e.ParamName);
        }

        [Test]
        public void Constructor_ConnectionStringParameterWhitespace()
        {
            var e = Assert.Throws<ArgumentException>(delegate
            {
                var testSqlServerMetricLogger = new SqlServerMetricLogger("TestCategory", " ", 5, 10, mockBufferProcessingStrategy, true);
            });

            Assert.That(e.Message, Does.StartWith($"Parameter 'connectionString' must contain a value."));
            Assert.AreEqual("connectionString", e.ParamName);
        }

        [Test]
        public void Constructor_RetryCountParameterLessThan0()
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(delegate
            {
                var testSqlServerMetricLogger = new SqlServerMetricLogger("TestCategory", "Server=testServer; Database=testDB; User Id=userId; Password=password;", -1, 10, mockBufferProcessingStrategy, true);
            });

            Assert.That(e.Message, Does.StartWith($"Parameter 'retryCount' with value -1 cannot be less than 0."));
            Assert.AreEqual("retryCount", e.ParamName);
        }

        [Test]
        public void Constructor_RetryCountParameterGreaterThan59()
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(delegate
            {
                var testSqlServerMetricLogger = new SqlServerMetricLogger("TestCategory", "Server=testServer; Database=testDB; User Id=userId; Password=password;", 60, 10, mockBufferProcessingStrategy, true);
            });

            Assert.That(e.Message, Does.StartWith($"Parameter 'retryCount' with value 60 cannot be greater than 59."));
            Assert.AreEqual("retryCount", e.ParamName);
        }

        [Test]
        public void Constructor_RetryIntervalParameterLessThan0()
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(delegate
            {
                var testSqlServerMetricLogger = new SqlServerMetricLogger("TestCategory", "Server=testServer; Database=testDB; User Id=userId; Password=password;", 5, -1, mockBufferProcessingStrategy, true);
            });

            Assert.That(e.Message, Does.StartWith($"Parameter 'retryInterval' with value -1 cannot be less than 0."));
            Assert.AreEqual("retryInterval", e.ParamName);
        }

        [Test]
        public void Constructor_RetryIntervalParameterGreaterThan120()
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(delegate
            {
                var testSqlServerMetricLogger = new SqlServerMetricLogger("TestCategory", "Server=testServer; Database=testDB; User Id=userId; Password=password;", 5, 121, mockBufferProcessingStrategy, true);
            });

            Assert.That(e.Message, Does.StartWith($"Parameter 'retryInterval' with value 121 cannot be greater than 120."));
            Assert.AreEqual("retryInterval", e.ParamName);
        }

        [Test]
        public void ProcessCountMetricEvents_ExceptionExecutingStoredProcedure()
        {
            string mockExceptionMessage = "Mock SQL Server exception";
            mockStoredProcedureExecutionWrapper.When(wrapper => wrapper.Execute(insertCountMetricsStoredProcedureName, Arg.Any<IEnumerable<SqlParameter>>())).Do(callInfo => { throw new Exception(mockExceptionMessage); });

            // The first call will catch the exception on a worker thread, on the second call it will be re-thrown on the main thread
            SimulateDequeueAndProcessMetricEventsMethod(testSqlServerMetricLogger, null, null, null, null);
            var e = Assert.Throws<AggregateException>(delegate
            {
                SimulateDequeueAndProcessMetricEventsMethod(testSqlServerMetricLogger, null, null, null, null);
            });

            Assert.That(e.Message, Does.StartWith($"One or more exceptions occurred on worker threads whilst whilst writing metrics to SQL Server."));
            Assert.AreEqual(1, e.InnerExceptions.Count);
            var innerException = e.InnerExceptions[0];
            Assert.That(innerException.Message, Does.StartWith("An error occurred writing count metrics to SQL Server."));
            Assert.That(innerException.InnerException.Message, Does.StartWith(mockExceptionMessage));
        }

        [Test]
        public void ProcessAmountMetricEvents_ExceptionExecutingStoredProcedure()
        {
            string mockExceptionMessage = "Mock SQL Server exception";
            mockStoredProcedureExecutionWrapper.When(wrapper => wrapper.Execute(insertAmountMetricsStoredProcedureName, Arg.Any<IEnumerable<SqlParameter>>())).Do(callInfo => { throw new Exception(mockExceptionMessage); });

            // The first call will catch the exception on a worker thread, on the second call it will be re-thrown on the main thread
            SimulateDequeueAndProcessMetricEventsMethod(testSqlServerMetricLogger, null, null, null, null);
            var e = Assert.Throws<AggregateException>(delegate
            {
                SimulateDequeueAndProcessMetricEventsMethod(testSqlServerMetricLogger, null, null, null, null);
            });

            Assert.That(e.Message, Does.StartWith($"One or more exceptions occurred on worker threads whilst whilst writing metrics to SQL Server."));
            Assert.AreEqual(1, e.InnerExceptions.Count);
            var innerException = e.InnerExceptions[0];
            Assert.That(innerException.Message, Does.StartWith("An error occurred writing amount metrics to SQL Server."));
            Assert.That(innerException.InnerException.Message, Does.StartWith(mockExceptionMessage));
        }

        [Test]
        public void ProcessStatusMetricEvents_ExceptionExecutingStoredProcedure()
        {
            string mockExceptionMessage = "Mock SQL Server exception";
            mockStoredProcedureExecutionWrapper.When(wrapper => wrapper.Execute(insertStatusMetricsStoredProcedureName, Arg.Any<IEnumerable<SqlParameter>>())).Do(callInfo => { throw new Exception(mockExceptionMessage); });

            // The first call will catch the exception on a worker thread, on the second call it will be re-thrown on the main thread
            SimulateDequeueAndProcessMetricEventsMethod(testSqlServerMetricLogger, null, null, null, null);
            var e = Assert.Throws<AggregateException>(delegate
            {
                SimulateDequeueAndProcessMetricEventsMethod(testSqlServerMetricLogger, null, null, null, null);
            });

            Assert.That(e.Message, Does.StartWith($"One or more exceptions occurred on worker threads whilst whilst writing metrics to SQL Server."));
            Assert.AreEqual(1, e.InnerExceptions.Count);
            var innerException = e.InnerExceptions[0];
            Assert.That(innerException.Message, Does.StartWith("An error occurred writing status metrics to SQL Server."));
            Assert.That(innerException.InnerException.Message, Does.StartWith(mockExceptionMessage));
        }

        [Test]
        public void ProcessIntervalMetricEvents_ExceptionExecutingStoredProcedure()
        {
            string mockExceptionMessage = "Mock SQL Server exception";
            mockStoredProcedureExecutionWrapper.When(wrapper => wrapper.Execute(insertIntervalMetricsStoredProcedureName, Arg.Any<IEnumerable<SqlParameter>>())).Do(callInfo => { throw new Exception(mockExceptionMessage); });

            // The first call will catch the exception on a worker thread, on the second call it will be re-thrown on the main thread
            SimulateDequeueAndProcessMetricEventsMethod(testSqlServerMetricLogger, null, null, null, null);
            var e = Assert.Throws<AggregateException>(delegate
            {
                SimulateDequeueAndProcessMetricEventsMethod(testSqlServerMetricLogger, null, null, null, null);
            });

            Assert.That(e.Message, Does.StartWith($"One or more exceptions occurred on worker threads whilst whilst writing metrics to SQL Server."));
            Assert.AreEqual(1, e.InnerExceptions.Count);
            var innerException = e.InnerExceptions[0];
            Assert.That(innerException.Message, Does.StartWith("An error occurred writing interval metrics to SQL Server."));
            Assert.That(innerException.InnerException.Message, Does.StartWith(mockExceptionMessage));
        }

        [Test]
        public void DequeueAndProcessMetricEvents_ExceptionExecutingStoredProceduresOnAllProcessMethods()
        {
            // Tests that multiple exceptions occurring on worker threads are re-thrown as a cobined AggregateException 
            string mockExceptionMessage = "Mock SQL Server exception";
            mockStoredProcedureExecutionWrapper.When(wrapper => wrapper.Execute(insertCountMetricsStoredProcedureName, Arg.Any<IEnumerable<SqlParameter>>())).Do(callInfo => { throw new Exception(mockExceptionMessage); });
            mockStoredProcedureExecutionWrapper.When(wrapper => wrapper.Execute(insertAmountMetricsStoredProcedureName, Arg.Any<IEnumerable<SqlParameter>>())).Do(callInfo => { throw new Exception(mockExceptionMessage); });
            mockStoredProcedureExecutionWrapper.When(wrapper => wrapper.Execute(insertStatusMetricsStoredProcedureName, Arg.Any<IEnumerable<SqlParameter>>())).Do(callInfo => { throw new Exception(mockExceptionMessage); });
            mockStoredProcedureExecutionWrapper.When(wrapper => wrapper.Execute(insertIntervalMetricsStoredProcedureName, Arg.Any<IEnumerable<SqlParameter>>())).Do(callInfo => { throw new Exception(mockExceptionMessage); });

            // The first call will catch the exception on a worker thread, on the second call it will be re-thrown on the main thread
            SimulateDequeueAndProcessMetricEventsMethod(testSqlServerMetricLogger, null, null, null, null);
            var e = Assert.Throws<AggregateException>(delegate
            {
                SimulateDequeueAndProcessMetricEventsMethod(testSqlServerMetricLogger, null, null, null, null);
            });
            Assert.That(e.Message, Does.StartWith($"One or more exceptions occurred on worker threads whilst whilst writing metrics to SQL Server."));
            Assert.AreEqual(4, e.InnerExceptions.Count);
            var allInnerExceptions = new List<Exception>(e.InnerExceptions);
            allInnerExceptions.Sort(delegate (Exception x, Exception y)
            {
                return x.Message.CompareTo(y.Message);
            });
            Assert.That(allInnerExceptions[0].Message, Does.StartWith("An error occurred writing amount metrics to SQL Server."));
            Assert.That(allInnerExceptions[1].Message, Does.StartWith("An error occurred writing count metrics to SQL Server."));
            Assert.That(allInnerExceptions[2].Message, Does.StartWith("An error occurred writing interval metrics to SQL Server."));
            Assert.That(allInnerExceptions[3].Message, Does.StartWith("An error occurred writing status metrics to SQL Server."));
        }

        [Test]
        public void DequeueAndProcessMetricEvents()
        {
            // Variables to capture the table-valued parameters passed to each of the stored procedures
            List<SqlParameter> countMetricProcedureParameters = null;
            List<SqlParameter> amountMetricProcedureParameters = null;
            List<SqlParameter> statusMetricProcedureParameters = null;
            List<SqlParameter> intervalMetricProcedureParameters = null;

            List<Tuple<CountMetric, System.DateTime>> countMetricEventInstances;
            List<Tuple<AmountMetric, Int64, System.DateTime>> amountMetricEventInstances;
            List<Tuple<StatusMetric, Int64, System.DateTime>> statusMetricEventInstances;
            List<Tuple<IntervalMetric, Int64, System.DateTime>> intervalMetricEventInstances;
            GenerateDequeueAndProcessMetricEventsSuccessTestParameters(out countMetricEventInstances, out amountMetricEventInstances, out statusMetricEventInstances, out intervalMetricEventInstances);

            mockStoredProcedureExecutionWrapper.Execute(insertCountMetricsStoredProcedureName, Arg.Do<IEnumerable<SqlParameter>>(argVal => countMetricProcedureParameters = new List<SqlParameter>(argVal)));
            mockStoredProcedureExecutionWrapper.Execute(insertAmountMetricsStoredProcedureName, Arg.Do<IEnumerable<SqlParameter>>(argVal => amountMetricProcedureParameters = new List<SqlParameter>(argVal)));
            mockStoredProcedureExecutionWrapper.Execute(insertStatusMetricsStoredProcedureName, Arg.Do<IEnumerable<SqlParameter>>(argVal => statusMetricProcedureParameters = new List<SqlParameter>(argVal)));
            mockStoredProcedureExecutionWrapper.Execute(insertIntervalMetricsStoredProcedureName, Arg.Do<IEnumerable<SqlParameter>>(argVal => intervalMetricProcedureParameters = new List<SqlParameter>(argVal)));

            SimulateDequeueAndProcessMetricEventsMethod(testSqlServerMetricLogger, countMetricEventInstances, amountMetricEventInstances, statusMetricEventInstances, intervalMetricEventInstances);

            AssertDequeueAndProcessMetricEventsSuccessTestStoredProcedureParameters(countMetricProcedureParameters, amountMetricProcedureParameters, statusMetricProcedureParameters, intervalMetricProcedureParameters);
        }

        [Test]
        public void DequeueAndProcessMetricEvents_WorkerThreadsStartedInReverseOrder()
        {
            // Variables to capture the table-valued parameters passed to each of the stored procedures
            List<SqlParameter> countMetricProcedureParameters = null;
            List<SqlParameter> amountMetricProcedureParameters = null;
            List<SqlParameter> statusMetricProcedureParameters = null;
            List<SqlParameter> intervalMetricProcedureParameters = null;

            List<Tuple<CountMetric, System.DateTime>> countMetricEventInstances;
            List<Tuple<AmountMetric, Int64, System.DateTime>> amountMetricEventInstances;
            List<Tuple<StatusMetric, Int64, System.DateTime>> statusMetricEventInstances;
            List<Tuple<IntervalMetric, Int64, System.DateTime>> intervalMetricEventInstances;
            GenerateDequeueAndProcessMetricEventsSuccessTestParameters(out countMetricEventInstances, out amountMetricEventInstances, out statusMetricEventInstances, out intervalMetricEventInstances);

            mockStoredProcedureExecutionWrapper.Execute(insertCountMetricsStoredProcedureName, Arg.Do<IEnumerable<SqlParameter>>(argVal => countMetricProcedureParameters = new List<SqlParameter>(argVal)));
            mockStoredProcedureExecutionWrapper.Execute(insertAmountMetricsStoredProcedureName, Arg.Do<IEnumerable<SqlParameter>>(argVal => amountMetricProcedureParameters = new List<SqlParameter>(argVal)));
            mockStoredProcedureExecutionWrapper.Execute(insertStatusMetricsStoredProcedureName, Arg.Do<IEnumerable<SqlParameter>>(argVal => statusMetricProcedureParameters = new List<SqlParameter>(argVal)));
            mockStoredProcedureExecutionWrapper.Execute(insertIntervalMetricsStoredProcedureName, Arg.Do<IEnumerable<SqlParameter>>(argVal => intervalMetricProcedureParameters = new List<SqlParameter>(argVal)));

            // Call the 4 Process*MetricEvents() methods in semi-reverse order to ensure that thread signals still work properly when thread are started in an unexpected order
            //   We're simulating the indeterministic start order of the code within the Tasks created by each of the first 3 Process*MetricEvents() but changing the call order of the methods (although ofcourse in the real case the call order will not vary)
            //   Only caveat is that the 'last' method ProcessIntervalMetricEvents() runs on the main thread rather than creating a Task, and starts by waiting on a signal, so this method must always be called last
            testSqlServerMetricLogger.ProcessStatusMetricEvents(statusMetricEventInstances);
            // Wait to try and ensure the worker threads start in the order specified by the method calls
            Thread.Sleep(250);
            testSqlServerMetricLogger.ProcessAmountMetricEvents(amountMetricEventInstances);
            Thread.Sleep(250);
            testSqlServerMetricLogger.ProcessCountMetricEvents(countMetricEventInstances);
            Thread.Sleep(250);
            testSqlServerMetricLogger.ProcessIntervalMetricEvents(intervalMetricEventInstances);

            AssertDequeueAndProcessMetricEventsSuccessTestStoredProcedureParameters(countMetricProcedureParameters, amountMetricProcedureParameters, statusMetricProcedureParameters, intervalMetricProcedureParameters);
        }

        [Test]
        public void DequeueAndProcessMetricEvents_WorkerThreadsStartedInRandomOrders()
        {
            // Variables to capture the table-valued parameters passed to each of the stored procedures
            List<SqlParameter> countMetricProcedureParameters = null;
            List<SqlParameter> amountMetricProcedureParameters = null;
            List<SqlParameter> statusMetricProcedureParameters = null;
            List<SqlParameter> intervalMetricProcedureParameters = null;

            List<Tuple<CountMetric, System.DateTime>> countMetricEventInstances;
            List<Tuple<AmountMetric, Int64, System.DateTime>> amountMetricEventInstances;
            List<Tuple<StatusMetric, Int64, System.DateTime>> statusMetricEventInstances;
            List<Tuple<IntervalMetric, Int64, System.DateTime>> intervalMetricEventInstances;
            GenerateDequeueAndProcessMetricEventsSuccessTestParameters(out countMetricEventInstances, out amountMetricEventInstances, out statusMetricEventInstances, out intervalMetricEventInstances);

            mockStoredProcedureExecutionWrapper.Execute(insertCountMetricsStoredProcedureName, Arg.Do<IEnumerable<SqlParameter>>(argVal => countMetricProcedureParameters = new List<SqlParameter>(argVal)));
            mockStoredProcedureExecutionWrapper.Execute(insertAmountMetricsStoredProcedureName, Arg.Do<IEnumerable<SqlParameter>>(argVal => amountMetricProcedureParameters = new List<SqlParameter>(argVal)));
            mockStoredProcedureExecutionWrapper.Execute(insertStatusMetricsStoredProcedureName, Arg.Do<IEnumerable<SqlParameter>>(argVal => statusMetricProcedureParameters = new List<SqlParameter>(argVal)));
            mockStoredProcedureExecutionWrapper.Execute(insertIntervalMetricsStoredProcedureName, Arg.Do<IEnumerable<SqlParameter>>(argVal => intervalMetricProcedureParameters = new List<SqlParameter>(argVal)));

            // Call the 4 Process*MetricEvents() methods in random orders order to ensure that thread signals still work properly when thread are started in an unexpected order
            //   See comments/caveat in DequeueAndProcessMetricEvents_WorkerThreadsStartedInReverseOrder() test
            testSqlServerMetricLogger.ProcessCountMetricEvents(countMetricEventInstances);
            Thread.Sleep(250); 
            testSqlServerMetricLogger.ProcessStatusMetricEvents(statusMetricEventInstances);
            Thread.Sleep(250);
            testSqlServerMetricLogger.ProcessAmountMetricEvents(amountMetricEventInstances);
            Thread.Sleep(250);
            testSqlServerMetricLogger.ProcessIntervalMetricEvents(intervalMetricEventInstances);

            AssertDequeueAndProcessMetricEventsSuccessTestStoredProcedureParameters(countMetricProcedureParameters, amountMetricProcedureParameters, statusMetricProcedureParameters, intervalMetricProcedureParameters);


            testSqlServerMetricLogger.ProcessAmountMetricEvents(amountMetricEventInstances);
            Thread.Sleep(250);
            testSqlServerMetricLogger.ProcessCountMetricEvents(countMetricEventInstances);
            Thread.Sleep(250);
            testSqlServerMetricLogger.ProcessStatusMetricEvents(statusMetricEventInstances);
            Thread.Sleep(250);
            testSqlServerMetricLogger.ProcessIntervalMetricEvents(intervalMetricEventInstances);

            AssertDequeueAndProcessMetricEventsSuccessTestStoredProcedureParameters(countMetricProcedureParameters, amountMetricProcedureParameters, statusMetricProcedureParameters, intervalMetricProcedureParameters);


            testSqlServerMetricLogger.ProcessAmountMetricEvents(amountMetricEventInstances);
            Thread.Sleep(250);
            testSqlServerMetricLogger.ProcessStatusMetricEvents(statusMetricEventInstances);
            Thread.Sleep(250);
            testSqlServerMetricLogger.ProcessCountMetricEvents(countMetricEventInstances);
            Thread.Sleep(250);
            testSqlServerMetricLogger.ProcessIntervalMetricEvents(intervalMetricEventInstances);

            AssertDequeueAndProcessMetricEventsSuccessTestStoredProcedureParameters(countMetricProcedureParameters, amountMetricProcedureParameters, statusMetricProcedureParameters, intervalMetricProcedureParameters);


            testSqlServerMetricLogger.ProcessStatusMetricEvents(statusMetricEventInstances);
            Thread.Sleep(250);
            testSqlServerMetricLogger.ProcessCountMetricEvents(countMetricEventInstances);
            Thread.Sleep(250);
            testSqlServerMetricLogger.ProcessAmountMetricEvents(amountMetricEventInstances);
            Thread.Sleep(250);
            testSqlServerMetricLogger.ProcessIntervalMetricEvents(intervalMetricEventInstances);

            AssertDequeueAndProcessMetricEventsSuccessTestStoredProcedureParameters(countMetricProcedureParameters, amountMetricProcedureParameters, statusMetricProcedureParameters, intervalMetricProcedureParameters);
        }

        #region Private/Protected Methods

        /// <summary>
        /// Simulates calling the protected MetricLoggerBuffer.DequeueAndProcessMetricEvents() method.
        /// </summary>
        /// <param name="sqlServerMetricLoggerInstance">The SqlServerMetricLoggerWithProtectedMembers to simulate calling the method on.</param>
        /// <param name="countMetricEvents">Parameters representing a set of CountMetricEventInstance classes.</param>
        /// <param name="amountMetricEvents">Parameters representing a set of AmountMetricEventInstance classes.</param>
        /// <param name="statusMetricEvents">Parameters representing a set of StatusMetricEventInstance classes.</param>
        /// <param name="intervalMetricEvents">Parameters representing a set of IntervalMetricEventInstance classes.</param>
        private void SimulateDequeueAndProcessMetricEventsMethod
        (
            SqlServerMetricLoggerWithProtectedMembers sqlServerMetricLoggerInstance, 
            IEnumerable<Tuple<CountMetric, System.DateTime>> countMetricEvents, 
            IEnumerable<Tuple<AmountMetric, Int64, System.DateTime>> amountMetricEvents,
            IEnumerable<Tuple<StatusMetric, Int64, System.DateTime>> statusMetricEvents,
            IEnumerable<Tuple<IntervalMetric, Int64, System.DateTime>> intervalMetricEvents
        )
        {
            if (countMetricEvents == null)
            {
                sqlServerMetricLoggerInstance.ProcessCountMetricEvents(Enumerable.Empty<Tuple<CountMetric, System.DateTime>>());
            }
            else
            {
                sqlServerMetricLoggerInstance.ProcessCountMetricEvents(countMetricEvents);
            }
            if (amountMetricEvents == null)
            {
                sqlServerMetricLoggerInstance.ProcessAmountMetricEvents(Enumerable.Empty<Tuple<AmountMetric, Int64, System.DateTime>>());
            }
            else
            {
                sqlServerMetricLoggerInstance.ProcessAmountMetricEvents(amountMetricEvents);
            }
            if (statusMetricEvents == null)
            {
                sqlServerMetricLoggerInstance.ProcessStatusMetricEvents(Enumerable.Empty<Tuple<StatusMetric, Int64, System.DateTime>>());
            }
            else
            {
                sqlServerMetricLoggerInstance.ProcessStatusMetricEvents(statusMetricEvents);
            }
            if (intervalMetricEvents == null)
            {
                sqlServerMetricLoggerInstance.ProcessIntervalMetricEvents(Enumerable.Empty<Tuple<IntervalMetric, Int64, System.DateTime>>());
            }
            else
            {
                sqlServerMetricLoggerInstance.ProcessIntervalMetricEvents(intervalMetricEvents);
            }
        }

        /// <summary>
        /// Generates as UTC <see cref="System.DateTime"/> from the specified string containing a date in ISO format.
        /// </summary>
        /// <param name="isoFormattedDateString">The date string.</param>
        /// <returns>the DateTime.</returns>
        private System.DateTime GenerateUtcDateTime(String isoFormattedDateString)
        {
            var returnDateTime = System.DateTime.ParseExact(isoFormattedDateString, "yyyy-MM-dd HH:mm:ss.fff", DateTimeFormatInfo.InvariantInfo);
            return System.DateTime.SpecifyKind(returnDateTime, DateTimeKind.Utc);
        }

        /// <summary>
        /// Sets up parameters for testing the DequeueAndProcessMetricEvents() method (via method SimulateDequeueAndProcessMetricEventsMethod() in this test class).
        /// </summary>
        private void GenerateDequeueAndProcessMetricEventsSuccessTestParameters
        (
            out List<Tuple<CountMetric, System.DateTime>> countMetricEventInstances, 
            out List<Tuple<AmountMetric, Int64, System.DateTime>> amountMetricEventInstances, 
            out List<Tuple<StatusMetric, Int64, System.DateTime>> statusMetricEventInstances, 
            out List<Tuple<IntervalMetric, Int64, System.DateTime>> intervalMetricEventInstances
        )
        {
            countMetricEventInstances = new List<Tuple<CountMetric, System.DateTime>>()
            {
                new Tuple<CountMetric, System.DateTime>(new DiskReadOperation(), GenerateUtcDateTime("2022-08-30 21:58:00.001")),
                new Tuple<CountMetric, System.DateTime>(new DiskReadOperation(), GenerateUtcDateTime("2022-08-30 21:58:00.002")),
                new Tuple<CountMetric, System.DateTime>(new MessageReceived(), GenerateUtcDateTime("2022-08-30 21:58:00.003")),
                new Tuple<CountMetric, System.DateTime>(new MessageReceived(), GenerateUtcDateTime("2022-08-30 21:58:00.004"))
            };
            amountMetricEventInstances = new List<Tuple<AmountMetric, Int64, System.DateTime>>()
            {
                 new Tuple<AmountMetric, Int64, System.DateTime>(new DiskBytesRead(), 1, GenerateUtcDateTime("2022-08-30 21:58:00.005")),
                 new Tuple<AmountMetric, Int64, System.DateTime>(new MessageSize(), 2, GenerateUtcDateTime("2022-08-30 21:58:00.006")),
                 new Tuple<AmountMetric, Int64, System.DateTime>(new DiskBytesRead(), 3, GenerateUtcDateTime("2022-08-30 21:58:00.007")),
                 new Tuple<AmountMetric, Int64, System.DateTime>(new MessageSize(), 4, GenerateUtcDateTime("2022-08-30 21:58:00.008"))
            };
            statusMetricEventInstances = new List<Tuple<StatusMetric, Int64, System.DateTime>>()
            {
                 new Tuple<StatusMetric, Int64, System.DateTime>(new AvailableMemory(), 5, GenerateUtcDateTime("2022-08-30 21:58:00.009")),
                 new Tuple<StatusMetric, Int64, System.DateTime>(new AvailableMemory(), 6, GenerateUtcDateTime("2022-08-30 21:58:00.010")),
                 new Tuple<StatusMetric, Int64, System.DateTime>(new ActiveWorkerThreads(), 7, GenerateUtcDateTime("2022-08-30 21:58:00.011")),
                 new Tuple<StatusMetric, Int64, System.DateTime>(new ActiveWorkerThreads(), 8, GenerateUtcDateTime("2022-08-30 21:58:00.012"))
            };
            intervalMetricEventInstances = new List<Tuple<IntervalMetric, Int64, System.DateTime>>()
            {
                 new Tuple<IntervalMetric, Int64, System.DateTime>(new DiskReadTime(), 9, GenerateUtcDateTime("2022-08-30 21:58:00.013")),
                 new Tuple<IntervalMetric, Int64, System.DateTime>(new MessageReceiveTime(), 10, GenerateUtcDateTime("2022-08-30 21:58:00.014")),
                 new Tuple<IntervalMetric, Int64, System.DateTime>(new DiskReadTime(), 11, GenerateUtcDateTime("2022-08-30 21:58:00.015")),
                 new Tuple<IntervalMetric, Int64, System.DateTime>(new MessageReceiveTime(), 12, GenerateUtcDateTime("2022-08-30 21:58:00.016")),
            };
        }

        /// <summary>
        /// Asserts correctness of collections of <see cref="SqlParameter"/> objects passed to stored procedures (and intercepted via mocks) as part of the DequeueAndProcessMetricEvents() method (via method SimulateDequeueAndProcessMetricEventsMethod() in this test class).
        /// </summary>
        private void AssertDequeueAndProcessMetricEventsSuccessTestStoredProcedureParameters
        (
            List<SqlParameter> countMetricProcedureParameters,
            List<SqlParameter> amountMetricProcedureParameters,
            List<SqlParameter> statusMetricProcedureParameters,
            List<SqlParameter> intervalMetricProcedureParameters
        )
        {
            // Check parameters to 'insert count metrics' stored procedure
            Assert.AreEqual(2, countMetricProcedureParameters.Count);
            Assert.AreEqual(categoryParameterName, countMetricProcedureParameters[0].ParameterName);
            Assert.AreEqual(SqlDbType.NVarChar, countMetricProcedureParameters[0].SqlDbType);
            Assert.AreEqual(testCategory, countMetricProcedureParameters[0].Value);
            Assert.AreEqual(countMetricsParameterName, countMetricProcedureParameters[1].ParameterName);
            Assert.AreEqual(SqlDbType.Structured, countMetricProcedureParameters[1].SqlDbType);
            DataTable countMetricsTableParameter = (DataTable)countMetricProcedureParameters[1].Value;
            Assert.AreEqual(4, countMetricsTableParameter.Columns.Count);
            Assert.AreEqual(idColumnName, countMetricsTableParameter.Columns[0].ColumnName);
            Assert.AreEqual(metricNameColumnName, countMetricsTableParameter.Columns[1].ColumnName);
            Assert.AreEqual(metricDescriptionColumnName, countMetricsTableParameter.Columns[2].ColumnName);
            Assert.AreEqual(eventTimeColumnName, countMetricsTableParameter.Columns[3].ColumnName);
            Assert.AreEqual(4, countMetricsTableParameter.Rows.Count);
            Assert.AreEqual(0, countMetricsTableParameter.Rows[0].Field<Int64>(idColumnName));
            Assert.AreEqual(new DiskReadOperation().Name, countMetricsTableParameter.Rows[0].Field<String>(metricNameColumnName));
            Assert.AreEqual(new DiskReadOperation().Description, countMetricsTableParameter.Rows[0].Field<String>(metricDescriptionColumnName));
            Assert.AreEqual(GenerateUtcDateTime("2022-08-30 21:58:00.001"), countMetricsTableParameter.Rows[0].Field<System.DateTime>(eventTimeColumnName));
            Assert.AreEqual(1, countMetricsTableParameter.Rows[1].Field<Int64>(idColumnName));
            Assert.AreEqual(new DiskReadOperation().Name, countMetricsTableParameter.Rows[1].Field<String>(metricNameColumnName));
            Assert.AreEqual("", countMetricsTableParameter.Rows[1].Field<String>(metricDescriptionColumnName));
            Assert.AreEqual(GenerateUtcDateTime("2022-08-30 21:58:00.002"), countMetricsTableParameter.Rows[1].Field<System.DateTime>(eventTimeColumnName));
            Assert.AreEqual(2, countMetricsTableParameter.Rows[2].Field<Int64>(idColumnName));
            Assert.AreEqual(new MessageReceived().Name, countMetricsTableParameter.Rows[2].Field<String>(metricNameColumnName));
            Assert.AreEqual(new MessageReceived().Description, countMetricsTableParameter.Rows[2].Field<String>(metricDescriptionColumnName));
            Assert.AreEqual(GenerateUtcDateTime("2022-08-30 21:58:00.003"), countMetricsTableParameter.Rows[2].Field<System.DateTime>(eventTimeColumnName));
            Assert.AreEqual(3, countMetricsTableParameter.Rows[3].Field<Int64>(idColumnName));
            Assert.AreEqual(new MessageReceived().Name, countMetricsTableParameter.Rows[3].Field<String>(metricNameColumnName));
            Assert.AreEqual("", countMetricsTableParameter.Rows[3].Field<String>(metricDescriptionColumnName));
            Assert.AreEqual(GenerateUtcDateTime("2022-08-30 21:58:00.004"), countMetricsTableParameter.Rows[3].Field<System.DateTime>(eventTimeColumnName));

            // Check parameters to 'insert amount metrics' stored procedure
            Assert.AreEqual(2, amountMetricProcedureParameters.Count);
            Assert.AreEqual(categoryParameterName, amountMetricProcedureParameters[0].ParameterName);
            Assert.AreEqual(SqlDbType.NVarChar, amountMetricProcedureParameters[0].SqlDbType);
            Assert.AreEqual(testCategory, amountMetricProcedureParameters[0].Value);
            Assert.AreEqual(amountMetricsParameterName, amountMetricProcedureParameters[1].ParameterName);
            Assert.AreEqual(SqlDbType.Structured, amountMetricProcedureParameters[1].SqlDbType);
            DataTable amountMetricsTableParameter = (DataTable)amountMetricProcedureParameters[1].Value;
            Assert.AreEqual(5, amountMetricsTableParameter.Columns.Count);
            Assert.AreEqual(idColumnName, amountMetricsTableParameter.Columns[0].ColumnName);
            Assert.AreEqual(metricNameColumnName, amountMetricsTableParameter.Columns[1].ColumnName);
            Assert.AreEqual(metricDescriptionColumnName, amountMetricsTableParameter.Columns[2].ColumnName);
            Assert.AreEqual(eventTimeColumnName, amountMetricsTableParameter.Columns[3].ColumnName);
            Assert.AreEqual(amountColumnName, amountMetricsTableParameter.Columns[4].ColumnName);
            Assert.AreEqual(4, amountMetricsTableParameter.Rows.Count);
            Assert.AreEqual(0, amountMetricsTableParameter.Rows[0].Field<Int64>(idColumnName));
            Assert.AreEqual(new DiskBytesRead().Name, amountMetricsTableParameter.Rows[0].Field<String>(metricNameColumnName));
            Assert.AreEqual(new DiskBytesRead().Description, amountMetricsTableParameter.Rows[0].Field<String>(metricDescriptionColumnName));
            Assert.AreEqual(GenerateUtcDateTime("2022-08-30 21:58:00.005"), amountMetricsTableParameter.Rows[0].Field<System.DateTime>(eventTimeColumnName));
            Assert.AreEqual(1, amountMetricsTableParameter.Rows[0].Field<Int64>(amountColumnName));
            Assert.AreEqual(1, amountMetricsTableParameter.Rows[1].Field<Int64>(idColumnName));
            Assert.AreEqual(new MessageSize().Name, amountMetricsTableParameter.Rows[1].Field<String>(metricNameColumnName));
            Assert.AreEqual(new MessageSize().Description, amountMetricsTableParameter.Rows[1].Field<String>(metricDescriptionColumnName));
            Assert.AreEqual(GenerateUtcDateTime("2022-08-30 21:58:00.006"), amountMetricsTableParameter.Rows[1].Field<System.DateTime>(eventTimeColumnName));
            Assert.AreEqual(2, amountMetricsTableParameter.Rows[1].Field<Int64>(amountColumnName));
            Assert.AreEqual(2, amountMetricsTableParameter.Rows[2].Field<Int64>(idColumnName));
            Assert.AreEqual(new DiskBytesRead().Name, amountMetricsTableParameter.Rows[2].Field<String>(metricNameColumnName));
            Assert.AreEqual("", amountMetricsTableParameter.Rows[2].Field<String>(metricDescriptionColumnName));
            Assert.AreEqual(GenerateUtcDateTime("2022-08-30 21:58:00.007"), amountMetricsTableParameter.Rows[2].Field<System.DateTime>(eventTimeColumnName));
            Assert.AreEqual(3, amountMetricsTableParameter.Rows[2].Field<Int64>(amountColumnName));
            Assert.AreEqual(3, amountMetricsTableParameter.Rows[3].Field<Int64>(idColumnName));
            Assert.AreEqual(new MessageSize().Name, amountMetricsTableParameter.Rows[3].Field<String>(metricNameColumnName));
            Assert.AreEqual("", amountMetricsTableParameter.Rows[3].Field<String>(metricDescriptionColumnName));
            Assert.AreEqual(GenerateUtcDateTime("2022-08-30 21:58:00.008"), amountMetricsTableParameter.Rows[3].Field<System.DateTime>(eventTimeColumnName));
            Assert.AreEqual(4, amountMetricsTableParameter.Rows[3].Field<Int64>(amountColumnName));

            // Check parameters to 'insert status metrics' stored procedure
            Assert.AreEqual(2, statusMetricProcedureParameters.Count);
            Assert.AreEqual(categoryParameterName, statusMetricProcedureParameters[0].ParameterName);
            Assert.AreEqual(SqlDbType.NVarChar, statusMetricProcedureParameters[0].SqlDbType);
            Assert.AreEqual(testCategory, statusMetricProcedureParameters[0].Value);
            Assert.AreEqual(statusMetricsParameterName, statusMetricProcedureParameters[1].ParameterName);
            Assert.AreEqual(SqlDbType.Structured, statusMetricProcedureParameters[1].SqlDbType);
            DataTable statusMetricsTableParameter = (DataTable)statusMetricProcedureParameters[1].Value;
            Assert.AreEqual(5, statusMetricsTableParameter.Columns.Count);
            Assert.AreEqual(idColumnName, statusMetricsTableParameter.Columns[0].ColumnName);
            Assert.AreEqual(metricNameColumnName, statusMetricsTableParameter.Columns[1].ColumnName);
            Assert.AreEqual(metricDescriptionColumnName, statusMetricsTableParameter.Columns[2].ColumnName);
            Assert.AreEqual(eventTimeColumnName, statusMetricsTableParameter.Columns[3].ColumnName);
            Assert.AreEqual(valueColumnName, statusMetricsTableParameter.Columns[4].ColumnName);
            Assert.AreEqual(4, statusMetricsTableParameter.Rows.Count);
            Assert.AreEqual(0, statusMetricsTableParameter.Rows[0].Field<Int64>(idColumnName));
            Assert.AreEqual(new AvailableMemory().Name, statusMetricsTableParameter.Rows[0].Field<String>(metricNameColumnName));
            Assert.AreEqual(new AvailableMemory().Description, statusMetricsTableParameter.Rows[0].Field<String>(metricDescriptionColumnName));
            Assert.AreEqual(GenerateUtcDateTime("2022-08-30 21:58:00.009"), statusMetricsTableParameter.Rows[0].Field<System.DateTime>(eventTimeColumnName));
            Assert.AreEqual(5, statusMetricsTableParameter.Rows[0].Field<Int64>(valueColumnName));
            Assert.AreEqual(1, statusMetricsTableParameter.Rows[1].Field<Int64>(idColumnName));
            Assert.AreEqual(new AvailableMemory().Name, statusMetricsTableParameter.Rows[1].Field<String>(metricNameColumnName));
            Assert.AreEqual("", statusMetricsTableParameter.Rows[1].Field<String>(metricDescriptionColumnName));
            Assert.AreEqual(GenerateUtcDateTime("2022-08-30 21:58:00.010"), statusMetricsTableParameter.Rows[1].Field<System.DateTime>(eventTimeColumnName));
            Assert.AreEqual(6, statusMetricsTableParameter.Rows[1].Field<Int64>(valueColumnName));
            Assert.AreEqual(2, statusMetricsTableParameter.Rows[2].Field<Int64>(idColumnName));
            Assert.AreEqual(new ActiveWorkerThreads().Name, statusMetricsTableParameter.Rows[2].Field<String>(metricNameColumnName));
            Assert.AreEqual(new ActiveWorkerThreads().Description, statusMetricsTableParameter.Rows[2].Field<String>(metricDescriptionColumnName));
            Assert.AreEqual(GenerateUtcDateTime("2022-08-30 21:58:00.011"), statusMetricsTableParameter.Rows[2].Field<System.DateTime>(eventTimeColumnName));
            Assert.AreEqual(7, statusMetricsTableParameter.Rows[2].Field<Int64>(valueColumnName));
            Assert.AreEqual(3, statusMetricsTableParameter.Rows[3].Field<Int64>(idColumnName));
            Assert.AreEqual(new ActiveWorkerThreads().Name, statusMetricsTableParameter.Rows[3].Field<String>(metricNameColumnName));
            Assert.AreEqual("", statusMetricsTableParameter.Rows[3].Field<String>(metricDescriptionColumnName));
            Assert.AreEqual(GenerateUtcDateTime("2022-08-30 21:58:00.012"), statusMetricsTableParameter.Rows[3].Field<System.DateTime>(eventTimeColumnName));
            Assert.AreEqual(8, statusMetricsTableParameter.Rows[3].Field<Int64>(valueColumnName));

            // Check parameters to 'insert interval metrics' stored procedure
            Assert.AreEqual(2, intervalMetricProcedureParameters.Count);
            Assert.AreEqual(categoryParameterName, intervalMetricProcedureParameters[0].ParameterName);
            Assert.AreEqual(SqlDbType.NVarChar, intervalMetricProcedureParameters[0].SqlDbType);
            Assert.AreEqual(testCategory, intervalMetricProcedureParameters[0].Value);
            Assert.AreEqual(intervalMetricsParameterName, intervalMetricProcedureParameters[1].ParameterName);
            Assert.AreEqual(SqlDbType.Structured, intervalMetricProcedureParameters[1].SqlDbType);
            DataTable intervalMetricsTableParameter = (DataTable)intervalMetricProcedureParameters[1].Value;
            Assert.AreEqual(5, intervalMetricsTableParameter.Columns.Count);
            Assert.AreEqual(idColumnName, intervalMetricsTableParameter.Columns[0].ColumnName);
            Assert.AreEqual(metricNameColumnName, intervalMetricsTableParameter.Columns[1].ColumnName);
            Assert.AreEqual(metricDescriptionColumnName, intervalMetricsTableParameter.Columns[2].ColumnName);
            Assert.AreEqual(eventTimeColumnName, intervalMetricsTableParameter.Columns[3].ColumnName);
            Assert.AreEqual(durationColumnName, intervalMetricsTableParameter.Columns[4].ColumnName);
            Assert.AreEqual(4, intervalMetricsTableParameter.Rows.Count);
            Assert.AreEqual(0, intervalMetricsTableParameter.Rows[0].Field<Int64>(idColumnName));
            Assert.AreEqual(new DiskReadTime().Name, intervalMetricsTableParameter.Rows[0].Field<String>(metricNameColumnName));
            Assert.AreEqual(new DiskReadTime().Description, intervalMetricsTableParameter.Rows[0].Field<String>(metricDescriptionColumnName));
            Assert.AreEqual(GenerateUtcDateTime("2022-08-30 21:58:00.013"), intervalMetricsTableParameter.Rows[0].Field<System.DateTime>(eventTimeColumnName));
            Assert.AreEqual(9, intervalMetricsTableParameter.Rows[0].Field<Int64>(durationColumnName));
            Assert.AreEqual(1, intervalMetricsTableParameter.Rows[1].Field<Int64>(idColumnName));
            Assert.AreEqual(new MessageReceiveTime().Name, intervalMetricsTableParameter.Rows[1].Field<String>(metricNameColumnName));
            Assert.AreEqual(new MessageReceiveTime().Description, intervalMetricsTableParameter.Rows[1].Field<String>(metricDescriptionColumnName));
            Assert.AreEqual(GenerateUtcDateTime("2022-08-30 21:58:00.014"), intervalMetricsTableParameter.Rows[1].Field<System.DateTime>(eventTimeColumnName));
            Assert.AreEqual(10, intervalMetricsTableParameter.Rows[1].Field<Int64>(durationColumnName));
            Assert.AreEqual(2, intervalMetricsTableParameter.Rows[2].Field<Int64>(idColumnName));
            Assert.AreEqual(new DiskReadTime().Name, intervalMetricsTableParameter.Rows[2].Field<String>(metricNameColumnName));
            Assert.AreEqual("", intervalMetricsTableParameter.Rows[2].Field<String>(metricDescriptionColumnName));
            Assert.AreEqual(GenerateUtcDateTime("2022-08-30 21:58:00.015"), intervalMetricsTableParameter.Rows[2].Field<System.DateTime>(eventTimeColumnName));
            Assert.AreEqual(11, intervalMetricsTableParameter.Rows[2].Field<Int64>(durationColumnName));
            Assert.AreEqual(3, intervalMetricsTableParameter.Rows[3].Field<Int64>(idColumnName));
            Assert.AreEqual(new MessageReceiveTime().Name, intervalMetricsTableParameter.Rows[3].Field<String>(metricNameColumnName));
            Assert.AreEqual("", intervalMetricsTableParameter.Rows[3].Field<String>(metricDescriptionColumnName));
            Assert.AreEqual(GenerateUtcDateTime("2022-08-30 21:58:00.016"), intervalMetricsTableParameter.Rows[3].Field<System.DateTime>(eventTimeColumnName));
            Assert.AreEqual(12, intervalMetricsTableParameter.Rows[3].Field<Int64>(durationColumnName));
        }

        #endregion

        #region Nested Classes

        /// <summary>
        /// Version of the SqlServerMetricLogger class where private and protected methods are exposed as public so that they can be unit tested.
        /// </summary>
        private class SqlServerMetricLoggerWithProtectedMembers : SqlServerMetricLogger
        {
            /// <summary>
            /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.SqlServer.UnitTests.SqlServerMetricLoggerTests+SqlServerMetricLoggerWithProtectedMembers class.
            /// </summary>
            /// <param name="category">The category to log all metrics under.</param>
            /// <param name="connectionString">The string to use to connect to the SQL Server database.</param>
            /// <param name="retryCount">The number of times an operation against the SQL Server database should be retried in the case of execution failure.</param>
            /// <param name="retryInterval">The time in seconds between operation retries.</param>
            /// <param name="bufferProcessingStrategy">Object which implements a processing strategy for the buffers (queues).</param>
            /// <param name="intervalMetricChecking">Specifies whether an exception should be thrown if the correct order of interval metric logging is not followed (e.g. End() method called before Begin()).</param>
            /// <param name="dateTime">A test (mock) <see cref="System.DateTime"/> object.</param>
            /// <param name="stopWatch">A test (mock) <see cref="Stopwatch"/> object.</param>
            /// <param name="guidProvider">A test (mock) <see cref="IGuidProvider"/> object.</param>
            /// <param name="storedProcedureExecutor">A test (mock) <see cref="IStoredProcedureExecutionWrapper"/> object.</param>
            public SqlServerMetricLoggerWithProtectedMembers(String category, String connectionString, Int32 retryCount, Int32 retryInterval, IBufferProcessingStrategy bufferProcessingStrategy, bool intervalMetricChecking, IDateTime dateTime, IStopwatch stopWatch, IGuidProvider guidProvider, IStoredProcedureExecutionWrapper storedProcedureExecutor)
                : base(category, connectionString, retryCount, retryInterval, bufferProcessingStrategy, intervalMetricChecking, dateTime, stopWatch, guidProvider, storedProcedureExecutor)
            {
            }

            public void ProcessCountMetricEvents(IEnumerable<Tuple<CountMetric, System.DateTime>> countMetricEvents)
            {
                var countMetricEventsQueue = new Queue<CountMetricEventInstance>();
                foreach (Tuple<CountMetric, System.DateTime> currentCountMetricEvent in countMetricEvents)
                {
                    countMetricEventsQueue.Enqueue(new CountMetricEventInstance(currentCountMetricEvent.Item1, currentCountMetricEvent.Item2));
                }
                ProcessCountMetricEvents(countMetricEventsQueue);
            }

            public void ProcessAmountMetricEvents(IEnumerable<Tuple<AmountMetric, Int64, System.DateTime>> amountMetricEvents)
            {
                var amountMetricEventsQueue = new Queue<AmountMetricEventInstance>();
                foreach (Tuple<AmountMetric, Int64, System.DateTime> currentAmountMetricEvent in amountMetricEvents)
                {
                    amountMetricEventsQueue.Enqueue(new AmountMetricEventInstance(currentAmountMetricEvent.Item1, currentAmountMetricEvent.Item2, currentAmountMetricEvent.Item3));
                }
                ProcessAmountMetricEvents(amountMetricEventsQueue);
            }

            public void ProcessStatusMetricEvents(IEnumerable<Tuple<StatusMetric, Int64, System.DateTime>> statusMetricEvents)
            {
                var statusMetricEventsQueue = new Queue<StatusMetricEventInstance>();
                foreach (Tuple<StatusMetric, Int64, System.DateTime> currentStatusMetricEvent in statusMetricEvents)
                {
                    statusMetricEventsQueue.Enqueue(new StatusMetricEventInstance(currentStatusMetricEvent.Item1, currentStatusMetricEvent.Item2, currentStatusMetricEvent.Item3));
                }
                ProcessStatusMetricEvents(statusMetricEventsQueue);
            }

            public void ProcessIntervalMetricEvents(IEnumerable<Tuple<IntervalMetric, Int64, System.DateTime>> intervalMetricEvents)
            {
                var intervalMetricEventsQueue = new Queue<Tuple<IntervalMetricEventInstance, Int64>>();
                foreach (Tuple<IntervalMetric, Int64, System.DateTime> currentIntervalMetricEvent in intervalMetricEvents)
                {
                    intervalMetricEventsQueue.Enqueue(new Tuple<IntervalMetricEventInstance, Int64>(new IntervalMetricEventInstance(currentIntervalMetricEvent.Item1, IntervalMetricEventTimePoint.Start, currentIntervalMetricEvent.Item3), currentIntervalMetricEvent.Item2));
                }
                ProcessIntervalMetricEvents(intervalMetricEventsQueue);
            }
        }

        #endregion
    }
}
