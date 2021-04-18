using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace RunProcessAsTask
{
    // these overloads match the ones in Process.Start to make it a simpler transition for callers
    // see http://msdn.microsoft.com/en-us/library/system.diagnostics.process.start.aspx
    public partial class ProcessEx
    {
        /// <summary>
        /// Runs asynchronous process.
        /// </summary>
        /// <param name="fileName">An application or document which starts the process.</param>
        public static Task<ProcessResults> RunAsync(string fileName)
            => RunAsync(new ProcessStartInfo(fileName));

        /// <summary>
        /// Runs asynchronous process.
        /// </summary>
        /// <param name="fileName">An application or document which starts the process.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        public static Task<ProcessResults> RunAsync(string fileName, CancellationToken cancellationToken)
            => RunAsync(new ProcessStartInfo(fileName), cancellationToken);

        /// <summary>
        /// Runs asynchronous process.
        /// </summary>
        /// <param name="fileName">An application or document which starts the process.</param>
        /// <param name="arguments">Command-line arguments to pass to the application when the process starts.</param>
        public static Task<ProcessResults> RunAsync(string fileName, string arguments)
            => RunAsync(new ProcessStartInfo(fileName, arguments));

        /// <summary>
        /// Runs asynchronous process.
        /// </summary>
        /// <param name="fileName">An application or document which starts the process.</param>
        /// <param name="arguments">Command-line arguments to pass to the application when the process starts.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        public static Task<ProcessResults> RunAsync(string fileName, string arguments, CancellationToken cancellationToken)
            => RunAsync(new ProcessStartInfo(fileName, arguments), cancellationToken);

        /// <summary>
        /// Runs asynchronous process.
        /// </summary>
        /// <param name="processStartInfo">The <see cref="T:System.Diagnostics.ProcessStartInfo" /> that contains the information that is used to start the process, including the file name and any command-line arguments.</param>
        public static Task<ProcessResults> RunAsync(ProcessStartInfo processStartInfo)
            => RunAsync(processStartInfo, CancellationToken.None);

        /// <summary>
        /// Runs asynchronous process.
        /// </summary>
        /// <param name="processStartInfo">The <see cref="T:System.Diagnostics.ProcessStartInfo" /> that contains the information that is used to start the process, including the file name and any command-line arguments.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        public static Task<ProcessResults> RunAsync(ProcessStartInfo processStartInfo, CancellationToken cancellationToken)
            => RunAsync(processStartInfo, new List<string>(), new List<string>(), cancellationToken);

        /// <summary>
        /// Runs asynchronous process.
        /// </summary>
        /// <param name="processStartInfo">The <see cref="T:System.Diagnostics.ProcessStartInfo" /> that contains the information that is used to start the process, including the file name and any command-line arguments.</param>
        /// <param name="standardOutput">List that lines written to standard output by the process will be added to</param>
        /// <param name="standardError">List that lines written to standard error by the process will be added to</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        public static Task<ProcessResults> RunAsync(ProcessStartInfo processStartInfo, List<string> standardOutput, List<string> standardError, CancellationToken cancellationToken)
            => RunAsync(processStartInfo, o => standardOutput.Add(o), e => standardError.Add(e), cancellationToken);    
    }
}
