USE ApplicationMetrics
GO

--------------------------------------------------------------------------------
--------------------------------------------------------------------------------
-- Create Indices
--------------------------------------------------------------------------------
--------------------------------------------------------------------------------

CREATE UNIQUE INDEX CategoriesNameIndex ON ApplicationMetrics.dbo.Categories (Name);
CREATE UNIQUE INDEX CountMetricsNameIndex ON ApplicationMetrics.dbo.CountMetrics (Name);
CREATE UNIQUE INDEX AmountMetricsNameIndex ON ApplicationMetrics.dbo.AmountMetrics (Name);
CREATE UNIQUE INDEX StatusMetricsNameIndex ON ApplicationMetrics.dbo.StatusMetrics (Name);
CREATE UNIQUE INDEX IntervalMetricsNameIndex ON ApplicationMetrics.dbo.IntervalMetrics (Name);


--------------------------------------------------------------------------------
--------------------------------------------------------------------------------
-- Update Stored Procedures
--------------------------------------------------------------------------------
--------------------------------------------------------------------------------

DROP PROCEDURE dbo.InsertIntervalMetrics;
DROP PROCEDURE dbo.InsertStatusMetrics;
DROP PROCEDURE dbo.InsertAmountMetrics;
DROP PROCEDURE dbo.InsertCountMetrics;
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
    DECLARE @CategoryId                bigint;
    DECLARE @CountMetricId             bigint;

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

                -- Take an exclusive lock on the Categories table to prevent other sessions attempting to insert the same category 
                --   (or insert the same metric... since the change to the metric table occurs with the same lock on Categories in place)
                SELECT TOP 1 Id 
                FROM   dbo.Categories WITH (UPDLOCK, TABLOCKX);

                SET @CategoryId = NULL;

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

                SET @CountMetricId = NULL;

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
				
                -- Commit and restart the transaction to allow the new category and/or metric to be available to other sessions
                COMMIT TRANSACTION
                BEGIN TRANSACTION

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
    DECLARE @CategoryId                bigint;
    DECLARE @AmountMetricId            bigint;

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

                -- Take an exclusive lock on the Categories table to prevent other sessions attempting to insert the same category 
                --   (or insert the same metric... since the change to the metric table occurs with the same lock on Categories in place)
                SELECT TOP 1 Id 
                FROM   dbo.Categories WITH (UPDLOCK, TABLOCKX);

                SET @CategoryId = NULL;

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

                SET @AmountMetricId = NULL;

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
				
                -- Commit and restart the transaction to allow the new category and/or metric to be available to other sessions
                COMMIT TRANSACTION
                BEGIN TRANSACTION

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
    DECLARE @CategoryId                bigint;
    DECLARE @StatusMetricId            bigint;

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
            IF (ERROR_NUMBER() = 515)
            BEGIN
                -- Insert failed due to 'Cannot insert the value NULL into column' error
                --   Need to ensure @Category and @CurrentMetricName exist

                -- Take an exclusive lock on the Categories table to prevent other sessions attempting to insert the same category 
                --   (or insert the same metric... since the change to the metric table occurs with the same lock on Categories in place)
                SELECT TOP 1 Id 
                FROM   dbo.Categories WITH (UPDLOCK, TABLOCKX);

                SET @CategoryId = NULL;

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

                SET @StatusMetricId = NULL;

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
				
                -- Commit and restart the transaction to allow the new category and/or metric to be available to other sessions
                COMMIT TRANSACTION
                BEGIN TRANSACTION

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
    DECLARE @CategoryId                bigint;
    DECLARE @IntervalMetricId          bigint;

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

                -- Take an exclusive lock on the Categories table to prevent other sessions attempting to insert the same category 
                --   (or insert the same metric... since the change to the metric table occurs with the same lock on Categories in place)
                SELECT TOP 1 Id 
                FROM   dbo.Categories WITH (UPDLOCK, TABLOCKX);

                SET @CategoryId = NULL;

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

                SET @IntervalMetricId = NULL;

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

                -- Commit and restart the transaction to allow the new category and/or metric to be available to other sessions
                COMMIT TRANSACTION
                BEGIN TRANSACTION

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
            '2.0.0', 
            GETDATE()
        );
GO