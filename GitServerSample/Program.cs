using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;

namespace GitServerSample
{
    static class Program
    {
        internal static string GitExe = @"C:\Program Files\Git\bin\git.exe";
        internal static string RepositoryPath;

        // initialize git repo as follow
        // > mkdir repository
        // > cd repository
        // > git init .
        // > git config receive.denyCurrentBranch ignore
        static void Main(string[] args)
        {
            if (Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.Unix)
            {
                GitExe = "/usr/bin/git";
            }

            try
            {
                RepositoryPath = args[0];
                var port = Int32.Parse(args[1]);
                string prefix = String.Format("http://localhost:{0}/", port);
                var listener = new HttpListener();
                listener.Prefixes.Add(prefix);
                listener.Start();
                Console.WriteLine("Listening at " + prefix);
                while (true)
                {
                    // simple handle one request at a time
                    ProcessRequest(listener.GetContext());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        static void ProcessRequest(HttpListenerContext context)
        {
            Console.WriteLine("{0} {1} {2}", DateTime.UtcNow.ToString("o"), context.Request.HttpMethod, context.Request.Url.PathAndQuery);

            var service = GetService(context.Request.Url.PathAndQuery);
            context.Response.Headers["Content-Type"] = String.Format("application/x-git-{0}-advertisement", service);
            context.Response.Headers["Expires"] = "Fri, 01 Jan 1980 00:00:00 GMT";
            context.Response.Headers["Pragma"] = "no-cache";
            context.Response.Headers["Cache-Control"] = "no-cache, max-age=0, must-revalidate";

            if (context.Request.HttpMethod == "GET" && context.Request.Url.AbsolutePath == "/info/refs")
            {
                var input = String.Format("# service=git-{0}\n", service);
                var toWrite = (input.Length + 4).ToString("x").PadLeft(4, '0') + input;
                context.Response.OutputStream.Write(Encoding.UTF8.GetBytes(toWrite), 0, Encoding.UTF8.GetByteCount(toWrite));

                toWrite = "0000";
                context.Response.OutputStream.Write(Encoding.UTF8.GetBytes(toWrite), 0, Encoding.UTF8.GetByteCount(toWrite));

                var exe = new Executable();
                var args = String.Format("{0} --stateless-rpc --advertise-refs \"{1}\"", service, Program.RepositoryPath);
                exe.ExecuteAsync(args, context.Response.OutputStream).Wait();
                context.Response.Close();
            }
            else
            {
                var exe = new Executable();
                var args = String.Format("{0} --stateless-rpc \"{1}\"", service, Program.RepositoryPath);
                exe.ExecuteAsync(args, context.Response.OutputStream, GetInputStream(context.Request)).Wait();
                context.Response.Close();
            }
        }

        static Stream GetInputStream(HttpListenerRequest request)
        {
            var contentEncoding = request.Headers["Content-Encoding"];

            if (contentEncoding != null && contentEncoding.Contains("gzip"))
            {
                return new GZipStream(request.InputStream, CompressionMode.Decompress);
            }

            return request.InputStream;
        }

        static string GetService(string pathAndQuery)
        {
            return pathAndQuery.Substring(pathAndQuery.IndexOf("git-") + "git-".Length);
        }
    }
}
