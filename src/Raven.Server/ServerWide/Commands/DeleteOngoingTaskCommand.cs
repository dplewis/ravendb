﻿using System.Diagnostics;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;
using Raven.Server.Utils;
using Sparrow.Json.Parsing;

namespace Raven.Server.ServerWide.Commands
{
    public class DeleteOngoingTaskCommand : UpdateDatabaseCommand
    {
        public long TaskId;
        public OngoingTaskType TaskType;

        public DeleteOngoingTaskCommand() : base(null)
        {

        }

        public DeleteOngoingTaskCommand(long taskId, OngoingTaskType taskType, string databaseName) : base(databaseName)
        {
            TaskId = taskId;
            TaskType = taskType;
        }

        public override string UpdateDatabaseRecord(DatabaseRecord record, long etag)
        {
            Debug.Assert(TaskId != 0);

            switch (TaskType)
            {
                case OngoingTaskType.Replication:
                   ExternalReplication.RemoveWatcher(ref record.ExternalReplication,TaskId);
                    break;

                case OngoingTaskType.Backup:
                    record.DeletePeriodicBackupConfiguration(TaskId);
                    return TaskId.ToString();

                case OngoingTaskType.SqlEtl:
                    var sqlEtl = record.SqlEtls?.Find(x => x.TaskId == TaskId);
                    if (sqlEtl != null)
                    {
                        record.SqlEtls.Remove(sqlEtl);
                    }
                    break;

                case OngoingTaskType.RavenEtl:
                    var ravenEtl = record.RavenEtls?.Find(x => x.TaskId == TaskId);
                    if (ravenEtl != null)
                    {
                        record.RavenEtls.Remove(ravenEtl);
                    }
                    break;
            }

            return null;
        }

        public override void FillJson(DynamicJsonValue json)
        {
            json[nameof(TaskId)] = TypeConverter.ToBlittableSupportedType(TaskId);
            json[nameof(TaskType)] = TypeConverter.ToBlittableSupportedType(TaskType);
        }
    }
}
