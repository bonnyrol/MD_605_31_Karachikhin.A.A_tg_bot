using dotenv.net;

namespace tg_bot
{
    internal class Program
    {
        static void Main(string[] args)
        {
            DotEnv.Load();
            var envVars = DotEnv.Read();

            Console.WriteLine("Hello, World! " + envVars["TG_TOKEN"]);
        }
    }
}
