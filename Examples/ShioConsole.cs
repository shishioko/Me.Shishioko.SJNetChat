using System;
using System.Threading;
using System.Text;

namespace SJNCCLI
{
    public static class ShioConsole
    {
        private static readonly SemaphoreSlim WriteSync = new (1, 1);
        private static readonly SemaphoreSlim ReadSync = new (1, 1);
        private static string? InputVisual = null;
        static ShioConsole()
        {
            Console.OutputEncoding = Console.OutputEncoding = Encoding.UTF8;
            Console.Write("\u001b[s");
        }
        public static void Write(string text)
        {
            WriteSync.Wait();
            Console.Write($"\u001b[2K\u001b[u{text}\u001b[s\u001b[1E{InputVisual}");
            WriteSync.Release();
        }
        public static void WriteLine(string text)
        {
            Write($"{text}\n");
        }
        public static string ReadLine(bool silent = false)
        {
            return ReadLine(input => input, silent);
        }
        public static string ReadLine(Func<string, string> transformer, bool silent = false)
        {
            int cursor = 0;
            string input = string.Empty;
            ReadSync.Wait();
            try
            {
                while (true)
                {
                    if (!silent) Console.Write($"\u001b[?25l\u001b[2K\u001b[0G{InputVisual = transformer(input)}\u001b[{cursor + 1}G\u001b[?25h");
                    ConsoleKeyInfo key = Console.ReadKey(true);
                    if (!silent) WriteSync.Wait();
                    try
                    {
                        if (char.IsControl(key.KeyChar))
                        {
                            if (key.Key == ConsoleKey.Enter)
                            {
                                if (!silent) Console.Write($"\u001b[2K\u001b[0G");
                                return input;
                            }
                            if (key.Key == ConsoleKey.Backspace)
                            {
                                int erase = int.Min(cursor, key.Modifiers.HasFlag(ConsoleModifiers.Control) ? int.Max(1, cursor - (input[..cursor].LastIndexOf(' ') + 1)) : 1);
                                input = input[..(cursor - erase)] + input[cursor..];
                                cursor -= erase;
                            }
                            if (key.Key == ConsoleKey.Delete)
                            {
                                input = input[..cursor] + input[(cursor == input.Length ? cursor : cursor + 1)..];
                            }
                            if (key.Key == ConsoleKey.LeftArrow)
                            {
                                cursor = int.Clamp(cursor - 1, 0, input.Length);
                                if (key.Modifiers.HasFlag(ConsoleModifiers.Control)) cursor = input[..cursor].LastIndexOf(' ') + 1;
                            }
                            if (key.Key == ConsoleKey.RightArrow)
                            {
                                if (key.Modifiers.HasFlag(ConsoleModifiers.Control))
                                {
                                    int offset = input[cursor..].IndexOf(' ');
                                    cursor = offset < 0 ? input.Length : cursor + offset;
                                }
                                cursor = int.Clamp(cursor + 1, 0, input.Length);
                            }
                        }
                        else
                        {
                            input = input.Insert(cursor++, key.KeyChar.ToString());
                        }
                    }
                    finally
                    {
                        if (!silent) WriteSync.Release();
                    }
                }
            }
            finally
            {
                InputVisual = null;
                ReadSync.Release();
            }
        }
    }
}
