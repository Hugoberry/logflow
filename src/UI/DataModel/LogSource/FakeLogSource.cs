﻿using System.Threading;

namespace LogFlow.DataModel
{
    using System;
    using System.Collections.Generic;

    internal class FakeLogSource : LogSourceBase<DataItemBase>
    {
        public override string Name => "Faked Log";

        protected override IEnumerable<int> LoadFirst(IFilter filter, CancellationToken token)
        {
            Random r = new Random();
            for (int i = 0; i < 1000; i++)
            {
                this.AddTemplate($"string templates {{0}}, {{1}}, testing long string, {i}");
            }

            int totalCount = 1000000;

            for (int i = 0; i < totalCount; i++)
            {
                if (token.IsCancellationRequested) yield break;
                var rand = r.Next(100);
                this.AddItem(new DataItemBase()
                {
                    ThreadId = i % 100,
                    Time = DateTime.UtcNow.AddSeconds(i),
                    TemplateId = r.Next(this.Templates.Count),
                    Parameters = new object[] { DateTime.UtcNow, i + 255 },
                    ProcessId = i / 100000,
                    Level = (LogLevels)(1 << (rand < 1 ? 0 : (rand < 5 ? 1 : (rand < 10 ? 2 : (rand < 55 ? 3 : 4))))),
                });

                if (i % (totalCount / 20) == 0)
                {
                    yield return i * 100 / totalCount;
                }
            }
        }

        protected override IEnumerable<int> LoadIncremental(IFilter filter, CancellationToken token)
        {
            Random r = new Random();
            this.AddTemplate("string templates {{0}}, {{1}}, testing long string, progressive");

            for (int i = 0; i < 2; i++)
            {
                if (token.IsCancellationRequested) yield break;
                var rand = r.Next(100);
                this.AddItem(new DataItemBase()
                {
                    ThreadId = i % 100,
                    Time = DateTime.UtcNow.AddSeconds(i),
                    TemplateId = r.Next(this.Templates.Count),
                    Parameters = new object[] { DateTime.UtcNow, i + 255 },
                    ProcessId = i / 100000,
                    Level = (LogLevels)(1 << (rand < 1 ? 0 : (rand < 5 ? 1 : (rand < 10 ? 2 : (rand < 55 ? 3 : 4))))),
                });

                yield return i * 100 / 2;
            }
        }
    }
}
