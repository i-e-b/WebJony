using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using DispatchSharp;

namespace JwtEndpointCaller
{
    public class Program
    {

        private static void Main()
        {
            var client = new SecuredHttpClient {ServiceRequestTimeout = TimeSpan.FromHours(1)}; // give us time to debug without cancellation errors

            Console.WriteLine("Demo endpoint caller");

            // get a bearer token and spit it out, to help with testing
            Console.WriteLine("Here is a bearer token to use with Swagger:");
            var head = Sync.Run(SecuredHttpClient.AuthenticationHeaderValue);
            Console.WriteLine(head.Scheme + " " + head.Parameter);
            Console.WriteLine("\r\n");

            Console.WriteLine("=== Pick 'E' for local / Azure emulation, 'I' for IIS hosting, 'N' for native ASP,  or 'A' for Azure ===");
            var pick = Console.ReadKey().KeyChar.ToString().ToLowerInvariant();

            string server;
            switch (pick)
            {
                case "e":
                    server = "http://localhost:8080/"; // Azure emulator
                    break;
                case "i":
                    server = "http://localhost:99/"; // IIS hosted test
                    break;
                case "n":
                    server = "http://localhost:18308/"; // IIS hosted test
                    break;
                case "a":
                    server = "https://iebwraptest.cloudapp.net/";
                    break;
                default:
                    Console.WriteLine("Option not known");
                    return;
            }

            //////////////////////////////////////////////////

            Heading("Failure 1:");
            Console.WriteLine("Press [enter] to send an unsecured request to "+server+"values");
            UserWait();


            using (var result = client.GetUnsecuredSync(server+"values", 1))
            {
                Result("Result: " + result.StatusCode + "; Body = " + result.Content.ReadAsStringAsync().Result);
            }

            //////////////////////////////////////////////////

            Heading("Failure 2:");
            Console.WriteLine("Press [enter] to send a secured request, without a version header to "+server+"values");
            UserWait();
            
            using (var result = client.GetSync(server + "values", null))
            {
                Result("Result: " + result.StatusCode + "; Body = " + result.Content.ReadAsStringAsync().Result);
            }

            //////////////////////////////////////////////////

            Heading("Success?");
            Console.WriteLine("Press [enter] to send a secured request, with a version header to "+server+"values");
            UserWait();

            using (var result = client.GetSync(server + "values", 1))
            {
                var body = result.Content.ReadAsStringAsync().Result;
                Result("Result: " + result.StatusCode + "; Body = " + body);
                if (result.StatusCode != HttpStatusCode.OK) {
                    File.WriteAllText(@"C:\Temp\Diagnostic", body);
                }
            }

            //////////////////////////////////////////////////

            Heading("Success?");
            Console.WriteLine("Press [enter] to repeat the last request in multiple threads");
            Console.WriteLine("This will test overall load and response times");
            const int times = 2000;
            const int threads = 30;
            Console.WriteLine("(set to call " + times + " times with " + threads + " threads)");
            UserWait();
            int cursPos = Console.CursorTop;

            var sw = new Stopwatch();
            int errs = 0;
            var consoleLock = new object();
            sw.Start();
            Dispatch<int>.ProcessBatch("BatchRequests", Enumerable.Range(0, times).ToArray(), i =>
            {
                if (i % 10 == 0)
                {
                    lock (consoleLock)
                    {
                        Console.CursorLeft = 1;
                        Console.CursorTop = cursPos;
                        Console.WriteLine(i);
                    }
                }
                var vers = (i % 2) == 0 ? 1 : 3;
                using (var result = client.GetSync(server + "values", vers, false))
                {
                    if (result.StatusCode != HttpStatusCode.OK)
                    {
                        Interlocked.Increment(ref errs);
                        var body = result.Content.ReadAsStringAsync().Result;
                        Console.WriteLine("Result: " + result.StatusCode + "; Body = " + body);
                    }
                }
            }, threads, ex =>
            {
                Interlocked.Increment(ref errs);
                Console.WriteLine("Error: " + ex.Message);
            });
            sw.Stop();
            var cps = (1000.0 * times) / sw.ElapsedMilliseconds;
            Result("Complete. " + errs + " errors. Took " + sw.Elapsed + "(" + cps.ToString("0.0") + " calls per second)");

            //////////////////////////////////////////////////

            Heading("Success?");
            Console.WriteLine("Press [enter] to send a secured request, with a different version header to "+server+"values");
            Console.WriteLine("Note: you should get a different result to above.");
            UserWait();

            using (var result = client.GetSync(server + "values", 3))
            {
                Result("Result: " + result.StatusCode + "; Body = " + result.Content.ReadAsStringAsync().Result);
            }

            //////////////////////////////////////////////////

            Heading("Success?");
            Console.WriteLine("Press [enter] to send a secured request for the swagger docs");
            UserWait();

            using (var result = client.GetSync(server + "/swagger/v1/swagger.json", 3)) // usually (swagger/docs/v1) or (/swagger/v1/swagger.json)
            {
                Result("Result: " + result.StatusCode + "; Body = " + result.Content.ReadAsStringAsync().Result);
            }

            //////////////////////////////////////////////////

            Heading("Failure 3:");
            Console.WriteLine("Press [enter] to send a secured request, with an unknown version to "+server+"values");
            UserWait();

            using (var result = client.GetSync(server + "values", 5))
            {
                Result("Result: " + result.StatusCode + "; Body = " + result.Content.ReadAsStringAsync().Result);
            }

            //////////////////////////////////////////////////

            Heading("Failure 4:");
            Console.WriteLine("Press [enter] to send a secured request, with a known version to "+server+"values/123 -- which throws an exception");
            UserWait();

            using (var result = client.GetSync(server + "values/123", 1))
            {
                Result("Result: " + result.StatusCode + "; Body = " + result.Content.ReadAsStringAsync().Result);
            }

            //////////////////////////////////////////////////

            Console.WriteLine("\r\n\r\nPress [enter] to close demo");
            Console.ReadLine();
        }

        private static void UserWait()
        {
            Console.ReadLine();
            Console.Write("¶");
        }

        private static void Heading(string msg) {
            Console.WriteLine("\r\n\r\n");
            var old = Console.BackgroundColor;
            Console.BackgroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine(msg);
            Console.BackgroundColor = old;
        }

        private static void Result(string msg) {
            var old = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(msg);
            Console.ForegroundColor = old;
        }
    }
}
