-------------------
-- Count Metrics --
-------------------

DECLARE @TestTVP AS CountMetricEventTableType;

INSERT 
INTO    @TestTVP
		( 
			MetricName, 
			MetricDescription, 
			EventTime    
		)
VALUES  (
			'Metric1Name', 
			'Metric1Description', 
			GETDATE()
		);

INSERT 
INTO    @TestTVP
		( 
			MetricName, 
			MetricDescription, 
			EventTime   
		)
VALUES  (
			'Metric1Name', 
			null, 
			GETDATE()
		);

INSERT 
INTO    @TestTVP
		( 
			MetricName, 
			MetricDescription, 
			EventTime 
		)
VALUES  (
			'Metric2Name', 
			'Metric2Description', 
			GETDATE()
		);

INSERT 
INTO    @TestTVP
		( 
			MetricName, 
			MetricDescription, 
			EventTime  
		)
VALUES  (
			'Metric2Name', 
			null, 
			GETDATE()
		);

INSERT 
INTO    @TestTVP
		( 
			MetricName, 
			MetricDescription, 
			EventTime 
		)
VALUES  (
			'Metric2Name', 
			null, 
			GETDATE()
		);

EXEC InsertCountMetrics 'TestCategory' , @TestTVP;

--SELECT  *
--FROM    @TestTVP;

SELECT  *
FROM    CountMetricInstances

SELECT  *
FROM	Categories

SELECT  *
FROM    CountMetrics

DELETE  
FROM    CountMetricInstances;

DELETE  
FROM    CountMetrics;

DELETE  
FROM    Categories;


--------------------
-- Amount Metrics --
--------------------

DECLARE @TestTVP AS AmountMetricEventTableType;

INSERT 
INTO    @TestTVP
		( 
			MetricName, 
			MetricDescription, 
			EventTime, 
			Amount     
		)
VALUES  (
			'Metric1Name', 
			'Metric1Description', 
			GETDATE(), 
			101
		);

INSERT 
INTO    @TestTVP
		( 
			MetricName, 
			MetricDescription, 
			EventTime, 
			Amount     
		)
VALUES  (
			'Metric1Name', 
			null, 
			GETDATE(), 
			102
		);

INSERT 
INTO    @TestTVP
		( 
			MetricName, 
			MetricDescription, 
			EventTime, 
			Amount     
		)
VALUES  (
			' ', --'Metric2Name', 
			'Metric2Description', 
			GETDATE(), 
			201
		);

INSERT 
INTO    @TestTVP
		( 
			MetricName, 
			MetricDescription, 
			EventTime, 
			Amount     
		)
VALUES  (
			'Metric2Name', 
			null, 
			GETDATE(), 
			202
		);

INSERT 
INTO    @TestTVP
		( 
			MetricName, 
			MetricDescription, 
			EventTime, 
			Amount     
		)
VALUES  (
			'Metric2Name', 
			null, 
			GETDATE(), 
			203
		);

EXEC InsertAmountMetrics 'TestCategory' , @TestTVP;

--SELECT  *
--FROM    @TestTVP;

SELECT  *
FROM    AmountMetricInstances

SELECT  *
FROM	Categories

SELECT  *
FROM    AmountMetrics

DELETE  
FROM    AmountMetricInstances;

DELETE  
FROM    AmountMetrics;

DELETE  
FROM    Categories;


--------------------
-- Status Metrics --
--------------------

DECLARE @TestTVP AS StatusMetricEventTableType;

INSERT 
INTO    @TestTVP
		( 
			MetricName, 
			MetricDescription, 
			EventTime, 
			Value     
		)
VALUES  (
			'Metric1Name', 
			'Metric1Description', 
			GETDATE(), 
			101
		);

INSERT 
INTO    @TestTVP
		( 
			MetricName, 
			MetricDescription, 
			EventTime, 
			Value     
		)
VALUES  (
			'Metric1Name', 
			null, 
			GETDATE(), 
			102
		);

INSERT 
INTO    @TestTVP
		( 
			MetricName, 
			MetricDescription, 
			EventTime, 
			Value     
		)
VALUES  (
			'Metric2Name', 
			'Metric2Description', 
			GETDATE(), 
			201
		);

INSERT 
INTO    @TestTVP
		( 
			MetricName, 
			MetricDescription, 
			EventTime, 
			Value     
		)
VALUES  (
			'Metric2Name', 
			null, 
			GETDATE(), 
			202
		);

INSERT 
INTO    @TestTVP
		( 
			MetricName, 
			MetricDescription, 
			EventTime, 
			Value     
		)
VALUES  (
			'Metric2Name', 
			null, 
			GETDATE(), 
			203
		);

EXEC InsertStatusMetrics 'TestCategory' , @TestTVP;

--SELECT  *
--FROM    @TestTVP;

SELECT  *
FROM    StatusMetricInstances

SELECT  *
FROM	Categories

SELECT  *
FROM    StatusMetrics

DELETE  
FROM    StatusMetricInstances;

DELETE  
FROM    StatusMetrics;

DELETE  
FROM    Categories;


----------------------
-- Interval Metrics --
----------------------

DECLARE @TestTVP AS IntervalMetricEventTableType;

INSERT 
INTO    @TestTVP
		( 
			MetricName, 
			MetricDescription, 
			EventTime, 
			Duration     
		)
VALUES  (
			'Metric1Name', 
			'Metric1Description', 
			GETDATE(), 
			101
		);

INSERT 
INTO    @TestTVP
		( 
			MetricName, 
			MetricDescription, 
			EventTime, 
			Duration     
		)
VALUES  (
			'Metric1Name', 
			null, 
			GETDATE(), 
			102
		);

INSERT 
INTO    @TestTVP
		( 
			MetricName, 
			MetricDescription, 
			EventTime, 
			Duration     
		)
VALUES  (
			'Metric2Name', 
			'Metric2Description', 
			GETDATE(), 
			201
		);

INSERT 
INTO    @TestTVP
		( 
			MetricName, 
			MetricDescription, 
			EventTime, 
			Duration     
		)
VALUES  (
			'Metric2Name', 
			null, 
			GETDATE(), 
			202
		);

INSERT 
INTO    @TestTVP
		( 
			MetricName, 
			MetricDescription, 
			EventTime, 
			Duration     
		)
VALUES  (
			'Metric2Name', 
			null, 
			GETDATE(), 
			203
		);

EXEC InsertIntervalMetrics 'TestCategory' , @TestTVP;

--SELECT  *
--FROM    @TestTVP;

SELECT  *
FROM    IntervalMetricInstances

SELECT  *
FROM	Categories

SELECT  *
FROM    IntervalMetrics

DELETE  
FROM    IntervalMetricInstances;

DELETE  
FROM    IntervalMetrics;

DELETE  
FROM    Categories;