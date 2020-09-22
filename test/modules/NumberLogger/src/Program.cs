// Copyright (c) Microsoft. All rights reserved.
namespace NumberLogger
{
    using System;
    using System.Threading.Tasks;

    class Program
    {
        public static void Main()
        {
            string countString = Environment.GetEnvironmentVariable("Count");
            int parsedCount = int.Parse(countString);

            for (int i = 0; i < parsedCount; i++)
            {
                Console.WriteLine(i);
            }

            Task.Delay(-1).Wait();
        }
    }
}
