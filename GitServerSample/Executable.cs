using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GitServerSample
{
    public class Executable
    {
        public Executable()
        {
            Path = Program.GitExe;
            WorkingDirectory = Program.RepositoryPath;
        }

        public string WorkingDirectory { get; private set; }

        public string Path { get; private set; }

        // This is pure async process execution
        public async Task<int> ExecuteAsync(string arguments, Stream output, Stream input = null)
        {
            using (Process process = CreateProcess(arguments))
            {
                return await Start(process, output, input);
            }
        }
        public static async Task<int> Start(Process process, Stream output, Stream input = null)
        {
            var cancellationTokenSource = new CancellationTokenSource();

            process.Start();

            var tasks = new List<Task>();

            if (input != null)
            {
                tasks.Add(CopyStreamAsync(input, process.StandardInput.BaseStream, cancellationTokenSource.Token, closeAfterCopy: true));
            }

            var error = new MemoryStream();
            tasks.Add(CopyStreamAsync(process.StandardOutput.BaseStream, output, cancellationTokenSource.Token));
            tasks.Add(CopyStreamAsync(process.StandardError.BaseStream, error, cancellationTokenSource.Token));

            process.WaitForExit();

            await Task.WhenAll(tasks);

            return process.ExitCode;
        }

        private static async Task CopyStreamAsync(Stream from, Stream to, CancellationToken cancellationToken, bool closeAfterCopy = false)
        {
            try
            {
                byte[] bytes = new byte[1024];
                int read = 0;
                while ((read = await from.ReadAsync(bytes, 0, bytes.Length, cancellationToken)) != 0)
                {
                    await to.WriteAsync(bytes, 0, read, cancellationToken);
                }
            }
            finally
            {
                // this is needed specifically for input stream
                // in order to tell executable that the input is done
                if (closeAfterCopy)
                {
                    to.Close();
                }
            }
        }

        internal Process CreateProcess(string arguments)
        {
            var psi = new ProcessStartInfo
            {
                FileName = Path,
                WorkingDirectory = WorkingDirectory,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                ErrorDialog = false,
                Arguments = arguments
            };

            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;

            return new Process()
            {
                StartInfo = psi
            };
        }
    }
}
