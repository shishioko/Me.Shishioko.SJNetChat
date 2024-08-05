namespace Me.Shishioko.SJNetChat.Test
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("[ server ] [ client ]");
            string? option = Console.ReadLine();
            if (option == "server") await ExampleServer.Main();
            else if (option == "client") await ExampleClient.Main();
            else Console.WriteLine("Unknown option!");
        }
    }
}
