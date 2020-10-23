using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Extensions.Configuration;
using VkNet;
using VkNet.Model;
using VkNet.Model.RequestParams;

namespace AnswersLoader
{
    public class GroupReport
    {
        public string Name { get; set; }
        public List<DateTime> TestsDates { get; set; }
        public SortedDictionary<string, List<bool>> UsersWithMarks { get; set; }
        public GroupReport(string name, List<string> sortedNames, in int maxAnswCount)
        {
            Name = name;
            UsersWithMarks = new SortedDictionary<string, List<bool>>();
            sortedNames.ForEach(n => UsersWithMarks.Add(n, new List<bool>()));
            TestsDates = new List<DateTime>(maxAnswCount);
        }
    }
    public class GroupService
    {
        private VkApi _api;
        private DateTime _lowerDateLimit;
        private int _sleepTime;

        public GroupService(VkApi api, IConfiguration config)
        {
            _api = api;
            _sleepTime = config.GetSection("sleep_time_ms").Get<int>();

            var now = DateTime.Now;
            // если сейчас второе полугодие, то крайняя дата - 1 сентября прошлого года. Иначе: первое сентября текущего года.
            _lowerDateLimit = now.Month < 9 ? new DateTime(now.Year - 1, 9, 1) : new DateTime(now.Year, 9, 1) ;
        }
        
        public IEnumerable<Message> GetAllMessagesByGroup(string group)
        {
            const uint countMessages = 20;
            var result = new List<Message>();
            var offset = 0u;

            while (true)
            {
                var messages = _api.Messages.Search(new MessagesSearchParams
                {
                    Query = group,
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

        public GroupReport GetGroupReport(string groupName, IEnumerable<Message> allGroupMessages)
        {
            var result = new SortedDictionary<string, List<Message>>();
            var usersAnswers = allGroupMessages.GroupBy(m => m.PeerId);
            foreach (var answersByUser in usersAnswers)
            {
                var answerList = answersByUser.ToList();
                var userName = GetUserName(answerList[0]);
                result[userName] = GetAcceptedAnswers(answersByUser.Key, answerList);
            }

            var sortedNames = result.Keys.ToList();
            var answerLists = result.Values.ToList();
            var maxAnswCount = answerLists.Max(l => l.Count);

            var report = new GroupReport(groupName, sortedNames, maxAnswCount);

            for (int i = 0; i < maxAnswCount; i++)
            {
                var mode = GetModeDate(answerLists);
                var marks = GetMarksAndRemoveAnsw(mode, answerLists);

                report.TestsDates.Add(mode);
                
                var j = 0;
                foreach (var name in sortedNames)
                {
                    report.UsersWithMarks[name].Add(marks[j]);
                    j++;
                }
            }

            return report;
        }

        private DateTime GetModeDate(List<List<Message>> answerLists)
        {
            var counter = new Dictionary<DateTime, int>();
            foreach (var l in answerLists)
            {
                if (l.Count == 0) { continue; }
                
                var date = l[0].Date.Value.Date;
                if (counter.ContainsKey(date))
                {
                    counter[date]++;
                }
                else
                {
                    counter[date] = 1;
                }
            }

            var modeDate = counter
                .Aggregate((l, r) => l.Value > r.Value ? l : r).Key;

            return modeDate;
        }

        private List<bool> GetMarksAndRemoveAnsw(DateTime mode, List<List<Message>> answerLists)
        {
            var marks = new List<bool>(answerLists.Count);
            foreach (var l in answerLists)
            {
                if (l.Count != 0 && (Math.Abs(l[0].Date.Value.Subtract(mode).TotalDays) <= 1))
                {
                    marks.Add(true);
                    l.RemoveAt(0);
                }
                else
                {
                    marks.Add(false);
                }
            }

            return marks;
        }

        private string GetUserName(Message msg)
        {
            var firstLine = msg.Text.Split('\n')[0];
            
            var regex = new Regex(@"\w{2,}_\d{2}\.? (\w+ \w+ \w*)");
            var match = regex.Match(firstLine);

            if (!match.Success && match.Groups.Count < 2)
            {
                return string.Empty;
            }

            return match.Groups[1].Value;
        }

        private List<Message> GetAcceptedAnswers(long? peerId, List<Message> answers)
        {
            var result = new List<Message>();
            var allUserMessages = _api.Messages.GetHistory(new MessagesGetHistoryParams
            {
                Offset = answers[0].ConversationMessageId - 1,
                Reversed = true,
                Count = 200,
                PeerId = peerId
            }).Messages.ToList();

            var answerIdx = 0;
            var alreadyAdded = false;
            foreach (var m in allUserMessages)
            {
                if (answerIdx + 1 < answers.Count() && m.Id == answers[answerIdx + 1].Id)
                {
                    answerIdx++;
                    alreadyAdded = false;
                    //continue;
                }

                if (!alreadyAdded && m.FromId == 3704270 && m.Text.ToLower().Contains("получено"))
                {
                    result.Add(answers[answerIdx]);
                    alreadyAdded = true;
                }
            }

            return result;
        }
    }
}