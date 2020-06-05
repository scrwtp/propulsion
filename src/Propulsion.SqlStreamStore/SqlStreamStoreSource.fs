namespace Propulsion.SqlStreamStore

open System
open SqlStreamStore

type SqlStreamStoreSource =

    static member Run
            (logger : Serilog.ILogger,
             store: IStreamStore,
             checkpointer: ICheckpointer,
             sink : Propulsion.ProjectorPipeline<_>,
             consumerGroup: string,
             ?statsInterval: TimeSpan,
             ?readerMaxBatchSize: int,
             ?readerSleepInterval: TimeSpan) : Async<unit> =

        async {
            let streamId = "$all"

            let logger =
                let instanceId = System.Guid.NewGuid()
                logger
                    .ForContext("instanceId", string instanceId)
                    .ForContext("consumerGroup", consumerGroup)

            let ingester : Propulsion.Ingestion.Ingester<_,_> =
                sink.StartIngester(logger, 0)

            let reader =
                StreamReader(logger,
                             store,
                             checkpointer,
                             ingester.Submit,
                             streamId,
                             consumerGroup,
                             ?maxBatchSize = readerMaxBatchSize,
                             ?sleepInterval = readerSleepInterval,
                             ?statsInterval = statsInterval)

            try
                let! position =
                    checkpointer.GetPosition(streamId, consumerGroup)

                do! reader.Start(position)
            with
            | exc ->
                logger.Error(exc, "Exception while running reader loop")
                return! Async.Raise exc
        }
