using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Configuration;
using VkNet;
using VkNet.Model;
using VkNet.Model.RequestParams;

namespace AnswersLoader
{
    class Program
    {
        static void Main(string[] args)
        {
            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", true, true)
                .Build();

            var groups = config.GetSection("groups").Get<Group[]>();
            var service = new GroupService(ApiBuilder.GetApi(config), config);
            
            var reports = new List<GroupReport>(groups.Length);
            foreach (var g in groups)
            {
                Console.WriteLine($"Загружаем группу {g.Name}...");
                var messages = service.GetAllMessagesByGroup(g);

                Console.WriteLine("Обрабатываем сообщения...");
                if (messages.Any())
                {
                    var report = service.GetGroupReport(g, messages);
                    if (report != null)
                    {
                        reports.Add(report);
                    }
                }
                
                Console.WriteLine("------------");
            }
            
            var excelService = new ExcelReportService(reports);
            excelService.GenerateExcelReport("Отчёт по группам.xlsx");
        }
    }
}