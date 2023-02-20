using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace Zametek.Maths.Graphs.Tests
{
    public class ResourceScheduleBuilderFixture
        : IDisposable
    {
        public ResourceScheduleBuilderFixture()
        {
            ResourceSchedule1_JsonString = ReadJsonFile(@"Builders\TestFiles\ResourceSchedule1.json");
            ResourceSchedule2_JsonString = ReadJsonFile(@"Builders\TestFiles\ResourceSchedule2.json");


            static string ReadJsonFile(string filename)
            {
                using StreamReader reader = File.OpenText(filename);
                string content = reader.ReadToEnd();
                JObject json = JObject.Parse(content);
                return json.ToString();
            }
        }

        public string ResourceSchedule1_JsonString { get; init; }
        public string ResourceSchedule2_JsonString { get; init; }

        public void Dispose()
        {
        }
    }
}
