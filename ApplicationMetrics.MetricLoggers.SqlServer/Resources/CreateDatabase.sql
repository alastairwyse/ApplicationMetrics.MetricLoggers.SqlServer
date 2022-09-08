
-- TODO: Think about what are the right indices for instance tables
--   Should maybe be compound on eventtime and then category/metric


--------------------------------------------------------------------------------
--------------------------------------------------------------------------------
-- Create Database
--------------------------------------------------------------------------------
--------------------------------------------------------------------------------

CREATE DATABASE ApplicationMetrics;


--------------------------------------------------------------------------------
--------------------------------------------------------------------------------
-- Create Tables
--------------------------------------------------------------------------------
--------------------------------------------------------------------------------

CREATE TABLE ApplicationMetrics.dbo.Categories
(
    Id               bigint         NOT NULL IDENTITY(1,1) PRIMARY KEY,  
    Name             nvarchar(450)  NOT NULL
);

CREATE TABLE ApplicationMetrics.dbo.CountMetrics
(
    Id               bigint          NOT NULL IDENTITY(1,1) PRIMARY KEY,  
    Name             nvarchar(450)   NOT NULL, 
    Description      nvarchar(4000)  NOT NULL, 
);

CREATE TABLE ApplicationMetrics.dbo.AmountMetrics
(
    Id               bigint          NOT NULL IDENTITY(1,1) PRIMARY KEY,  
    Name             nvarchar(450)   NOT NULL, 
    Description      nvarchar(4000)  NOT NULL, 
);

CREATE TABLE ApplicationMetrics.dbo.StatusMetrics
(
    Id               bigint          NOT NULL IDENTITY(1,1) PRIMARY KEY,  
    Name             nvarchar(450)   NOT NULL, 
    Description      nvarchar(4000)  NOT NULL, 
);

CREATE TABLE ApplicationMetrics.dbo.IntervalMetrics
(
    Id               bigint          NOT NULL IDENTITY(1,1) PRIMARY KEY,  
    Name             nvarchar(450)   NOT NULL, 
    Description      nvarchar(4000)  NOT NULL, 
);

CREATE TABLE ApplicationMetrics.dbo.CountMetricInstances
(
    Id              bigint     NOT NULL IDENTITY(1,1) PRIMARY KEY,  
    CategoryId      bigint     NOT NULL, 
    EventTime       datetime2  NOT NULL, 
    CountMetricId   bigint     NOT NULL
);

CREATE INDEX CountMetricInstancesCategoryIdIndex ON ApplicationMetrics.dbo.CountMetricInstances (CategoryId);
CREATE INDEX CountMetricInstancesCountMetricIdIndex ON ApplicationMetrics.dbo.CountMetricInstances (CountMetricId);
CREATE INDEX CountMetricInstancesEventTimeIndex ON ApplicationMetrics.dbo.CountMetricInstances (EventTime);

CREATE TABLE ApplicationMetrics.dbo.AmountMetricInstances
(
    Id              bigint     NOT NULL IDENTITY(1,1) PRIMARY KEY, 
    CategoryId      bigint     NOT NULL, 
    AmountMetricId  bigint     NOT NULL, 
    EventTime       datetime2  NOT NULL, 
    Amount          bigint     NOT NULL
);

CREATE INDEX AmountMetricInstancesCategoryIdIndex ON ApplicationMetrics.dbo.AmountMetricInstances (CategoryId);
CREATE INDEX AmountMetricInstancesAmountMetricIdIndex ON ApplicationMetrics.dbo.AmountMetricInstances (AmountMetricId);
CREATE INDEX AmountMetricInstancesEventTimeIndex ON ApplicationMetrics.dbo.AmountMetricInstances (EventTime);

CREATE TABLE ApplicationMetrics.dbo.StatusMetricInstances
(
    Id              bigint     NOT NULL IDENTITY(1,1) PRIMARY KEY, 
    CategoryId      bigint     NOT NULL, 
    StatusMetricId  bigint     NOT NULL, 
    EventTime       datetime2  NOT NULL, 
    Value           bigint     NOT NULL
);

CREATE INDEX StatusMetricInstancesCategoryIdIndex ON ApplicationMetrics.dbo.StatusMetricInstances (CategoryId);
CREATE INDEX StatusMetricInstancesAmountMetricIdIndex ON ApplicationMetrics.dbo.StatusMetricInstances (StatusMetricId);
CREATE INDEX StatusMetricInstancesEventTimeIndex ON ApplicationMetrics.dbo.StatusMetricInstances (EventTime);

CREATE TABLE ApplicationMetrics.dbo.IntervalMetricInstances
(
    Id                bigint     NOT NULL IDENTITY(1,1) PRIMARY KEY, 
    CategoryId        bigint     NOT NULL, 
    IntervalMetricId  bigint     NOT NULL, 
    EventTime         datetime2  NOT NULL, 
    Duration          bigint     NOT NULL
);

CREATE INDEX IntervalMetricInstancesCategoryIdIndex ON ApplicationMetrics.dbo.IntervalMetricInstances (CategoryId);
CREATE INDEX IntervalMetricInstancesAmountMetricIdIndex ON ApplicationMetrics.dbo.IntervalMetricInstances (IntervalMetricId);
CREATE INDEX IntervalMetricInstancesEventTimeIndex ON ApplicationMetrics.dbo.IntervalMetricInstances (EventTime);

CREATE TABLE ApplicationMetrics.dbo.SchemaVersions
(
    Id         bigint        NOT NULL IDENTITY(1,1) PRIMARY KEY,  
    [Version]  nvarchar(20)  NOT NULL, 
    Created    datetime2     NOT NULL, 
)


--------------------------------------------------------------------------------
--------------------------------------------------------------------------------
-- Create Views
--------------------------------------------------------------------------------
--------------------------------------------------------------------------------

USE ApplicationMetrics
GO

CREATE VIEW dbo.CountMetricInstancesView AS
SELECT  cmi.Id          Id, 
        c.Name          Category, 
        cm.Name         CountMetric, 
        cm.Description  CountMetricDescription, 
        cmi.EventTime   EventTime
FROM    CountMetricInstances cmi
        INNER JOIN CountMetrics cm
          ON cmi.CountMetricId = cm.Id
        Inner JOIN Categories c
          ON cmi.CategoryId = c.Id;
GO

CREATE VIEW dbo.AmountMetricInstancesView AS
SELECT  ami.Id          Id, 
        c.Name          Category, 
        am.Name         AmountMetric, 
        am.Description  AmountMetricDescription, 
        ami.EventTime   EventTime, 
        ami.Amount      Amount
FROM    AmountMetricInstances ami
        INNER JOIN AmountMetrics am
          ON ami.AmountMetricId = am.Id
        Inner JOIN Categories c
          ON ami.CategoryId = c.Id;
GO

CREATE VIEW dbo.StatusMetricInstancesView AS
SELECT  smi.Id          Id, 
        c.Name          Category, 
        sm.Name         StatusMetric, 
        sm.Description  StatusMetricDescription, 
        smi.EventTime   EventTime, 
        smi.Value       Value
FROM    StatusMetricInstances smi
        INNER JOIN StatusMetrics sm
          ON smi.StatusMetricId = sm.Id
        Inner JOIN Categories c
          ON smi.CategoryId = c.Id;
GO
          
CREATE VIEW dbo.IntervalMetricInstancesView AS
SELECT  imi.Id          Id, 
        c.Name          Category, 
        im.Name         IntervalMetric, 
        im.Description  IntervalMetricDescription, 
        imi.EventTime   EventTime, 
        imi.Duration    Duration
FROM    IntervalMetricInstances imi
        INNER JOIN IntervalMetrics im
          ON imi.IntervalMetricId = im.Id
        Inner JOIN Categories c
          ON imi.CategoryId = c.Id;
GO

CREATE VIEW dbo.AllMetricInstancesView AS
SELECT  Id                      Id, 
        Category                Category, 
        'Count'                 MetricType, 
        CountMetric             MetricName, 
        CountMetricDescription  MetricDescription, 
        EventTime               EventTime, 
        null                    Value
FROM    CountMetricInstancesView
UNION ALL
SELECT  Id, 
        Category, 
        'Amount', 
        AmountMetric, 
        AmountMetricDescription, 
        EventTime, 
        Amount
FROM    AmountMetricInstancesView
UNION ALL
SELECT  Id, 
        Category, 
        'Status', 
        StatusMetric, 
        StatusMetricDescription, 
        EventTime, 
        Value
FROM    StatusMetricInstancesView
UNION ALL
SELECT  Id, 
        Category, 
        'Interval', 
        IntervalMetric, 
        IntervalMetricDescription, 
        EventTime, 
        Duration
FROM    IntervalMetricInstancesView;
GO


--------------------------------------------------------------------------------
--------------------------------------------------------------------------------
-- Create User-defined Types
--------------------------------------------------------------------------------
--------------------------------------------------------------------------------

USE ApplicationMetrics
GO 

CREATE TYPE dbo.CountMetricEventTableType 
AS TABLE
(
    Id                 bigint          NOT NULL PRIMARY KEY, 
    MetricName         nvarchar(max), 
    MetricDescription  nvarchar(max), 
    EventTime          datetime2
);

CREATE TYPE dbo.AmountMetricEventTableType 
AS TABLE
(
    Id                 bigint          NOT NULL PRIMARY KEY, 
    MetricName         nvarchar(max), 
    MetricDescription  nvarchar(max), 
    EventTime          datetime2,
    Amount             bigint
);

CREATE TYPE dbo.StatusMetricEventTableType 
AS TABLE
(
    Id                 bigint          NOT NULL PRIMARY KEY, 
    MetricName         nvarchar(max), 
    MetricDescription  nvarchar(max), 
    EventTime          datetime2,
    Value              bigint
);

CREATE TYPE dbo.IntervalMetricEventTableType 
AS TABLE
(
    Id                 bigint          NOT NULL PRIMARY KEY, 
    MetricName         nvarchar(max), 
    MetricDescription  nvarchar(max), 
    EventTime          datetime2,
    Duration           bigint
);


--------------------------------------------------------------------------------
--------------------------------------------------------------------------------
-- Create Functions / Stored Procedures
--------------------------------------------------------------------------------
--------------------------------------------------------------------------------

USE ApplicationMetrics
GO 

--------------------------------------------------------------------------------
-- dbo.InsertCountMetrics

CREATE PROCEDURE dbo.InsertCountMetrics
(
    @Category       nvarchar(max), 
    @CountMetrics   CountMetricEventTableType  READONLY
)
AS
BEGIN

    DECLARE @ErrorMessage         nvarchar(max);

    DECLARE @CurrentMetricName         nvarchar(max);
    DECLARE @CurrentMetricDescription  nvarchar(max);
    DECLARE @CurrentEventTime          datetime2;

    DECLARE InputTableCursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT  MetricName, 
            MetricDescription, 
            EventTime
    FROM    @CountMetrics
    ORDER   BY Id;

    BEGIN TRANSACTION

    OPEN InputTableCursor;
    FETCH NEXT 
    FROM        InputTableCursor
    INTO        @CurrentMetricName, 
                @CurrentMetricDescription, 
                @CurrentEventTime;

    WHILE (@@FETCH_STATUS) = 0
	BEGIN
        BEGIN TRY
            INSERT  
            INTO    dbo.CountMetricInstances 
                    (
                        CategoryId, 
                        CountMetricId, 
                        EventTime
                    )
            VALUES  (
                        ( 
                            SELECT  Id 
                            FROM    dbo.Categories 
                            WHERE   Name = @Category 
                        ), 
                        ( 
                            SELECT  Id 
                            FROM    dbo.CountMetrics
                            WHERE   Name = @CurrentMetricName 
                        ), 
                        @CurrentEventTime
                    );
        END TRY
        BEGIN CATCH
            IF (ERROR_NUMBER() = 515)
            BEGIN
                -- Insert failed due to 'Cannot insert the value NULL into column' error
                --   Need to ensure @Category and @CurrentMetricName exist
                DECLARE @CategoryId  bigint;

                SELECT  @CategoryId = Id 
                FROM    dbo.Categories 
                WHERE   Name = @Category;
					
                IF (@CategoryId IS NULL)
                BEGIN TRY
                    -- Insert @Category
                    INSERT  
                    INTO    dbo.Categories 
                            (
                                Name
                            )
                    VALUES  (
                                @Category
                            );
                END TRY
                BEGIN CATCH
                    ROLLBACK TRANSACTION
                    SET @ErrorMessage = N'Error occurred when inserting Category ''' + ISNULL(@Category, '(null)') + '''; ' + ERROR_MESSAGE();
                    THROW 50001, @ErrorMessage, 1;
                END CATCH

                DECLARE @CountMetricId  bigint;

                SELECT  @CountMetricId = Id 
                FROM    dbo.CountMetrics 
                WHERE   Name = @CurrentMetricName;
            
                IF (@CountMetricId IS NULL)
                BEGIN TRY
                    -- Insert @CurrentMetricName
                    INSERT  
                    INTO    dbo.CountMetrics 
                            (
                                Name, 
                                Description
                            )
                    VALUES  (
                                @CurrentMetricName, 
                                @CurrentMetricDescription
                            );
                END TRY
                BEGIN CATCH
                    ROLLBACK TRANSACTION
                    SET @ErrorMessage = N'Error occurred when inserting CountMetric ''' + ISNULL(@CurrentMetricName, '(null)') + '''; ' + ERROR_MESSAGE();
                    THROW 50001, @ErrorMessage, 1;
                END CATCH

                -- Repeat the original insert
                BEGIN TRY
                    INSERT  
                    INTO    dbo.CountMetricInstances 
                            (
                                CategoryId, 
                                CountMetricId, 
                                EventTime
                            )
                    VALUES  (
                                ( 
                                    SELECT  Id 
                                    FROM    dbo.Categories 
                                    WHERE   Name = @Category 
                                ), 
                                ( 
                                    SELECT  Id 
                                    FROM    dbo.CountMetrics
                                    WHERE   Name = @CurrentMetricName 
                                ), 
                                @CurrentEventTime
                            );
                END TRY
                BEGIN CATCH
                    ROLLBACK TRANSACTION
                    SET @ErrorMessage = N'Error occurred when inserting count metric instance for category ''' + ISNULL(@Category, '(null)') + ''' and count metric ''' + ISNULL(@CurrentMetricName, '(null)') + ''' ; ' + ERROR_MESSAGE();
                    THROW 50001, @ErrorMessage, 1;
                END CATCH
            END
            ELSE  -- i.e. ERROR_NUMBER() != 515
            BEGIN
                ROLLBACK TRANSACTION
                SET @ErrorMessage = N'Error occurred when inserting count metric instance for category ''' + ISNULL(@Category, '(null)') + ''' and count metric ''' + ISNULL(@CurrentMetricName, '(null)') + ''' ; ' + ERROR_MESSAGE();
                THROW 50001, @ErrorMessage, 1;
            END
        END CATCH

		FETCH NEXT 
		FROM        InputTableCursor
		INTO        @CurrentMetricName, 
					@CurrentMetricDescription, 
					@CurrentEventTime;

	END;
    CLOSE InputTableCursor;
    DEALLOCATE InputTableCursor;

    COMMIT TRANSACTION

END
GO

--------------------------------------------------------------------------------
-- dbo.InsertAmountMetrics

CREATE PROCEDURE dbo.InsertAmountMetrics
(
    @Category       nvarchar(max), 
    @AmountMetrics  AmountMetricEventTableType  READONLY
)
AS
BEGIN

    DECLARE @ErrorMessage         nvarchar(max);

    DECLARE @CurrentMetricName         nvarchar(max);
    DECLARE @CurrentMetricDescription  nvarchar(max);
    DECLARE @CurrentEventTime          datetime2;
    DECLARE @CurrentAmount             bigint

    DECLARE InputTableCursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT  MetricName, 
            MetricDescription, 
            EventTime, 
            Amount
    FROM    @AmountMetrics
    ORDER   BY Id;

    BEGIN TRANSACTION

    OPEN InputTableCursor;
    FETCH NEXT 
    FROM        InputTableCursor
    INTO        @CurrentMetricName, 
                @CurrentMetricDescription, 
                @CurrentEventTime, 
                @CurrentAmount;

    WHILE (@@FETCH_STATUS) = 0
	BEGIN
        BEGIN TRY
            INSERT  
            INTO    dbo.AmountMetricInstances 
                    (
                        CategoryId, 
                        AmountMetricId, 
                        EventTime, 
                        Amount 
                    )
            VALUES  (
                        ( 
                            SELECT  Id 
                            FROM    dbo.Categories 
                            WHERE   Name = @Category 
                        ), 
                        ( 
                            SELECT  Id 
                            FROM    dbo.AmountMetrics
                            WHERE   Name = @CurrentMetricName 
                        ), 
                        @CurrentEventTime, 
                        @CurrentAmount 
                    );
        END TRY
        BEGIN CATCH
            IF (ERROR_NUMBER() = 515)
            BEGIN
                -- Insert failed due to 'Cannot insert the value NULL into column' error
                --   Need to ensure @Category and @CurrentMetricName exist
                DECLARE @CategoryId  bigint;

                SELECT  @CategoryId = Id 
                FROM    dbo.Categories 
                WHERE   Name = @Category;
					
                IF (@CategoryId IS NULL)
                BEGIN TRY
                    -- Insert @Category
                    INSERT  
                    INTO    dbo.Categories 
                            (
                                Name
                            )
                    VALUES  (
                                @Category
                            );
                END TRY
                BEGIN CATCH
                    ROLLBACK TRANSACTION
                    SET @ErrorMessage = N'Error occurred when inserting Category ''' + ISNULL(@Category, '(null)') + '''; ' + ERROR_MESSAGE();
                    THROW 50001, @ErrorMessage, 1;
                END CATCH

                DECLARE @AmountMetricId  bigint;

                SELECT  @AmountMetricId = Id 
                FROM    dbo.AmountMetrics 
                WHERE   Name = @CurrentMetricName;
            
                IF (@AmountMetricId IS NULL)
                BEGIN TRY
                    -- Insert @CurrentMetricName
                    INSERT  
                    INTO    dbo.AmountMetrics 
                            (
                                Name, 
                                Description
                            )
                    VALUES  (
                                @CurrentMetricName, 
                                @CurrentMetricDescription
                            );
                END TRY
                BEGIN CATCH
                    ROLLBACK TRANSACTION
                    SET @ErrorMessage = N'Error occurred when inserting AmountMetric ''' + ISNULL(@CurrentMetricName, '(null)') + '''; ' + ERROR_MESSAGE();
                    THROW 50001, @ErrorMessage, 1;
                END CATCH

                -- Repeat the original insert
                BEGIN TRY
                    INSERT  
                    INTO    dbo.AmountMetricInstances 
                            (
                                CategoryId, 
                                AmountMetricId, 
                                EventTime, 
                                Amount 
                            )
                    VALUES  (
                                ( 
                                    SELECT  Id 
                                    FROM    dbo.Categories 
                                    WHERE   Name = @Category 
                                ), 
                                ( 
                                    SELECT  Id 
                                    FROM    dbo.AmountMetrics
                                    WHERE   Name = @CurrentMetricName 
                                ), 
                                @CurrentEventTime, 
                                @CurrentAmount 
                            );
                END TRY
                BEGIN CATCH
                    ROLLBACK TRANSACTION
                    SET @ErrorMessage = N'Error occurred when inserting amount metric instance for category ''' + ISNULL(@Category, '(null)') + ''' and amount metric ''' + ISNULL(@CurrentMetricName, '(null)') + ''' ; ' + ERROR_MESSAGE();
                    THROW 50001, @ErrorMessage, 1;
                END CATCH
            END
            ELSE  -- i.e. ERROR_NUMBER() != 515
            BEGIN
                ROLLBACK TRANSACTION
                SET @ErrorMessage = N'Error occurred when inserting amount metric instance for category ''' + ISNULL(@Category, '(null)') + ''' and amount metric ''' + ISNULL(@CurrentMetricName, '(null)') + ''' ; ' + ERROR_MESSAGE();
                THROW 50001, @ErrorMessage, 1;
            END
        END CATCH

		FETCH NEXT 
		FROM        InputTableCursor
		INTO        @CurrentMetricName, 
					@CurrentMetricDescription, 
					@CurrentEventTime, 
					@CurrentAmount;

	END;
    CLOSE InputTableCursor;
    DEALLOCATE InputTableCursor;

    COMMIT TRANSACTION

END
GO

--------------------------------------------------------------------------------
-- dbo.InsertStatusMetrics

CREATE PROCEDURE dbo.InsertStatusMetrics
(
    @Category       nvarchar(max), 
    @StatusMetrics  StatusMetricEventTableType  READONLY
)
AS
BEGIN

    DECLARE @ErrorMessage         nvarchar(max);

    DECLARE @CurrentMetricName         nvarchar(max);
    DECLARE @CurrentMetricDescription  nvarchar(max);
    DECLARE @CurrentEventTime          datetime2;
    DECLARE @CurrentValue              bigint

    DECLARE InputTableCursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT  MetricName, 
            MetricDescription, 
            EventTime, 
            Value
    FROM    @StatusMetrics
    ORDER   BY Id;

    BEGIN TRANSACTION

    OPEN InputTableCursor;
    FETCH NEXT 
    FROM        InputTableCursor
    INTO        @CurrentMetricName, 
                @CurrentMetricDescription, 
                @CurrentEventTime, 
                @CurrentValue;

    WHILE (@@FETCH_STATUS) = 0
	BEGIN
        BEGIN TRY
            INSERT  
            INTO    dbo.STatusMetricInstances 
                    (
                        CategoryId, 
                        StatusMetricId, 
                        EventTime, 
                        Value 
                    )
            VALUES  (
                        ( 
                            SELECT  Id 
                            FROM    dbo.Categories 
                            WHERE   Name = @Category 
                        ), 
                        ( 
                            SELECT  Id 
                            FROM    dbo.StatusMetrics
                            WHERE   Name = @CurrentMetricName 
                        ), 
                        @CurrentEventTime, 
                        @CurrentValue 
                    );
        END TRY
        BEGIN CATCH
            IF (ERROR_NUMBER() = 515)
            BEGIN
                -- Insert failed due to 'Cannot insert the value NULL into column' error
                --   Need to ensure @Category and @CurrentMetricName exist
                DECLARE @CategoryId  bigint;

                SELECT  @CategoryId = Id 
                FROM    dbo.Categories 
                WHERE   Name = @Category;
					
                IF (@CategoryId IS NULL)
                BEGIN TRY
                    -- Insert @Category
                    INSERT  
                    INTO    dbo.Categories 
                            (
                                Name
                            )
                    VALUES  (
                                @Category
                            );
                END TRY
                BEGIN CATCH
                    ROLLBACK TRANSACTION
                    SET @ErrorMessage = N'Error occurred when inserting Category ''' + ISNULL(@Category, '(null)') + '''; ' + ERROR_MESSAGE();
                    THROW 50001, @ErrorMessage, 1;
                END CATCH

                DECLARE @StatusMetricId  bigint;

                SELECT  @StatusMetricId = Id 
                FROM    dbo.StatusMetrics 
                WHERE   Name = @CurrentMetricName;
            
                IF (@StatusMetricId IS NULL)
                BEGIN TRY
                    -- Insert @CurrentMetricName
                    INSERT  
                    INTO    dbo.StatusMetrics 
                            (
                                Name, 
                                Description
                            )
                    VALUES  (
                                @CurrentMetricName, 
                                @CurrentMetricDescription
                            );
                END TRY
                BEGIN CATCH
                    ROLLBACK TRANSACTION
                    SET @ErrorMessage = N'Error occurred when inserting StatusMetric ''' + ISNULL(@CurrentMetricName, '(null)') + '''; ' + ERROR_MESSAGE();
                    THROW 50001, @ErrorMessage, 1;
                END CATCH

                -- Repeat the original insert
                BEGIN TRY
                    INSERT  
                    INTO    dbo.StatusMetricInstances 
                            (
                                CategoryId, 
                                StatusMetricId, 
                                EventTime, 
                                Value 
                            )
                    VALUES  (
                                ( 
                                    SELECT  Id 
                                    FROM    dbo.Categories 
                                    WHERE   Name = @Category 
                                ), 
                                ( 
                                    SELECT  Id 
                                    FROM    dbo.StatusMetrics
                                    WHERE   Name = @CurrentMetricName 
                                ), 
                                @CurrentEventTime, 
                                @CurrentValue
                            );
                END TRY
                BEGIN CATCH
                    ROLLBACK TRANSACTION
                    SET @ErrorMessage = N'Error occurred when inserting status metric instance for category ''' + ISNULL(@Category, '(null)') + ''' and status metric ''' + ISNULL(@CurrentMetricName, '(null)') + ''' ; ' + ERROR_MESSAGE();
                    THROW 50001, @ErrorMessage, 1;
                END CATCH
            END
            ELSE  -- i.e. ERROR_NUMBER() != 515
            BEGIN
                ROLLBACK TRANSACTION
                SET @ErrorMessage = N'Error occurred when inserting status metric instance for category ''' + ISNULL(@Category, '(null)') + ''' and status metric ''' + ISNULL(@CurrentMetricName, '(null)') + ''' ; ' + ERROR_MESSAGE();
                THROW 50001, @ErrorMessage, 1;
            END
        END CATCH

		FETCH NEXT 
		FROM        InputTableCursor
		INTO        @CurrentMetricName, 
					@CurrentMetricDescription, 
					@CurrentEventTime, 
					@CurrentValue;

	END;
    CLOSE InputTableCursor;
    DEALLOCATE InputTableCursor;

    COMMIT TRANSACTION

END
GO

--------------------------------------------------------------------------------
-- dbo.InsertIntervalMetrics

CREATE PROCEDURE dbo.InsertIntervalMetrics
(
    @Category         nvarchar(max), 
    @IntervalMetrics  IntervalMetricEventTableType  READONLY
)
AS
BEGIN

    DECLARE @ErrorMessage         nvarchar(max);

    DECLARE @CurrentMetricName         nvarchar(max);
    DECLARE @CurrentMetricDescription  nvarchar(max);
    DECLARE @CurrentEventTime          datetime2;
    DECLARE @CurrentDuration           bigint

    DECLARE InputTableCursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT  MetricName, 
            MetricDescription, 
            EventTime, 
            Duration
    FROM    @IntervalMetrics
    ORDER   BY Id;

    BEGIN TRANSACTION

    OPEN InputTableCursor;
    FETCH NEXT 
    FROM        InputTableCursor
    INTO        @CurrentMetricName, 
                @CurrentMetricDescription, 
                @CurrentEventTime, 
                @CurrentDuration;

    WHILE (@@FETCH_STATUS) = 0
	BEGIN
        BEGIN TRY
            INSERT  
            INTO    dbo.IntervalMetricInstances 
                    (
                        CategoryId, 
                        IntervalMetricId, 
                        EventTime, 
                        Duration 
                    )
            VALUES  (
                        ( 
                            SELECT  Id 
                            FROM    dbo.Categories 
                            WHERE   Name = @Category 
                        ), 
                        ( 
                            SELECT  Id 
                            FROM    dbo.IntervalMetrics
                            WHERE   Name = @CurrentMetricName 
                        ), 
                        @CurrentEventTime, 
                        @CurrentDuration 
                    );
        END TRY
        BEGIN CATCH
            IF (ERROR_NUMBER() = 515)
            BEGIN
                -- Insert failed due to 'Cannot insert the value NULL into column' error
                --   Need to ensure @Category and @CurrentMetricName exist
                DECLARE @CategoryId  bigint;

                SELECT  @CategoryId = Id 
                FROM    dbo.Categories 
                WHERE   Name = @Category;
					
                IF (@CategoryId IS NULL)
                BEGIN TRY
                    -- Insert @Category
                    INSERT  
                    INTO    dbo.Categories 
                            (
                                Name
                            )
                    VALUES  (
                                @Category
                            );
                END TRY
                BEGIN CATCH
                    ROLLBACK TRANSACTION
                    SET @ErrorMessage = N'Error occurred when inserting Category ''' + ISNULL(@Category, '(null)') + '''; ' + ERROR_MESSAGE();
                    THROW 50001, @ErrorMessage, 1;
                END CATCH

                DECLARE @IntervalMetricId  bigint;

                SELECT  @IntervalMetricId = Id 
                FROM    dbo.IntervalMetrics 
                WHERE   Name = @CurrentMetricName;
            
                IF (@IntervalMetricId IS NULL)
                BEGIN TRY
                    -- Insert @CurrentMetricName
                    INSERT  
                    INTO    dbo.IntervalMetrics 
                            (
                                Name, 
                                Description
                            )
                    VALUES  (
                                @CurrentMetricName, 
                                @CurrentMetricDescription
                            );
                END TRY
                BEGIN CATCH
                    ROLLBACK TRANSACTION
                    SET @ErrorMessage = N'Error occurred when inserting IntervalMetric ''' + ISNULL(@CurrentMetricName, '(null)') + '''; ' + ERROR_MESSAGE();
                    THROW 50001, @ErrorMessage, 1;
                END CATCH

                -- Repeat the original insert
                BEGIN TRY
                    INSERT  
                    INTO    dbo.IntervalMetricInstances 
                            (
                                CategoryId, 
                                IntervalMetricId, 
                                EventTime, 
                                Duration 
                            )
                    VALUES  (
                                ( 
                                    SELECT  Id 
                                    FROM    dbo.Categories 
                                    WHERE   Name = @Category 
                                ), 
                                ( 
                                    SELECT  Id 
                                    FROM    dbo.IntervalMetrics
                                    WHERE   Name = @CurrentMetricName 
                                ), 
                                @CurrentEventTime, 
                                @CurrentDuration 
                            );
                END TRY
                BEGIN CATCH
                    ROLLBACK TRANSACTION
                    SET @ErrorMessage = N'Error occurred when inserting interval metric instance for category ''' + ISNULL(@Category, '(null)') + ''' and interval metric ''' + ISNULL(@CurrentMetricName, '(null)') + ''' ; ' + ERROR_MESSAGE();
                    THROW 50001, @ErrorMessage, 1;
                END CATCH
            END
            ELSE  -- i.e. ERROR_NUMBER() != 515
            BEGIN
                ROLLBACK TRANSACTION
                SET @ErrorMessage = N'Error occurred when inserting interval metric instance for category ''' + ISNULL(@Category, '(null)') + ''' and interval metric ''' + ISNULL(@CurrentMetricName, '(null)') + ''' ; ' + ERROR_MESSAGE();
                THROW 50001, @ErrorMessage, 1;
            END
        END CATCH

		FETCH NEXT 
		FROM        InputTableCursor
		INTO        @CurrentMetricName, 
					@CurrentMetricDescription, 
					@CurrentEventTime, 
					@CurrentDuration;

	END;
    CLOSE InputTableCursor;
    DEALLOCATE InputTableCursor;

    COMMIT TRANSACTION

END
GO


--------------------------------------------------------------------------------
--------------------------------------------------------------------------------
-- Update 'SchemaVersions' table
--------------------------------------------------------------------------------
--------------------------------------------------------------------------------

INSERT 
INTO    dbo.SchemaVersions
        (
            [Version], 
            Created
        )
VALUES  (
            '1.0.0', 
            GETDATE()
        );
GO