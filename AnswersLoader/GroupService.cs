using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using VkNet;
using VkNet.Model;
using VkNet.Model.RequestParams;

namespace AnswersLoader
{
    public class GroupReport
    {
        public Group Group { get; set; }
        
        [JsonIgnore]
        public SortedDictionary<string, List<bool>> UsersWithMarks { get; set; }

        public GroupReport(Group group)
        {
            UsersWithMarks = new SortedDictionary<string, List<bool>>();
            Group = group;
        }
    }

    public class Group
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        
        [JsonProperty("tests")]
        public string[] Tests { get; set; }

        [JsonIgnore]
        private List<DateTime> tsds;

        public List<DateTime> GetTestsDates()
        {
            if (tsds != null) return tsds;
            
            tsds = new List<DateTime>();
            foreach (var s in Tests)
            {
                tsds.Add(DateTime.Parse(s));
            }
            tsds.Sort();

            return tsds;
        }
        
        // [JsonIgnore]
        // public List<DateTime> TestsDates 
        // { 
        //     get
        //     {
        //         if (tsds == null)
        //         {
        //             foreach (var s in Tests)
        //             {
        //                 tsds.Add(DateTime.Parse(s));
        //             }
        //             tsds.Sort();
        //         }
        //
        //         return tsds;
        //     }
        // }
    }
    
    public class GroupService
    {
        private VkApi _api;
        private DateTime _lowerDateLimit;
        private int _dateNeighborhood;
        private int _sleepTime;

        public GroupService(VkApi api, IConfiguration config)
        {
            _api = api;
            _sleepTime = config.GetSection("sleep_time_ms").Get<int>();
            _dateNeighborhood = config.GetSection("date_neighborhood").Get<int>();

            var now = DateTime.Now;
            // если сейчас второе полугодие, то крайняя дата - 1 сентября прошлого года. Иначе: первое сентября текущего года.
            _lowerDateLimit = now.Month < 9 ? new DateTime(now.Year - 1, 9, 1) : new DateTime(now.Year, 9, 1) ;
        }
        
        public IEnumerable<Message> GetAllMessagesByGroup(Group group)
        {
            const uint countMessages = 100;
            var result = new List<Message>();
            var offset = 0u;

            while (true)
            {
                var messages = _api.Messages.Search(new MessagesSearchParams
                {
                    Query = group.Name,
                    Extended = true,
                    Count = countMessages,
                    Offset = offset
                });

                var items = messages.Items.Where(m => m.Date >= _lowerDateLimit).ToList();
                offset += countMessages;
                result.AddRange(items);
                
                if (items.Count < countMessages) {break;}
                Thread.Sleep(_sleepTime);
            }

            return result.Where(m => !String.IsNullOrEmpty(m.Text) && GetUserName(m) != "").Reverse();
        }

        public GroupReport GetGroupReport(Group group, IEnumerable<Message> allGroupMessages)
        {
            var result = new SortedDictionary<string, List<Message>>();
            var usersAnswers = allGroupMessages.GroupBy(m => m.PeerId);
            foreach (var answersByUser in usersAnswers)
            {
                var answerList = answersByUser.ToList();
                answerList.Sort((a, b) => a.Date.Value.CompareTo(b.Date.Value));
                
                var acceptedAnswers = GetAcceptedAnswers(answersByUser.Key, answerList);
                if (acceptedAnswers.Count == 0) { continue; }

                var userName = GetUserName(answerList[0]);
                result[userName] = acceptedAnswers;
            }
            
            if (result.Count == 0) { return null; }

            var report = new GroupReport(group);
            foreach (var (name, answers) in result)
            {
                var marks = GetUserMarks(group, answers);
                report.UsersWithMarks[name] = marks;
            }

            return report;
        }

        private List<bool> GetUserMarks(Group group, List<Message> answers)
        {
            var result = new List<bool>(group.GetTestsDates().Count);
            int j = 0;
            foreach (var d in group.GetTestsDates())
            {
                if (j < answers.Count && Math.Abs(d.Date.Subtract(answers[j].Date.Value.Date).TotalDays) < _dateNeighborhood)
                {
                    result.Add(true);
                    j++;
                }
                else
                {
                    result.Add(false);
                }
            }

            return result;
        }
        
        private string GetUserName(Message msg)
        {
            var firstLine = msg.Text.Split('\n')[0];
            
            var regex = new Regex(@"\w{2,}_\d{2}\.? (\w+ \w+\s?\w*)");
            var match = regex.Match(firstLine);

            if (!match.Success && match.Groups.Count < 2)
            {
                return string.Empty;
            }

            return match.Groups[1].Value.Trim();
        }

        private List<Message> GetAcceptedAnswers(long? peerId, List<Message> answers)
        {
            var result = new List<Message>();
            var allUserMessages = GetAllMessages(peerId, answers[0].ConversationMessageId - 1);

            var answerIdx = 0;
            var alreadyAdded = false;
            foreach (var m in allUserMessages)
            {
                if (answerIdx + 1 < answers.Count && m.Id == answers[answerIdx + 1].Id)
                {
                    answerIdx++;
                    alreadyAdded = false;
                    continue;
                }

                var lowerText = m.Text.ToLower();
                if (!alreadyAdded && m.FromId == 3704270 && 
                    (lowerText.Contains("получено") ||
                     lowerText.Contains("принято")))
                {
                    result.Add(answers[answerIdx]);
                    alreadyAdded = true;
                }
            }

            result.Sort((a,b) => a.Date.Value.CompareTo(b.Date.Value));
            return result;
        }

        private List<Message> GetAllMessages(long? peerId, long? start)
        {
            var result = new List<Message>();
            var offset = start;
            const int count = 200;
            while (true)
            {
                var items = _api.Messages.GetHistory(new MessagesGetHistoryParams
                {
                    Offset = offset,
                    Reversed = true,
                    Count = count,
                    PeerId = peerId
                }).Messages.ToList();
                offset += count;
                result.AddRange(items);
                if (items.Count < count) { break; }
                
                Thread.Sleep(_sleepTime);
            }

            return result;
        }
    }
}