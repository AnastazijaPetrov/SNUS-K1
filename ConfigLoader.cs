using System.Xml.Linq;

namespace SNUS_K1
{
    public static class ConfigLoader
    {
        public static SystemConfig Load(string path)
        {
            var xml = XDocument.Load(path);
            var root = xml.Root;

            var config = new SystemConfig
            {
                WorkerCount = int.Parse(root.Element("WorkerCount").Value),
                ProducerCount = int.Parse(root.Element("ProducerCount").Value),
                MaxQueueSize = int.Parse(root.Element("MaxQueueSize").Value),
            };

            foreach (var jobEl in root.Element("Jobs").Elements("Job"))
            {
                config.Jobs.Add(new Job
                {
                    Id = Guid.NewGuid(),
                    Type = Enum.Parse<JobType>(jobEl.Attribute("Type").Value),
                    Payload = jobEl.Attribute("Payload").Value,
                    Priority = int.Parse(jobEl.Attribute("Priority").Value)
                });
            }

            return config;
        }
    }
}
