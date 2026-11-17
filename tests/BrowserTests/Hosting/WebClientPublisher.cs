using System.Diagnostics;
using System.Text;

namespace BrowserTests.Hosting;

/// <summary>
/// Publishes the Blazor WebAssembly client so the browser tests can serve the exact static files a
/// deployment would. A build output alone is not servable: the client's asset fingerprints and the
/// framework files are only materialized by a publish.
/// </summary>
public static class WebClientPublisher
{
    private const string SolutionFileName = "PenguinPlank.sln";
    private const string WebProjectRelativePath = "src/PenguinPlank.Web/PenguinPlank.Web.csproj";
    private static readonly TimeSpan s_publishTimeout = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Finds the build configuration the running test assembly was built with, from the
    /// <c>bin/{Configuration}/{tfm}/</c> layout of its output directory.
    /// </summary>
    /// <param name="baseDirectory">The test assembly output directory.</param>
    /// <returns>The configuration name, or <c>Debug</c> when the layout is not recognized.</returns>
    public static string ResolveConfiguration(string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);

        DirectoryInfo targetFramework = new(baseDirectory.TrimEnd('\\', '/'));
        DirectoryInfo? configuration = targetFramework.Parent;
        DirectoryInfo? bin = configuration?.Parent;

        return configuration is not null
            && bin is not null
            && string.Equals(bin.Name, "bin", StringComparison.OrdinalIgnoreCase)
                ? configuration.Name
                : "Debug";
    }

    /// <summary>Publishes the Web project and returns the directory holding the servable static files.</summary>
    /// <param name="outputDirectory">The directory to publish into.</param>
    /// <param name="cancellationToken">A token that cancels the publish.</param>
    /// <returns>The published <c>wwwroot</c> directory.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the publish fails or times out.</exception>
    public static async Task<string> PublishAsync(string outputDirectory, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        string repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        string project = Path.Combine(repositoryRoot, WebProjectRelativePath);
        string configuration = ResolveConfiguration(AppContext.BaseDirectory);

        ProcessStartInfo startInfo = new("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("publish");
        startInfo.ArgumentList.Add(project);
        startInfo.ArgumentList.Add("--configuration");
        startInfo.ArgumentList.Add(configuration);
        startInfo.ArgumentList.Add("--output");
        startInfo.ArgumentList.Add(outputDirectory);
        startInfo.ArgumentList.Add("--nologo");

        StringBuilder output = new();
        using Process process = new() { StartInfo = startInfo };
        process.OutputDataReceived += (_, e) => AppendLine(output, e.Data);
        process.ErrorDataReceived += (_, e) => AppendLine(output, e.Data);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(s_publishTimeout);

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new InvalidOperationException(
                $"Publishing the Web client did not finish within {s_publishTimeout}.{Environment.NewLine}{output}");
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Publishing the Web client failed with exit code {process.ExitCode}.{Environment.NewLine}{output}");
        }

        string wwwroot = Path.Combine(outputDirectory, "wwwroot");
        if (!File.Exists(Path.Combine(wwwroot, "index.html")))
        {
            throw new InvalidOperationException(
                $"The published Web client has no wwwroot/index.html under '{outputDirectory}'.");
        }

        return wwwroot;
    }

    private static string FindRepositoryRoot(string startDirectory)
    {
        DirectoryInfo? current = new(startDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, SolutionFileName)))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException(
            $"Could not find {SolutionFileName} above '{startDirectory}' to locate the Web project.");
    }

    private static void AppendLine(StringBuilder output, string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (output)
        {
            output.AppendLine(line);
        }
    }
}
