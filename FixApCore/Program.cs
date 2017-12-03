namespace FixApCore
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// This dotnet core applicaiton tries to recover from weird bugs or connection problems experienced 
    /// on Saunalahti Huawei B315s. It monitors the connection status of the access point
    /// and automatically triggers a reboot when the connection is unavailable. The idea is to 
    /// have a raspberry pi with a scheduled cron job that executes this application periodically.
    /// Code ported from https://github.com/kotylo/b315s-change-network
    /// Jurassic library built from https://github.com/MaitreDede/jurassic/commits/dot-net-core 
    /// commit 746fe6b83c36d186ed8130694112f372d366abc4
    /// </summary>
    public class Program
    {
        private static readonly int delay = 5000;

        private static readonly int retries = 10;

        public static void Main(string[] args)
        {
            MainAsync(args).Wait();
        }

        private static async Task MainAsync(string[] args)
        {
            if (args.Length != 3)
            {
                Console.WriteLine("Expected url, user and password");
                return;
            }

            var url = args[0];
            var user = args[1];
            var password = args[2];

            var manager = new RouterManager(url, user, password);
            for (int x = 0; x < retries; x++)
            {
                var connection = await manager.GetConnectionTypeAsync();
                if (connection != "3G" &&
                    connection != "LTE")
                {
                    Console.WriteLine($"Connection seems down {connection}");
                    Console.WriteLine($"Retring in {delay} seconds");
                    Thread.Sleep(delay);
                    continue;
                }
                else
                {
                    Console.WriteLine($"Connection is {connection}.");
                    return;
                }
            }

            Console.WriteLine("Connection is down.. rebooting.. ");
            if (await manager.LoginAsync())
            {
                await manager.RebootAsync();
            }
            else
            {
                Console.WriteLine("Login failed!");
            }
        }
    }
}
