--------------------------------------------------------------------------------
--------------------------------------------------------------------------------
-- Drop Functions / Stored Procedures
--------------------------------------------------------------------------------
--------------------------------------------------------------------------------

USE ApplicationMetrics
GO 

DROP PROCEDURE dbo.InsertIntervalMetrics;
DROP PROCEDURE dbo.InsertStatusMetrics;
DROP PROCEDURE dbo.InsertAmountMetrics;
DROP PROCEDURE dbo.InsertCountMetrics;

--------------------------------------------------------------------------------
--------------------------------------------------------------------------------
-- Drop User-defined Types
--------------------------------------------------------------------------------
--------------------------------------------------------------------------------

DROP TYPE dbo.IntervalMetricEventTableType;
DROP TYPE dbo.StatusMetricEventTableType;
DROP TYPE dbo.AmountMetricEventTableType;
DROP TYPE dbo.CountMetricEventTableType;


--------------------------------------------------------------------------------
--------------------------------------------------------------------------------
-- Drop View
--------------------------------------------------------------------------------
--------------------------------------------------------------------------------

DROP VIEW dbo.AllMetricInstancesView;
DROP VIEW dbo.IntervalMetricInstancesView;
DROP VIEW dbo.StatusMetricInstancesView;
DROP VIEW dbo.AmountMetricInstancesView;
DROP VIEW dbo.CountMetricInstancesView;


--------------------------------------------------------------------------------
--------------------------------------------------------------------------------
-- Drop Tables
--------------------------------------------------------------------------------
--------------------------------------------------------------------------------

DROP TABLE ApplicationMetrics.dbo.SchemaVersions;
DROP TABLE ApplicationMetrics.dbo.IntervalMetricInstances;
DROP TABLE ApplicationMetrics.dbo.StatusMetricInstances;
DROP TABLE ApplicationMetrics.dbo.AmountMetricInstances;
DROP TABLE ApplicationMetrics.dbo.CountMetricInstances;
DROP TABLE ApplicationMetrics.dbo.IntervalMetrics;
DROP TABLE ApplicationMetrics.dbo.StatusMetrics;
DROP TABLE ApplicationMetrics.dbo.AmountMetrics;
DROP TABLE ApplicationMetrics.dbo.CountMetrics;
DROP TABLE ApplicationMetrics.dbo.Categories;