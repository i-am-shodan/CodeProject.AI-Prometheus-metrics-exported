using Docker.DotNet;
using Docker.DotNet.Models;
using System.Text;

namespace Log2Metric
{
    public class DockerContainerLogStream
    {
        private readonly DockerClient client;
        private readonly Task readLogs;

        public DockerContainerLogStream(string containerName, ILogger<DockerContainerLogStream> logger)
        {
            client = new DockerClientConfiguration().CreateClient();
            logger.LogInformation("Docker client created");

            readLogs = new Task(async () =>
            {
                IList<ContainerListResponse> containers = await client.Containers.ListContainersAsync(
                    new ContainersListParameters()
                    {
                        Limit = 100,
                    }
                );
                logger.LogInformation("Containers listed");

                foreach (var container in containers) {
                    if (container.Names.Any(x => x == "/" +containerName))
                    {
                        logger.LogInformation($"Found container named {containerName}");
                        var logStream = await client.Containers.GetContainerLogsAsync(
                            container.ID,
                            true,
                            new ContainerLogsParameters()
                            {
                                Follow = true,
                                ShowStderr = true,
                                ShowStdout = true,
                            });

                        await ReadOutputAsync( logStream, logger );
                    }
                }
                logger.LogError("Stopped reading");
                throw new Exception("Container not found");
            });
            readLogs.Start();
        }

        private static async Task ReadOutputAsync(MultiplexedStream multiplexedStream, ILogger<DockerContainerLogStream> logger, CancellationToken cancellationToken = default)
        {
            byte[] buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(81920);
            StringBuilder stringBuilder = new StringBuilder();
            bool emittedLogMsg = false;

            while (true)
            {
                Array.Clear(buffer, 0, buffer.Length);

                MultiplexedStream.ReadResult readResult = await multiplexedStream.ReadOutputAsync(buffer, 0, buffer.Length, cancellationToken);

                if (readResult.EOF)
                {
                    break;
                }

                if (readResult.Count > 0)
                {
                    var responseLine = Encoding.UTF8.GetString(buffer, 0, readResult.Count);
                    stringBuilder.Append(responseLine);

                    var index = stringBuilder.ToString().IndexOf('\n');
                    while (index != -1)
                    {
                        if (!emittedLogMsg)
                        {
                            logger.LogInformation("Successfully read container output");
                            emittedLogMsg = true;
                        }

                        CodeProjectAI.CodeProjectAI.ParseLogMessage(stringBuilder.ToString().Substring(0, index).Trim());
                        stringBuilder = stringBuilder.Remove(0, index+1);

                        index = stringBuilder.ToString().IndexOf('\n');
                    }
                }
            }

            System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}