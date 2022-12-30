namespace DownloadBot
{
    public class DownloadBot
    {
        public static void Main(string[] args)
            => new Bot().RunAsync().GetAwaiter().GetResult();
    }
}