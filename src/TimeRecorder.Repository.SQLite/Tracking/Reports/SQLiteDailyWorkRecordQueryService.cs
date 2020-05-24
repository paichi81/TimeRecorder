﻿using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using TimeRecorder.Domain.Domain;
using TimeRecorder.Domain.Domain.Clients;
using TimeRecorder.Domain.Domain.Products;
using TimeRecorder.Domain.Domain.Tasks;
using TimeRecorder.Domain.Domain.Tasks.Definitions;
using TimeRecorder.Domain.Domain.Tracking;
using TimeRecorder.Domain.Domain.WorkProcesses;
using TimeRecorder.Domain.UseCase.Tracking.Reports;
using TimeRecorder.Domain.Utility;
using TimeRecorder.Repository.SQLite.Clients.Dao;
using TimeRecorder.Repository.SQLite.Products.Dao;
using TimeRecorder.Repository.SQLite.Tasks.Dao;
using TimeRecorder.Repository.SQLite.Tracking.Dao;
using TimeRecorder.Repository.SQLite.WorkProcesses.Dao;

namespace TimeRecorder.Repository.SQLite.Tracking.Reports
{
    public class SQLiteDailyWorkRecordQueryService : IDailyWorkRecordQueryService
    {
        #region SQL
        const string sql = @"
SELECT
  time.id as WorkingTimeId
  , time.taskid as WorkTaskId
  , time.ymd as ymd
  , time.starttime as starttime
  , time.endtime as endtime
  , task.title as title
  , task.taskcategory as taskcategory
  , task.clientid as clientid
  , task.processid as workprocessid
  , task.productid as productid
FROM
  workingtimes time
INNER JOIN
  worktasks task ON task.id = time.taskid
WHERE
  time.ymd BETWEEN @start AND @end
";
        #endregion

        public WorkingTimeRecordForReport[] SelectByYearMonth(YearMonth yearMonth)
        {
            var list = new List<WorkingTimeRecordForReport>();

            RepositoryAction.Query(c =>
            {
                var workTaskDao = new WorkTaskDao(c, null);
                var workingTimeDao = new WorkingTimeDao(c, null);

                var processes = new WorkProcessDao(c, null).SelectAll()
                                                           .Select(d => d.ToDomainObject())
                                                           .ToDictionary(p => p.Id);

                var products = new ProductDao(c, null).SelectAll()
                                                      .Select(d => d.ToDomainObject())
                                                      .ToDictionary(p => p.Id);

                var clients = new ClientDao(c, null).SelectAll()
                                                    .Select(d => d.ToDomainObject())
                                                    .ToDictionary(p => p.Id);

                var param = new {
                    start = yearMonth.StartDate.ToString("yyyyMMdd"),
                    end = yearMonth.EndDate.ToString("yyyyMMdd")
                };

                var rows = c.Query<TableRow>(sql, param);

                foreach (var task in rows)
                {
                    processes.TryGetValue(new Identity<WorkProcess>(task.WorkProcessId), out WorkProcess targetProcess);
                    products.TryGetValue(new Identity<Product>(task.ProductId), out Product targetProduct);
                    clients.TryGetValue(new Identity<Client>(task.ClientId), out Client targetClient);

                    if(string.IsNullOrEmpty(task.EndTime))
                    {
                        continue;
                    }

                    var dto = new WorkingTimeRecordForReport
                    {
                        Ymd = new YmdString(task.Ymd),
                        TaskCategory = task.TaskCategory,
                        WorkProcess = targetProcess,
                        Product = targetProduct ?? Product.Empty,
                        Client = targetClient ?? Client.Empty,
                        StartDateTime = DateTimeParser.ConvertFromYmdHHmmss(task.Ymd, task.StartTime).Value,
                        EndDateTime = DateTimeParser.ConvertFromYmdHHmmss(task.Ymd, task.EndTime).Value,
                        Title = task.Title,
                        WorkingTimeId = new Identity<WorkingTimeRange>(task.WorkingTimeId),
                        WorkTaskId = new Identity<WorkTask>(task.WorkTaskId),
                    };

                    list.Add(dto);
                }
            });

            return list.ToArray();
        }

        class TableRow
        {
            public int WorkingTimeId { get; set; }

            public int WorkTaskId { get; set; }

            public string Ymd { get; set; }

            public string StartTime { get; set; }

            public string EndTime { get; set; }

            public string Title { get; set; }

            public TaskCategory TaskCategory { get; set; }

            public string Remarks { get; set; }

            public int WorkProcessId { get; set; }

            public int ClientId { get; set; }

            public int ProductId { get; set; }
        }
    }
}
